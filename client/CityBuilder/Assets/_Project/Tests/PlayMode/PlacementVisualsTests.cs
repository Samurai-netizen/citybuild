#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Core;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.City;
using CityBuilder.Simulation;
using CityBuilder.Simulation.State;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// What the placement flow draws on the city (M0-46): the green and red tiles of the hints, and the translucent ghost of a command on its
    /// way, which comes from the command sender's pending list and goes when the sender ends the command.
    /// </summary>
    public sealed class PlacementVisualsTests
    {
        /// <summary>The object that holds the world under test.</summary>
        private GameObject _host = null!;

        /// <summary>The world under test, showing an 8 by 8 grid.</summary>
        private CityWorld _world = null!;

        /// <summary>The network the sender talks to; answers when the test says.</summary>
        private ControllableTransport _transport = null!;

        /// <summary>The sender whose pending list the ghosts follow.</summary>
        private CommandSender _sender = null!;

        /// <summary>Cancels commands still open at the end of a test.</summary>
        private CancellationTokenSource _cancel = null!;

        /// <summary>Before each test: a world with a grid, and a command sender over a transport that answers when told to.</summary>
        [SetUp]
        public void CreateWorld()
        {
            _host = new GameObject("Test world host");
            _world = _host.AddComponent<CityWorld>();
            _world.ShowGrid(new CityGrid(8, 8));
            _transport = new ControllableTransport();
            var api = new ApiClient("http://localhost:8080", 30, _transport);
            _sender = new CommandSender(api, new ClientCityStore(), CommandPolicy.Default);
            _cancel = new CancellationTokenSource();
        }

        /// <summary>After each test: ends open commands and destroys the world.</summary>
        [TearDown]
        public void DestroyWorld()
        {
            _cancel.Cancel();
            _cancel.Dispose();
            Object.DestroyImmediate(_host);
        }

        /// <summary>The block of a tile of the ground.</summary>
        private Renderer Tile(int x, int y)
        {
            return _world.Ground!.Root.transform.Find("Tile " + new GridCoordinate(x, y)).GetComponent<Renderer>();
        }

        /// <summary>One hint per tile of the 8 by 8 grid, all free except the listed tiles.</summary>
        private static TileHint[] Hints(params (int X, int Y)[] occupied)
        {
            var hints = new TileHint[64];
            foreach ((int x, int y) in occupied)
            {
                hints[y * 8 + x] = TileHint.Occupied;
            }

            return hints;
        }

        /// <summary>Starts a place-building command that stays open, and lists it in the world's ghosts.</summary>
        private Task<CommandOutcome> StartPlacing(string typeId, int x, int y)
        {
            Task<CommandOutcome> task = _sender.SendAsync(CommandIntent.PlaceBuilding(typeId, x, y), _cancel.Token);
            _world.ShowGhosts(_sender.Pending, 1);
            return task;
        }

        /// <summary>Free tiles are painted green and occupied ones red, and the other tiles keep their colour.</summary>
        [Test]
        public void ShowHints_PaintsFreeTilesGreen_AndOccupiedTilesRed()
        {
            Color before = Tile(4, 4).sharedMaterial.color;

            _world.ShowHints(Hints((1, 0)));

            Color free = Tile(4, 4).sharedMaterial.color;
            Color occupied = Tile(1, 0).sharedMaterial.color;
            Assert.That(free.g, Is.GreaterThan(free.r + 0.3f), "a free tile is green");
            Assert.That(occupied.r, Is.GreaterThan(occupied.g + 0.3f), "an occupied tile is red");
            Assert.That(free, Is.Not.EqualTo(before));
            Assert.That(_world.Ground!.ShownHintAt(new GridCoordinate(1, 0)), Is.EqualTo(TileHint.Occupied));
            Assert.That(_world.Ground.ShownHintAt(new GridCoordinate(4, 4)), Is.EqualTo(TileHint.Free));
        }

        /// <summary>Showing no hints gives every tile its own ground colour back, chessboard included.</summary>
        [Test]
        public void ShowHints_Null_RestoresTheGround()
        {
            Color light = Tile(0, 0).sharedMaterial.color;
            Color dark = Tile(1, 0).sharedMaterial.color;
            _world.ShowHints(Hints((1, 0)));

            _world.ShowHints(null);

            Assert.That(Tile(0, 0).sharedMaterial.color, Is.EqualTo(light));
            Assert.That(Tile(1, 0).sharedMaterial.color, Is.EqualTo(dark));
            Assert.That(_world.Ground!.ShownHintAt(new GridCoordinate(1, 0)), Is.Null);
        }

        /// <summary>A hint that changes (a building appeared) repaints that tile only.</summary>
        [Test]
        public void ShowHints_FollowsAChangedHint()
        {
            _world.ShowHints(Hints());
            Assert.That(_world.Ground!.ShownHintAt(new GridCoordinate(2, 2)), Is.EqualTo(TileHint.Free));

            _world.ShowHints(Hints((2, 2)));

            Assert.That(_world.Ground.ShownHintAt(new GridCoordinate(2, 2)), Is.EqualTo(TileHint.Occupied));
            Assert.That(Tile(2, 2).sharedMaterial.color.r, Is.GreaterThan(0.6f));
        }

        /// <summary>A hint array that does not match the grid is refused, not half applied.</summary>
        [Test]
        public void ShowHints_WithTheWrongNumberOfHints_Throws()
        {
            Assert.Throws<ArgumentException>(() => _world.ShowHints(new TileHint[10]));
        }

        /// <summary>
        /// A place-building command on its way gets one translucent ghost, of the type's look, in the middle of its tile, with no colliders
        /// (a primitive's collider is destroyed at the end of the frame, so the test waits one).
        /// </summary>
        [UnityTest]
        public IEnumerator ACommandOnItsWay_HasATranslucentGhostOnItsTile()
        {
            _ = StartPlacing("house", 2, 3);
            yield return null;

            Assert.That(_world.Ghosts!.Count, Is.EqualTo(1));
            Assert.That(_sender.Pending.Count, Is.EqualTo(1));
            Assert.That(_world.Ghosts.TryGetGhost(_sender.Pending[0].RequestId, out GameObject? ghost), Is.True);
            Assert.That(ghost!.transform.localPosition, Is.EqualTo(new Vector3(2.5f, 0f, 3.5f)));
            Assert.That(ghost.transform.childCount, Is.EqualTo(1), "one instance of the house model");
            Assert.That(ghost.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(0));
            foreach (Renderer renderer in ghost.GetComponentsInChildren<Renderer>())
            {
                // The first material only writes depth, so the layers of the model do not blend over each other; the second is the see-through one.
                Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(2));
                Material seeThrough = renderer.sharedMaterials[1];
                Assert.That(seeThrough.color.a, Is.EqualTo(PlacementGhosts.Alpha).Within(0.001f));
                Assert.That(seeThrough.renderQueue, Is.GreaterThanOrEqualTo(3000), "drawn with the transparent objects");
                Assert.That(renderer.sharedMaterials[0].renderQueue, Is.LessThan(seeThrough.renderQueue), "the depth comes first");
                Assert.That(renderer.GetComponent<Collider>(), Is.Null);
            }
        }

        /// <summary>The ghost of a model has its own see-through copy of the art's material; the shared material that real buildings use stays opaque.</summary>
        [UnityTest]
        public IEnumerator AGhostOfAModel_NeverChangesTheSharedMaterial()
        {
            _ = StartPlacing("house", 2, 3);
            yield return null;
            _world.Ghosts!.TryGetGhost(_sender.Pending[0].RequestId, out GameObject? ghost);
            Material ghostMaterial = ghost!.GetComponentInChildren<Renderer>().sharedMaterials[1];

            Material shared = BuildingLooks.FindModel("house", 1, 1)!.GetComponentInChildren<Renderer>().sharedMaterial;
            Assert.That(ghostMaterial, Is.Not.SameAs(shared));
            Assert.That(ghostMaterial.mainTexture, Is.SameAs(shared.mainTexture), "it keeps the palette");
            Assert.That(shared.color.a, Is.EqualTo(1f));
            Assert.That(shared.renderQueue, Is.LessThan(3000));
        }

        /// <summary>A type with no model still gets a ghost, from its primitives.</summary>
        [UnityTest]
        public IEnumerator AGhostOfATypeWithoutAModel_IsMadeFromPrimitives()
        {
            _ = StartPlacing("castle", 1, 1);
            yield return null;

            _world.Ghosts!.TryGetGhost(_sender.Pending[0].RequestId, out GameObject? ghost);
            Assert.That(ghost!.transform.childCount, Is.EqualTo(BuildingLooks.Fallback.Parts.Count));
        }

        /// <summary>A ghost is not a building: the building views stay empty.</summary>
        [Test]
        public void AGhost_IsNotABuilding()
        {
            _ = StartPlacing("house", 2, 3);

            Assert.That(_world.Buildings!.Count, Is.EqualTo(0));
        }

        /// <summary>Showing the same pending list again makes no second ghost.</summary>
        [Test]
        public void ShowingTheSamePendingCommandTwice_MakesOneGhost()
        {
            _ = StartPlacing("house", 2, 3);

            _world.ShowGhosts(_sender.Pending, 1);

            Assert.That(_world.Ghosts!.Count, Is.EqualTo(1));
            Assert.That(_world.Ghosts.Root.transform.childCount, Is.EqualTo(1));
        }

        /// <summary>Commands that are not place-building (the worker stepper, an upgrade) get no ghost.</summary>
        [Test]
        public void OtherCommands_GetNoGhost()
        {
            _ = _sender.SendAsync(CommandIntent.UpgradeBuilding(3), _cancel.Token);
            _world.ShowGhosts(_sender.Pending, 1);

            Assert.That(_sender.Pending.Count, Is.EqualTo(1));
            Assert.That(_world.Ghosts!.Count, Is.EqualTo(0));
        }

        /// <summary>When the sender ends the command (here: the server refused it) the ghost is gone at once.</summary>
        [UnityTest]
        public IEnumerator WhenTheCommandEnds_TheGhostGoes()
        {
            Task<CommandOutcome> task = StartPlacing("house", 2, 3);
            yield return null;
            Assert.That(_transport.OpenCount, Is.EqualTo(1));

            _transport.Respond(409, File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Tests/Fixtures/error-409-TILE_OCCUPIED.json")));
            while (!task.IsCompleted)
            {
                yield return null;
            }

            _world.ShowGhosts(_sender.Pending, 1);
            Assert.That(task.Result.Kind, Is.EqualTo(CommandOutcomeKind.Failed));
            Assert.That(_world.Ghosts!.Count, Is.EqualTo(0));
            Assert.That(_world.Ghosts.Root.transform.childCount, Is.EqualTo(0));
        }

        /// <summary>
        /// The ghost really is see-through on screen, not only flagged so: the same tile is drawn bare, with the ghost and with the finished
        /// building, and the ghost's picture lies between the other two (the render pipeline could ignore the material's transparency, or a
        /// build could strip its shader variant, and no property check would notice). Skipped when the machine has no graphics device.
        /// </summary>
        [UnityTest]
        public IEnumerator TheGhost_IsDrawnBetweenTheBareTileAndTheFinishedBuilding()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("There is no graphics device, so nothing can be drawn.");
            }

            Camera camera = _world.Camera;
            _world.Rig!.Model.Frame(1f);
            _world.Rig.Apply();
            var texture = RenderTexture.GetTemporary(512, 512, 24);
            camera.targetTexture = texture;
            camera.aspect = 1f;
            yield return null;
            try
            {
                var tile = new GridCoordinate(2, 2);
                Color bare = MeanColorOver(tile);

                _ = StartPlacing("house", tile.X, tile.Y);
                Color ghost = MeanColorOver(tile);
                _world.ShowGhosts(new PendingCommand[0], 1);

                var built = new List<Building> { new Building(1, new BuildingTypeId("house"), tile.X, tile.Y, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0) };
                _world.ShowCity(new CityState(0, 1, 2, new List<KeyValuePair<ItemId, long>>(), built, new List<ResearchId>(), null));
                Color finished = MeanColorOver(tile);

                float ghostShift = Distance(ghost, bare);
                float finishedShift = Distance(finished, bare);
                Assert.That(finishedShift, Is.GreaterThan(0.05f), "the finished building must differ from the bare tile for this test to mean anything");
                Assert.That(ghostShift, Is.GreaterThan(finishedShift * 0.25f), "the ghost is visible");
                Assert.That(ghostShift, Is.LessThan(finishedShift * 0.75f), "the ghost lets the tile show through");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(texture);
            }
        }

        /// <summary>The mean colour of the middle of a tile as the world's camera draws it into its render texture.</summary>
        private Color MeanColorOver(GridCoordinate tile)
        {
            Camera camera = _world.Camera;
            camera.Render();
            Vector3 low = camera.WorldToScreenPoint(new Vector3(tile.X + 0.2f, 0f, tile.Y + 0.2f));
            Vector3 high = camera.WorldToScreenPoint(new Vector3(tile.X + 0.8f, 0.5f, tile.Y + 0.8f));
            int left = Mathf.Max(0, Mathf.RoundToInt(Mathf.Min(low.x, high.x)));
            int bottom = Mathf.Max(0, Mathf.RoundToInt(Mathf.Min(low.y, high.y)));
            int width = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(high.x - low.x)));
            int height = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(high.y - low.y)));
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = camera.targetTexture;
            var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(left, bottom, width, height), 0, 0);
            Color[] pixels = picture.GetPixels();
            RenderTexture.active = previous;
            Object.DestroyImmediate(picture);
            var sum = new Vector3();
            foreach (Color pixel in pixels)
            {
                sum += new Vector3(pixel.r, pixel.g, pixel.b);
            }

            sum /= pixels.Length;
            return new Color(sum.x, sum.y, sum.z);
        }

        /// <summary>How far apart two colours are, as the length of the difference of their red, green and blue.</summary>
        private static float Distance(Color a, Color b)
        {
            return new Vector3(a.r - b.r, a.g - b.g, a.b - b.b).magnitude;
        }
    }
}
