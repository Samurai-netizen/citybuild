#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Net;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The translucent "pending" buildings of the placement flow (M0-46): one ghost, in the look of the type and the city's era, on the tile of
    /// every place-building command that <see cref="CommandSender.Pending"/> lists. The ghost is not a building: nothing is created, paid
    /// or counted. It is there while the command is on its way and goes when the sender ends it, in the same call that hands the new snapshot
    /// to the store, so the real building (from the snapshot) and the ghost are never both missing or both seen. It has no collider and never
    /// calls the server. <see cref="Destroy"/> removes everything it made.
    /// </summary>
    public sealed class PlacementGhosts
    {
        /// <summary>The name of the object that holds the ghosts.</summary>
        public const string RootName = "Ghosts";

        /// <summary>How opaque a ghost is: enough to read what it is, little enough to read that it is not built.</summary>
        public const float Alpha = 0.5f;

        /// <summary>The ghosts by the request id of their command.</summary>
        private readonly Dictionary<Guid, GameObject> _ghosts = new Dictionary<Guid, GameObject>();

        /// <summary>The request ids seen in the current call to <see cref="Show"/>; kept to avoid a new set each time.</summary>
        private readonly HashSet<Guid> _seen = new HashSet<Guid>();

        /// <summary>The ids to remove in the current call; kept for the same reason.</summary>
        private readonly List<Guid> _gone = new List<Guid>();

        /// <summary>The translucent materials, one per colour, destroyed with the ghosts.</summary>
        private readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();

        /// <summary>The grid whose tiles the ghosts stand on.</summary>
        private readonly CityGrid _grid;

        /// <summary>Whether <see cref="Destroy"/> has run.</summary>
        private bool _destroyed;

        /// <summary>Creates the ghosts' holder under a new object called <c>Ghosts</c> that is a child of <paramref name="parent"/>.</summary>
        /// <param name="grid">The grid the ghosts stand on.</param>
        /// <param name="parent">The transform the ghosts hang under.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public PlacementGhosts(CityGrid grid, Transform parent)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            Root = new GameObject(RootName);
            Root.transform.SetParent(parent, false);
        }

        /// <summary>The object that holds every ghost.</summary>
        public GameObject Root { get; }

        /// <summary>The number of ghosts shown.</summary>
        public int Count => _ghosts.Count;

        /// <summary>The ghost of a command, or false when none is shown.</summary>
        /// <param name="requestId">The request id of the pending command.</param>
        /// <param name="ghost">The ghost's object, or null.</param>
        /// <returns>True when the command has a ghost.</returns>
        public bool TryGetGhost(Guid requestId, out GameObject? ghost)
        {
            return _ghosts.TryGetValue(requestId, out ghost);
        }

        /// <summary>
        /// Makes the ghosts match <paramref name="pending"/>: a ghost for each place-building command that has none, none left for a command
        /// that ended. Commands of other kinds are ignored.
        /// </summary>
        /// <param name="pending">The sender's pending commands.</param>
        /// <param name="era">The city's era, which picks the look.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pending"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">The ghosts were destroyed.</exception>
        public void Show(IReadOnlyList<PendingCommand> pending, int era)
        {
            if (pending == null)
            {
                throw new ArgumentNullException(nameof(pending));
            }

            if (_destroyed)
            {
                throw new ObjectDisposedException(nameof(PlacementGhosts));
            }

            _seen.Clear();
            for (int i = 0; i < pending.Count; i++)
            {
                PendingCommand command = pending[i];
                if (command.Intent.Kind != CommandKind.PlaceBuilding)
                {
                    continue;
                }

                _seen.Add(command.RequestId);
                if (!_ghosts.ContainsKey(command.RequestId))
                {
                    _ghosts.Add(command.RequestId, CreateGhost(command.Intent, era));
                }
            }

            if (_seen.Count == _ghosts.Count)
            {
                return;
            }

            _gone.Clear();
            foreach (KeyValuePair<Guid, GameObject> entry in _ghosts)
            {
                if (!_seen.Contains(entry.Key))
                {
                    _gone.Add(entry.Key);
                }
            }

            for (int i = 0; i < _gone.Count; i++)
            {
                GameObject ghost = _ghosts[_gone[i]];
                _ghosts.Remove(_gone[i]);

                // Out of the tree at once, so a delayed destroy leaves no ghost that still counts as a child this frame.
                ghost.transform.SetParent(null, false);
                PlaceholderArt.Destroy(ghost);
            }
        }

        /// <summary>Destroys every ghost, the root object and the materials. Safe to call twice.</summary>
        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;
            _ghosts.Clear();
            PlaceholderArt.Destroy(Root);
            foreach (Material material in _materials.Values)
            {
                PlaceholderArt.Destroy(material);
            }

            _materials.Clear();
        }

        /// <summary>Makes the ghost of one place-building intent: the finished level 1 look of its type, on its tile, in translucent parts.</summary>
        private GameObject CreateGhost(CommandIntent intent, int era)
        {
            var ghost = new GameObject("Ghost " + intent.ContentId + " " + intent.X + "," + intent.Y);
            ghost.transform.SetParent(Root.transform, false);
            ghost.transform.localPosition = _grid.ToWorldCenter(new GridCoordinate(intent.X, intent.Y));
            BuildingLook look = BuildingLooks.Get(intent.ContentId!, era);
            for (int i = 0; i < look.Parts.Count; i++)
            {
                BuildingPart part = look.Parts[i];
                if (!_materials.TryGetValue(part.Color, out Material? material))
                {
                    material = PlaceholderArt.CreateTranslucentMaterial(part.Color, Alpha);
                    _materials.Add(part.Color, material);
                }

                BuildingViewFactory.CreatePrimitive(ghost.transform, part, material);
            }

            return ghost;
        }
    }
}
