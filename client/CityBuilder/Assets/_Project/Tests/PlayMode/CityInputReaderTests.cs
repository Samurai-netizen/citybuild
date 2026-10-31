#nullable enable

using CityBuilder.Client.Presentation.City;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The input reader with fake mouse and touch devices (M0-43): a mouse or finger tap selects a tile, a drag pans without selecting, the wheel and a pinch
    /// zoom, and the finger left after a pinch does not tap; a press that starts over the UI (M0-45) is the UI's and never reaches the ground. The devices come from the Input System's test fixture, which also hides the real ones.
    /// </summary>
    public sealed class CityInputReaderTests : InputTestFixture
    {
        /// <summary>The object that holds the world under test.</summary>
        private GameObject _host = null!;

        /// <summary>The world under test, showing an 8 by 8 grid.</summary>
        private CityWorld _world = null!;

        /// <summary>The reader under test; the test calls its <see cref="CityInputReader.Tick"/> itself, because no frame passes in these tests.</summary>
        private CityInputReader _reader = null!;

        /// <summary>The fake mouse.</summary>
        private Mouse _mouse = null!;

        /// <summary>Before each test (after the fixture's own set-up): adds the fake devices and creates the world.</summary>
        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.AddDevice<Touchscreen>();
            _host = new GameObject("Test world host");
            _world = _host.AddComponent<CityWorld>();
            _world.ShowGrid(new CityGrid(8, 8));
            _reader = new CityInputReader(_world.Rig!, 160f);
        }

        /// <summary>After each test: destroys the world, then lets the fixture remove the devices.</summary>
        public override void TearDown()
        {
            Object.DestroyImmediate(_host);
            base.TearDown();
        }

        /// <summary>A mouse click on a tile selects it, once the button is released.</summary>
        [Test]
        public void MouseClick_OnATile_SelectsIt()
        {
            Vector2 onTile = ScreenOf(new Vector3(5.5f, 0f, 6.5f));

            Set(_mouse.position, onTile);
            Press(_mouse.leftButton);
            _reader.Tick();
            Assert.That(_world.Selection!.Selected, Is.Null, "nothing is selected while the button is still down");

            Release(_mouse.leftButton);
            _reader.Tick();

            Assert.That(_world.Selection.Selected, Is.EqualTo(new GridCoordinate(5, 6)));
        }

        /// <summary>A mouse drag pans the camera and does not select the tile it ended on.</summary>
        [Test]
        public void MouseDrag_PansTheCamera_AndSelectsNothing()
        {
            _world.Rig!.Zoom(0.5f);
            Vector2 start = ScreenOf(new Vector3(4f, 0f, 4f));
            Vector2 focusBefore = _world.Rig.Model.Focus;

            Set(_mouse.position, start);
            Press(_mouse.leftButton);
            _reader.Tick();
            Set(_mouse.position, start + new Vector2(60f, 0f));
            _reader.Tick();
            Release(_mouse.leftButton);
            _reader.Tick();

            Assert.That(_world.Rig.Model.Focus, Is.Not.EqualTo(focusBefore));
            Assert.That(_world.Selection!.Selected, Is.Null);
        }

        /// <summary>The mouse pointer marks the tile under it while no button is pressed.</summary>
        [Test]
        public void MouseMove_MarksTheTileUnderThePointer()
        {
            Set(_mouse.position, ScreenOf(new Vector3(1.5f, 0f, 2.5f)));
            _reader.Tick();

            Assert.That(_world.Selection!.Hovered, Is.EqualTo(new GridCoordinate(1, 2)));
        }

        /// <summary>The wheel zooms in on a scroll up and out on a scroll down.</summary>
        [Test]
        public void MouseWheel_Zooms()
        {
            float start = _world.Rig!.Model.Distance;

            Set(_mouse.scroll, new Vector2(0f, 120f));
            _reader.Tick();
            float closer = _world.Rig.Model.Distance;
            Set(_mouse.scroll, new Vector2(0f, -120f));
            _reader.Tick();

            Assert.That(closer, Is.LessThan(start));
            Assert.That(_world.Rig.Model.Distance, Is.GreaterThan(closer));
        }

        /// <summary>A finger tap on a tile selects it when the finger lifts, at the place it was last seen.</summary>
        [Test]
        public void FingerTap_OnATile_SelectsIt()
        {
            Vector2 onTile = ScreenOf(new Vector3(2.5f, 0f, 3.5f));

            BeginTouch(1, onTile);
            _reader.Tick();
            EndTouch(1, onTile);
            _reader.Tick();

            Assert.That(_world.Selection!.Selected, Is.EqualTo(new GridCoordinate(2, 3)));
        }

        /// <summary>A finger drag pans the camera, and lifting it selects nothing.</summary>
        [Test]
        public void FingerDrag_PansTheCamera_AndSelectsNothing()
        {
            _world.Rig!.Zoom(0.5f);
            Vector2 start = ScreenOf(new Vector3(4f, 0f, 4f));
            Vector2 focusBefore = _world.Rig.Model.Focus;

            BeginTouch(1, start);
            _reader.Tick();
            MoveTouch(1, start + new Vector2(0f, 80f));
            _reader.Tick();
            EndTouch(1, start + new Vector2(0f, 80f));
            _reader.Tick();

            Assert.That(_world.Rig.Model.Focus, Is.Not.EqualTo(focusBefore));
            Assert.That(_world.Selection!.Selected, Is.Null);
        }

        /// <summary>Two fingers moving apart zoom in; lifting them one after the other selects nothing.</summary>
        [Test]
        public void Pinch_Zooms_AndTheFingerLeftAfterwardsDoesNotTap()
        {
            Vector2 middle = ScreenOf(new Vector3(4f, 0f, 4f));
            float start = _world.Rig!.Model.Distance;

            BeginTouch(1, middle - new Vector2(40f, 0f));
            BeginTouch(2, middle + new Vector2(40f, 0f));
            _reader.Tick();
            MoveTouch(1, middle - new Vector2(120f, 0f));
            MoveTouch(2, middle + new Vector2(120f, 0f));
            _reader.Tick();
            Assert.That(_world.Rig.Model.Distance, Is.LessThan(start), "spreading the fingers zooms in");

            EndTouch(2, middle + new Vector2(120f, 0f));
            _reader.Tick();
            EndTouch(1, middle - new Vector2(120f, 0f));
            _reader.Tick();

            Assert.That(_world.Selection!.Selected, Is.Null);
        }

        /// <summary>A reader for which everything within 30 pixels of <paramref name="center"/> is over the UI (a button, say).</summary>
        private CityInputReader ReaderWithUiAt(Vector2 center)
        {
            return new CityInputReader(_world.Rig!, 160f, position => (position - center).sqrMagnitude < 30f * 30f);
        }

        /// <summary>A mouse click on the UI selects no tile: the press belongs to the UI.</summary>
        [Test]
        public void MouseClick_OverTheUi_SelectsNothing()
        {
            Vector2 onUi = ScreenOf(new Vector3(5.5f, 0f, 6.5f));
            CityInputReader reader = ReaderWithUiAt(onUi);

            Set(_mouse.position, onUi);
            Press(_mouse.leftButton);
            reader.Tick();
            Release(_mouse.leftButton);
            reader.Tick();

            Assert.That(_world.Selection!.Selected, Is.Null);
        }

        /// <summary>A press that starts on the UI and moves off it neither pans nor taps the ground it ends on.</summary>
        [Test]
        public void MousePress_StartedOverTheUi_AndMovedOffIt_DoesNothingToTheGround()
        {
            _world.Rig!.Zoom(0.5f);
            Vector2 onUi = ScreenOf(new Vector3(4f, 0f, 4f));
            CityInputReader reader = ReaderWithUiAt(onUi);
            Vector2 focusBefore = _world.Rig.Model.Focus;

            Set(_mouse.position, onUi);
            Press(_mouse.leftButton);
            reader.Tick();
            Set(_mouse.position, onUi + new Vector2(120f, 0f));
            reader.Tick();
            Release(_mouse.leftButton);
            reader.Tick();

            Assert.That(_world.Rig.Model.Focus, Is.EqualTo(focusBefore));
            Assert.That(_world.Selection!.Selected, Is.Null);
        }

        /// <summary>After the UI press ended, the next press on the ground works again.</summary>
        [Test]
        public void MouseClick_OnTheGround_AfterAPressOnTheUi_StillSelects()
        {
            Vector2 onUi = ScreenOf(new Vector3(1.5f, 0f, 1.5f));
            Vector2 onTile = ScreenOf(new Vector3(5.5f, 0f, 6.5f));
            CityInputReader reader = ReaderWithUiAt(onUi);
            Set(_mouse.position, onUi);
            Press(_mouse.leftButton);
            reader.Tick();
            Release(_mouse.leftButton);
            reader.Tick();

            Set(_mouse.position, onTile);
            Press(_mouse.leftButton);
            reader.Tick();
            Release(_mouse.leftButton);
            reader.Tick();

            Assert.That(_world.Selection!.Selected, Is.EqualTo(new GridCoordinate(5, 6)));
        }

        /// <summary>A drag that starts on the ground keeps panning when the pointer moves over the UI.</summary>
        [Test]
        public void MouseDrag_StartedOnTheGround_ContinuesOverTheUi()
        {
            _world.Rig!.Zoom(0.5f);
            Vector2 start = ScreenOf(new Vector3(4f, 0f, 4f));
            CityInputReader reader = ReaderWithUiAt(start + new Vector2(80f, 0f));
            Vector2 focusBefore = _world.Rig.Model.Focus;

            Set(_mouse.position, start);
            Press(_mouse.leftButton);
            reader.Tick();
            Set(_mouse.position, start + new Vector2(80f, 0f));
            reader.Tick();
            Release(_mouse.leftButton);
            reader.Tick();

            Assert.That(_world.Rig.Model.Focus, Is.Not.EqualTo(focusBefore));
        }

        /// <summary>The wheel over the UI does not zoom the city, and the pointer over the UI marks no tile.</summary>
        [Test]
        public void MouseWheelAndHover_OverTheUi_DoNothingToTheGround()
        {
            Vector2 onUi = ScreenOf(new Vector3(4f, 0f, 4f));
            CityInputReader reader = ReaderWithUiAt(onUi);
            float start = _world.Rig!.Model.Distance;

            Set(_mouse.position, onUi);
            Set(_mouse.scroll, new Vector2(0f, 120f));
            reader.Tick();

            Assert.That(_world.Rig.Model.Distance, Is.EqualTo(start));
            Assert.That(_world.Selection!.Hovered, Is.Null);
        }

        /// <summary>A finger tap on the UI selects no tile.</summary>
        [Test]
        public void FingerTap_OverTheUi_SelectsNothing()
        {
            Vector2 onUi = ScreenOf(new Vector3(2.5f, 0f, 3.5f));
            CityInputReader reader = ReaderWithUiAt(onUi);

            BeginTouch(1, onUi);
            reader.Tick();
            EndTouch(1, onUi);
            reader.Tick();

            Assert.That(_world.Selection!.Selected, Is.Null);
        }

        /// <summary>Where a world point shows on the screen, in pixels.</summary>
        private Vector2 ScreenOf(Vector3 worldPoint)
        {
            Vector3 screen = _world.Camera.WorldToScreenPoint(worldPoint);
            return new Vector2(screen.x, screen.y);
        }
    }
}
