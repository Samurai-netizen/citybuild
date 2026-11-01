#nullable enable

using System;
using CityBuilder.Client.Presentation.Ui;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The status of one building drawn above it (M0-50): a progress ring, a bubble that names why the building makes no progress, and a row of
    /// worker pips, on an object under the <see cref="BuildingView"/> that stands at the model's <c>Anchor_Status</c> (or at a fixed height over
    /// a building drawn from primitives) and faces the camera. A thin component: <see cref="BuildingStatusModel"/> decides what to show,
    /// <see cref="StatusArt"/> owns the pictures, and this only places them. <see cref="Apply"/> draws only what differs from the last call and
    /// allocates nothing then; the parts are made the first time they are needed. It reads nothing from the server and decides nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingStatusView : MonoBehaviour
    {
        /// <summary>The name of the object that holds the status.</summary>
        public const string RootName = "Status";

        /// <summary>How high above the ring's centre the bubble stands.</summary>
        public const float BubbleHeight = 0.62f;

        /// <summary>How far below the ring's centre the pips stand.</summary>
        public const float PipsHeight = -0.36f;

        /// <summary>The height, over the ground, of a building drawn from primitives at full height, where its status stands (a model has an anchor).</summary>
        public const float FallbackAnchorHeight = 1.35f;

        /// <summary>How wide the bubble is.</summary>
        private const float BubbleSize = 0.50f;

        /// <summary>How wide a pip is.</summary>
        private const float PipSize = 0.13f;

        /// <summary>How far apart the pips' centres are.</summary>
        private const float PipSpacing = 0.16f;

        /// <summary>The building this shows the status of.</summary>
        private BuildingView _owner = null!;

        /// <summary>The shared pictures.</summary>
        private StatusArt _art = null!;

        /// <summary>The camera the status faces, or null to leave it as it is.</summary>
        private Transform? _facing;

        /// <summary>The anchor the status was placed at, to place it again when a new body brings a new anchor.</summary>
        private Transform? _placedAt;

        /// <summary>Whether the status was placed at all.</summary>
        private bool _placed;

        /// <summary>The last status drawn.</summary>
        private BuildingStatus _shown;

        /// <summary>Whether anything was drawn yet.</summary>
        private bool _hasShown;

        /// <summary>The object that holds the ring's parts, or null before a ring is first needed.</summary>
        private GameObject? _ringGroup;

        /// <summary>The filled part of the ring.</summary>
        private MeshFilter? _fillFilter;

        /// <summary>Draws the filled part of the ring.</summary>
        private MeshRenderer? _fillRenderer;

        /// <summary>The object that holds the bubble's parts, or null before a bubble is first needed.</summary>
        private GameObject? _bubbleGroup;

        /// <summary>The coloured rim of the bubble.</summary>
        private SpriteRenderer? _rim;

        /// <summary>The picture in the bubble.</summary>
        private SpriteRenderer? _icon;

        /// <summary>The strike across the picture of a missing input.</summary>
        private SpriteRenderer? _strike;

        /// <summary>The pips made so far, up to <see cref="BuildingStatus.MaxPips"/>; the ones past <see cref="PipCount"/> are hidden.</summary>
        private readonly SpriteRenderer?[] _pips = new SpriteRenderer?[BuildingStatus.MaxPips];

        /// <summary>The last status that was drawn, or the empty one before the first call to <see cref="Apply"/>.</summary>
        public BuildingStatus Shown => _shown;

        /// <summary>The step the ring is filled to, 0 to <see cref="StatusArt.RingSteps"/>, or −1 when no ring is shown.</summary>
        public int RingStep { get; private set; } = -1;

        /// <summary>Whether a bubble is shown.</summary>
        public bool BubbleVisible => _bubbleGroup != null && _bubbleGroup.activeSelf;

        /// <summary>The rim colour of the shown bubble; clear when none is shown.</summary>
        public Color BubbleColor => BubbleVisible && _rim != null ? _rim.color : Color.clear;

        /// <summary>The picture in the shown bubble, or null when there is none (no bubble, or no icon for it).</summary>
        public Sprite? BubbleIcon => BubbleVisible && _icon != null && _icon.enabled ? _icon.sprite : null;

        /// <summary>Whether the shown bubble has the strike of a missing input across it.</summary>
        public bool BubbleStruck => BubbleVisible && _strike != null && _strike.enabled;

        /// <summary>How many pips are shown.</summary>
        public int PipCount { get; private set; }

        /// <summary>
        /// Makes the status object of a building, under its view, and stores it in <see cref="BuildingView.Status"/>. Nothing shows until
        /// <see cref="Apply"/> is called with something to show.
        /// </summary>
        /// <param name="owner">The view of the building.</param>
        /// <param name="art">The pictures every status shares.</param>
        /// <param name="facing">The camera the status turns to face, or null.</param>
        /// <returns>The new component.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="owner"/> or <paramref name="art"/> is null.</exception>
        internal static BuildingStatusView Create(BuildingView owner, StatusArt art, Transform? facing)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (art == null)
            {
                throw new ArgumentNullException(nameof(art));
            }

            var root = new GameObject(RootName);
            root.transform.SetParent(owner.transform, false);
            var view = root.AddComponent<BuildingStatusView>();
            view._owner = owner;
            view._art = art;
            view._facing = facing;
            owner.Status = view;
            return view;
        }

        /// <summary>
        /// Draws <paramref name="status"/>: moves the object to the building's anchor (when the body changed) and turns it to the camera, then
        /// changes the ring, the bubble and the pips that differ from the last call. The same status again changes nothing and allocates nothing.
        /// </summary>
        /// <param name="status">What to show.</param>
        internal void Apply(BuildingStatus status)
        {
            Place();
            if (_hasShown && status == _shown)
            {
                return;
            }

            _hasShown = true;
            _shown = status;
            ShowRing(status);
            ShowBubble(status);
            ShowPips(status.Pips);
        }

        /// <summary>Puts the object at the building's status anchor (or the fixed height without one) and turns it to the camera.</summary>
        private void Place()
        {
            Transform? anchor = _owner.StatusAnchor;

            // Reference equality: a destroyed anchor still compares equal to null under Unity's operator, which would hide a switch from a model to primitives.
            if (!_placed || !ReferenceEquals(anchor, _placedAt))
            {
                _placed = true;
                _placedAt = anchor;
                float height = FallbackAnchorHeight * _owner.HeightPermille / BuildingLooks.FullHeightPermille;
                transform.localPosition = anchor != null ? _owner.transform.InverseTransformPoint(anchor.position) : new Vector3(0f, height, 0f);
            }

            if (_facing != null)
            {
                transform.rotation = _facing.rotation;
            }
        }

        /// <summary>Shows the ring of <paramref name="status"/>, or hides it.</summary>
        private void ShowRing(BuildingStatus status)
        {
            if (status.Ring == StatusRingKind.None)
            {
                if (_ringGroup != null)
                {
                    _ringGroup.SetActive(false);
                }

                RingStep = -1;
                return;
            }

            MakeRing();
            _ringGroup!.SetActive(true);
            int step = StatusArt.StepOf(status.ProgressPermille);
            _fillFilter!.sharedMesh = _art.RingMesh(step);
            _fillRenderer!.sharedMaterial = _art.FillMaterial(status.Ring);
            _fillRenderer.enabled = step > 0;
            RingStep = step;
        }

        /// <summary>Shows the bubble of <paramref name="status"/> with its rim colour and picture, or hides it.</summary>
        private void ShowBubble(BuildingStatus status)
        {
            if (status.Bubble == StatusBubbleKind.None)
            {
                if (_bubbleGroup != null)
                {
                    _bubbleGroup.SetActive(false);
                }

                return;
            }

            MakeBubble();
            _bubbleGroup!.SetActive(true);
            _rim!.color = StatusArt.BubbleColor(status.Bubble);
            Texture2D? icon = status.Bubble == StatusBubbleKind.NoWorkers ? UiIcons.Workers : UiIcons.ForItem(status.BubbleItem);
            _icon!.sprite = icon == null ? null : _art.IconSprite(icon);
            _icon.enabled = icon != null;
            _strike!.enabled = status.Bubble == StatusBubbleKind.MissingInput;
        }

        /// <summary>Shows <paramref name="count"/> pips in a centred row under the ring and hides the rest.</summary>
        private void ShowPips(int count)
        {
            for (int i = 0; i < _pips.Length; i++)
            {
                if (i >= count)
                {
                    if (_pips[i] != null)
                    {
                        _pips[i]!.enabled = false;
                    }

                    continue;
                }

                SpriteRenderer pip = _pips[i] ?? MakePip(i);
                pip.transform.localPosition = new Vector3((i - (count - 1) / 2f) * PipSpacing, PipsHeight, -0.004f);
                pip.enabled = true;
            }

            PipCount = count;
        }

        /// <summary>Makes the track and the fill of the ring, once.</summary>
        private void MakeRing()
        {
            if (_ringGroup != null)
            {
                return;
            }

            _ringGroup = new GameObject("Ring");
            _ringGroup.transform.SetParent(transform, false);
            AddMesh(_ringGroup.transform, "Track", _art.RingMesh(StatusArt.RingSteps), _art.TrackMaterial, 0, 0f, out _);
            _fillRenderer = AddMesh(_ringGroup.transform, "Fill", _art.RingMesh(0), _art.TrackMaterial, 1, -0.002f, out _fillFilter);
        }

        /// <summary>Makes the rim, face, picture and strike of the bubble, once.</summary>
        private void MakeBubble()
        {
            if (_bubbleGroup != null)
            {
                return;
            }

            _bubbleGroup = new GameObject("Bubble");
            _bubbleGroup.transform.SetParent(transform, false);
            _bubbleGroup.transform.localPosition = new Vector3(0f, BubbleHeight, 0f);
            _rim = AddSprite(_bubbleGroup.transform, "Rim", _art.Disc, Color.white, BubbleSize, 2, 0f);
            AddSprite(_bubbleGroup.transform, "Face", _art.Disc, StatusArt.BubbleFaceColor, BubbleSize * 0.82f, 3, -0.002f);
            _icon = AddSprite(_bubbleGroup.transform, "Icon", null, Color.white, 1f, 4, -0.004f);
            _strike = AddSprite(_bubbleGroup.transform, "Strike", _art.Square, StatusArt.BubbleColor(StatusBubbleKind.MissingInput), 1f, 5, -0.006f);
            _strike.transform.localScale = new Vector3(BubbleSize * 0.85f, 0.045f, 1f);
            _strike.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        /// <summary>Makes pip number <paramref name="index"/>, hidden.</summary>
        private SpriteRenderer MakePip(int index)
        {
            SpriteRenderer pip = AddSprite(transform, "Pip " + (index + 1), _art.OutlinedDisc, StatusArt.PipColor, PipSize, 2, -0.004f);
            _pips[index] = pip;
            return pip;
        }

        /// <summary>Adds a child with a mesh and a renderer that casts and takes no shadow, at depth <paramref name="z"/> and sorted at <paramref name="order"/>.</summary>
        private static MeshRenderer AddMesh(Transform parent, string name, Mesh mesh, Material material, int order, float z, out MeshFilter filter)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = new Vector3(0f, 0f, z);
            filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return renderer;
        }

        /// <summary>Adds a child with a sprite renderer of the given size (a scale), colour, sort order and depth.</summary>
        private static SpriteRenderer AddSprite(Transform parent, string name, Sprite? sprite, Color color, float size, int order, float z)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = new Vector3(0f, 0f, z);
            child.transform.localScale = new Vector3(size, size, 1f);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
