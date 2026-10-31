#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Reads the Input System's devices once per frame and tells a <see cref="CityCameraRig"/> what the player did: one finger or the left
    /// mouse button drags to pan, or taps to select; two fingers pinch to zoom; the mouse wheel zooms; the mouse pointer hovers. With no
    /// device present (a test, a server build) it does nothing. Touch wins over the mouse in a frame that has both.
    /// </summary>
    public sealed class CityInputReader
    {
        /// <summary>The factor one wheel step zooms by (below 1: the camera comes closer on a scroll up). The wheel's size differs between devices, so every scroll event is one step.</summary>
        private const float ScrollZoomStep = 0.9f;

        /// <summary>A press may move this many inches and still be a tap; about the touch slop of a phone.</summary>
        private const float TapSlopInches = 0.06f;

        /// <summary>The smallest tap slop in pixels, for screens that report no or a low DPI.</summary>
        private const float MinTapSlopPixels = 8f;

        /// <summary>The DPI assumed when the screen reports none.</summary>
        private const float FallbackDpi = 160f;

        /// <summary>The rig the actions go to.</summary>
        private readonly CityCameraRig _rig;

        /// <summary>Tap or drag for the first finger.</summary>
        private readonly PointerGesture _touch;

        /// <summary>Tap or drag for the left mouse button.</summary>
        private readonly PointerGesture _mouse;

        /// <summary>Zoom factor for two fingers.</summary>
        private readonly PinchGesture _pinch = new PinchGesture();

        /// <summary>Where the single finger was in the last frame it was down: a finger that went up has no position of its own to read.</summary>
        private Vector2 _lastFingerPosition;

        /// <summary>True from the moment a second finger came down until every finger is up, so the finger left after a pinch does not tap.</summary>
        private bool _ignoreTouchUntilAllUp;

        /// <summary>Creates a reader that talks to <paramref name="rig"/>.</summary>
        /// <param name="rig">The rig that moves the camera and changes the selection.</param>
        /// <param name="screenDpi">The screen's DPI (<c>Screen.dpi</c>, 0 when unknown); it sets how far a press may move and still be a tap.</param>
        public CityInputReader(CityCameraRig rig, float screenDpi)
        {
            _rig = rig;
            float slop = Mathf.Max(MinTapSlopPixels, (screenDpi > 0f ? screenDpi : FallbackDpi) * TapSlopInches);
            _touch = new PointerGesture(slop);
            _mouse = new PointerGesture(slop);
        }

        /// <summary>Reads the devices and passes what happened this frame to the rig. Call it once per frame; it allocates nothing.</summary>
        public void Tick()
        {
            if (!ReadTouch())
            {
                ReadMouse();
            }
        }

        /// <summary>
        /// Handles the touch screen. Returns true when a finger is on it (the mouse is then not read), false when none is or there is no touch screen.
        /// </summary>
        private bool ReadTouch()
        {
            Touchscreen? screen = Touchscreen.current;
            if (screen == null)
            {
                return false;
            }

            int fingers = 0;
            Vector2 first = default;
            Vector2 second = default;
            for (int i = 0; i < screen.touches.Count; i++)
            {
                if (!screen.touches[i].isInProgress)
                {
                    continue;
                }

                if (fingers == 0)
                {
                    first = screen.touches[i].position.ReadValue();
                }
                else if (fingers == 1)
                {
                    second = screen.touches[i].position.ReadValue();
                }

                fingers++;
            }

            if (fingers == 0)
            {
                // A finger that went up this frame still needs its release read by the gesture, at the place it was last seen.
                _pinch.End();
                if (_touch.IsDown && !_ignoreTouchUntilAllUp)
                {
                    FollowPointer(_touch, false, _lastFingerPosition);
                }

                _touch.Cancel();
                _ignoreTouchUntilAllUp = false;
                return false;
            }

            _rig.HoverAt(null);
            if (fingers >= 2)
            {
                _ignoreTouchUntilAllUp = true;
                _touch.Cancel();
                _rig.Zoom(_pinch.Update(first, second));
                return true;
            }

            _pinch.End();
            _lastFingerPosition = first;
            if (!_ignoreTouchUntilAllUp)
            {
                FollowPointer(_touch, true, first);
            }

            return true;
        }

        /// <summary>Handles the mouse: the wheel zooms, the pointer hovers, the left button drags or taps.</summary>
        private void ReadMouse()
        {
            Mouse? mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            float wheel = mouse.scroll.ReadValue().y;
            if (wheel > 0f)
            {
                _rig.Zoom(ScrollZoomStep);
            }
            else if (wheel < 0f)
            {
                _rig.Zoom(1f / ScrollZoomStep);
            }

            Vector2 position = mouse.position.ReadValue();
            FollowPointer(_mouse, mouse.leftButton.isPressed, position);
            _rig.HoverAt(_mouse.IsDragging ? (Vector2?)null : position);
        }

        /// <summary>
        /// Feeds one pointer's state to its gesture and the result to the rig: a press starts the gesture, movement past the slop pans, and a
        /// release that never moved that far taps. <paramref name="down"/> is whether the pointer is pressed now; <paramref name="position"/> where it is.
        /// </summary>
        private void FollowPointer(PointerGesture gesture, bool down, Vector2 position)
        {
            if (down)
            {
                if (!gesture.IsDown)
                {
                    gesture.Press(position);
                    return;
                }

                Vector2 moved = gesture.Move(position);
                if (moved != Vector2.zero)
                {
                    _rig.DragBy(position - moved, position);
                }
            }
            else if (gesture.IsDown && gesture.Release())
            {
                _rig.TapAt(position);
            }
        }
    }
}
