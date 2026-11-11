#nullable enable

using System;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// One walker on the map (CR-0008-T2 part D): a courier or a trip worker made from the common model, with its <see cref="MotionRig"/> (arms
    /// and legs wave while it walks) and a load at its <c>Anchor_Load</c>. Views are pooled by <see cref="CityWalkers"/>; every method here
    /// allocates nothing unless the load changes.
    /// </summary>
    public sealed class WalkerView
    {
        /// <summary>The name of the model's anchor where a carried load sits.</summary>
        public const string LoadAnchorName = "Anchor_Load";

        /// <summary>Where a load is held when the model has no <see cref="LoadAnchorName"/>, relative to the walker's feet.</summary>
        private static readonly Vector3 FallbackLoadPosition = new Vector3(0f, 0.14f, -0.05f);

        /// <summary>The root object, under the city; moved and turned by <see cref="Place"/>.</summary>
        private readonly GameObject _root;

        /// <summary>The leg and arm motion, or null when the model has none.</summary>
        private readonly MotionRig? _rig;

        /// <summary>Where a load sits: the model's anchor, or a point at the walker's back when the model has none.</summary>
        private Transform? _loadAnchor;

        /// <summary>The load object now shown, or null.</summary>
        private GameObject? _load;

        /// <summary>The item the load shows, or null when there is none.</summary>
        private string? _loadItem;

        /// <summary>Whether the rig has been put back at rest since the walker last moved.</summary>
        private bool _atRest = true;

        /// <summary>Creates a view from a model (or a capsule when the art has none).</summary>
        /// <param name="name">The object's name.</param>
        /// <param name="model">The model to instantiate; null makes a small capsule.</param>
        /// <param name="parent">The transform the walker hangs under.</param>
        public WalkerView(string name, GameObject? model, Transform parent)
        {
            _root = new GameObject(name);
            _root.transform.SetParent(parent, false);
            if (model != null)
            {
                GameObject body = UnityEngine.Object.Instantiate(model, _root.transform, false);
                body.name = "Body";
                _rig = MotionRig.Find(body.transform);
                _loadAnchor = BuildingViewFactory.FindByName(body.transform, LoadAnchorName);
                if (_loadAnchor == null)
                {
                    // The worker model has no carrying frame (only the courier does): its load is held at its back.
                    var carried = new GameObject(LoadAnchorName);
                    carried.transform.SetParent(_root.transform, false);
                    carried.transform.localPosition = FallbackLoadPosition;
                    _loadAnchor = carried.transform;
                }
            }
            else
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                PlaceholderArt.Destroy(body.GetComponent<Collider>());
                body.name = "Body";
                body.transform.SetParent(_root.transform, false);
                body.transform.localScale = new Vector3(0.18f, 0.12f, 0.18f);
                body.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                var carried = new GameObject(LoadAnchorName);
                carried.transform.SetParent(_root.transform, false);
                carried.transform.localPosition = FallbackLoadPosition;
                _loadAnchor = carried.transform;
            }
        }

        /// <summary>The root object, for tests.</summary>
        public GameObject Root => _root;

        /// <summary>The item the shown load is of, or null when none is shown.</summary>
        public string? LoadItem => _loadItem;

        /// <summary>Whether a load object is shown.</summary>
        public bool HasLoad => _load != null;

        /// <summary>Whether the model has leg and arm motion.</summary>
        public bool HasRig => _rig != null;

        /// <summary>Shows or hides the walker.</summary>
        /// <param name="active">True to show.</param>
        public void SetActive(bool active)
        {
            if (_root.activeSelf != active)
            {
                _root.SetActive(active);
            }
        }

        /// <summary>Puts the walker on the ground at a place of the grid and turns it along a step; a zero direction keeps its facing.</summary>
        /// <param name="x">The column in world units (tile centre at +0.5).</param>
        /// <param name="z">The row in world units.</param>
        /// <param name="directionX">The step's direction in columns, -1, 0 or 1.</param>
        /// <param name="directionZ">The step's direction in rows, -1, 0 or 1.</param>
        public void Place(float x, float z, int directionX, int directionZ)
        {
            Transform t = _root.transform;
            t.localPosition = new Vector3(x * CityGrid.TileSize, 0f, z * CityGrid.TileSize);
            if (directionX != 0 || directionZ != 0)
            {
                t.localRotation = Quaternion.Euler(0f, Mathf.Atan2(directionX, directionZ) * Mathf.Rad2Deg, 0f);
            }
        }

        /// <summary>Moves the legs and arms on while the walker walks.</summary>
        /// <param name="deltaSeconds">The frame time.</param>
        public void Walk(float deltaSeconds)
        {
            _atRest = false;
            _rig?.Advance(deltaSeconds, 1f);
        }

        /// <summary>Puts the legs and arms at rest, once, when the walker stands.</summary>
        public void Stand()
        {
            if (_atRest)
            {
                return;
            }

            _atRest = true;
            _rig?.Rest();
        }

        /// <summary>
        /// Shows the load of an item, or none for null. Does nothing when the item is the one shown. The object is made from <paramref name="loadModel"/>
        /// or, when the art has none for the item, a small cube.
        /// </summary>
        /// <param name="item">The carried item's id, or null for no load.</param>
        /// <param name="loadModel">The item's carry model, or null for the cube.</param>
        /// <param name="fallbackMaterial">The material of the cube.</param>
        public void SetLoad(string? item, GameObject? loadModel, Material? fallbackMaterial)
        {
            if (string.Equals(_loadItem, item, StringComparison.Ordinal))
            {
                return;
            }

            if (_load != null)
            {
                _load.transform.SetParent(null, false);
                PlaceholderArt.Destroy(_load);
                _load = null;
            }

            _loadItem = item;
            if (item == null || _loadAnchor == null)
            {
                return;
            }

            if (loadModel != null)
            {
                _load = UnityEngine.Object.Instantiate(loadModel, _loadAnchor, false);
            }
            else
            {
                _load = GameObject.CreatePrimitive(PrimitiveType.Cube);
                PlaceholderArt.Destroy(_load.GetComponent<Collider>());
                _load.transform.SetParent(_loadAnchor, false);
                _load.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
                if (fallbackMaterial != null)
                {
                    _load.GetComponent<Renderer>().sharedMaterial = fallbackMaterial;
                }
            }

            _load.name = "Load";
        }

        /// <summary>Destroys the walker's objects.</summary>
        public void Destroy()
        {
            _root.transform.SetParent(null, false);
            PlaceholderArt.Destroy(_root);
        }
    }
}
