#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Simulation.State;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Builds and refreshes the objects that stand for buildings (M0-44, CR-0005): an instance of the model for a building's type, the city's era
    /// and its level (<see cref="BuildingLooks.FindModel"/>), or of the construction site while it is built or upgraded, with the anchors found
    /// by name. A level with no model falls back to the placeholder look of its type from primitives with no collider, stretched upward for the
    /// level and the phase, and an unknown type to the grey box. Models share the art's one material; the primitives get one material per colour,
    /// which the factory destroys in <see cref="Destroy"/>. Display only: it reads a <see cref="Building"/> and never changes it.
    /// </summary>
    public sealed class BuildingViewFactory
    {
        /// <summary>The name of the child object that holds a building's primitives.</summary>
        public const string BodyName = "Body";

        /// <summary>The materials this factory created, one per colour, destroyed with it.</summary>
        private readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();

        /// <summary>The grid the buildings stand on.</summary>
        private readonly CityGrid _grid;

        /// <summary>Whether <see cref="Destroy"/> has run.</summary>
        private bool _destroyed;

        /// <summary>Creates a factory for buildings on <paramref name="grid"/>.</summary>
        /// <param name="grid">The grid whose tiles the buildings stand on.</param>
        /// <exception cref="ArgumentNullException"><paramref name="grid"/> is null.</exception>
        public BuildingViewFactory(CityGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        /// <summary>
        /// Creates the object of one building under <paramref name="parent"/>, on its tile, with the look for its type and
        /// <paramref name="era"/>, drawn for its level and phase.
        /// </summary>
        /// <param name="building">The building to show.</param>
        /// <param name="era">The city's era, which picks the look.</param>
        /// <param name="parent">The transform the object hangs under.</param>
        /// <returns>The new view.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public BuildingView Create(Building building, int era, Transform parent)
        {
            if (building == null)
            {
                throw new ArgumentNullException(nameof(building));
            }

            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var root = new GameObject("Building " + building.Id + " " + building.TypeId.Value);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = _grid.ToWorldCenter(new GridCoordinate(building.X, building.Y));
            var view = root.AddComponent<BuildingView>();
            view.BuildingId = building.Id;
            Refresh(view, building, era);
            return view;
        }

        /// <summary>
        /// Brings a view up to date with its building: a new body when the type, the era, the level or the construction-site state changed (or
        /// there is none yet) and the model or look it needs is not the one drawn, and a new height for a body of primitives when the level or the
        /// phase changed. Does nothing, and allocates nothing, when the view already shows exactly this.
        /// </summary>
        /// <param name="view">The view of <paramref name="building"/>.</param>
        /// <param name="building">The building as it is now.</param>
        /// <param name="era">The city's era.</param>
        /// <returns>True when the view changed.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public bool Refresh(BuildingView view, Building building, int era)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (building == null)
            {
                throw new ArgumentNullException(nameof(building));
            }

            bool changed = false;
            string typeId = building.TypeId.Value;
            bool site = BuildingLooks.ShowsConstructionSite(building.Phase);
            if (view.Body == null
                || view.Era != era
                || view.Level != building.Level
                || BuildingLooks.ShowsConstructionSite(view.Phase) != site
                || !string.Equals(view.TypeId, typeId, StringComparison.Ordinal))
            {
                GameObject? model = site ? BuildingLooks.FindConstructionSite(era) : BuildingLooks.FindModel(typeId, era, building.Level);
                BuildingLook? look = model == null ? BuildingLooks.Get(typeId, era) : null;
                if (view.Body == null || view.Model != model || view.Look != look)
                {
                    BuildBody(view, model, look);
                }

                view.TypeId = typeId;
                view.Era = era;
                changed = true;
            }

            int height = view.Model != null ? BuildingLooks.FullHeightPermille : BuildingLooks.HeightPermille(building.Level, building.Phase);
            if (changed || view.HeightPermille != height)
            {
                view.Body!.localScale = new Vector3(1f, height / (float)BuildingLooks.FullHeightPermille, 1f);
                view.HeightPermille = height;
                changed = true;
            }

            if (view.Level != building.Level || view.Phase != building.Phase)
            {
                view.Level = building.Level;
                view.Phase = building.Phase;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// The first object called <paramref name="name"/> at or below <paramref name="root"/>, depth first, or null. How a view finds the anchors
        /// and the wheel of a model. Walks the hierarchy without allocating; call it when a body is built, not every frame.
        /// </summary>
        /// <param name="root">Where to start looking.</param>
        /// <param name="name">The exact object name.</param>
        /// <returns>The transform, or null when there is none.</returns>
        public static Transform? FindByName(Transform root, string name)
        {
            if (string.Equals(root.name, name, StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform? found = FindByName(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>Destroys every material this factory created. The views are the caller's to destroy. Safe to call twice.</summary>
        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;
            foreach (Material material in _materials.Values)
            {
                PlaceholderArt.Destroy(material);
            }

            _materials.Clear();
        }

        /// <summary>
        /// Replaces the view's body with an instance of <paramref name="model"/>, or, when there is none, with the primitives of
        /// <paramref name="look"/>, and finds the model's anchors by name.
        /// </summary>
        private void BuildBody(BuildingView view, GameObject? model, BuildingLook? look)
        {
            if (view.Body != null)
            {
                // Out of the tree at once, so a delayed destroy leaves no second body beside the new one this frame.
                view.Body.SetParent(null, false);
                PlaceholderArt.Destroy(view.Body.gameObject);
            }

            GameObject body;
            if (model != null)
            {
                body = Object.Instantiate(model, view.transform, false);
                body.name = BodyName;
            }
            else
            {
                body = new GameObject(BodyName);
                body.transform.SetParent(view.transform, false);
                for (int i = 0; i < look!.Parts.Count; i++)
                {
                    CreatePart(body.transform, look.Parts[i]);
                }
            }

            view.Body = body.transform;
            view.Model = model;
            view.Look = look;
            view.StatusAnchor = model == null ? null : FindByName(body.transform, BuildingView.StatusAnchorName);
            view.DoorAnchor = model == null ? null : FindByName(body.transform, BuildingView.DoorAnchorName);
            view.SmokeAnchor = model == null ? null : FindByName(body.transform, BuildingView.SmokeAnchorName);
            view.StockpileAnchor = model == null ? null : FindByName(body.transform, BuildingView.StockpileAnchorName);
            view.Wheel = model == null ? null : FindByName(body.transform, BuildingView.WheelName);
        }

        /// <summary>Creates the primitive of one part under <paramref name="body"/>, without a collider and with the part's colour.</summary>
        private void CreatePart(Transform body, BuildingPart part)
        {
            CreatePrimitive(body, part, MaterialFor(part.Color));
        }

        /// <summary>
        /// Creates the primitive of one part under <paramref name="parent"/>, without a collider, drawn with <paramref name="material"/>. Shared
        /// with <see cref="PlacementGhosts"/>, which draws the same look in a translucent material.
        /// </summary>
        internal static void CreatePrimitive(Transform parent, BuildingPart part, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(part.Shape);
            PlaceholderArt.Destroy(primitive.GetComponent<Collider>());
            primitive.name = part.Shape.ToString();
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = part.Center;
            primitive.transform.localRotation = Quaternion.Euler(part.EulerAngles);

            // Unity's cylinder is 2 units long, its cube 1: the part's size is the real extent.
            float lengthScale = part.Shape == PrimitiveType.Cylinder ? 0.5f : 1f;
            primitive.transform.localScale = new Vector3(part.Size.x, part.Size.y * lengthScale, part.Size.z);
            primitive.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>The factory's material of one colour, created on first use.</summary>
        private Material MaterialFor(Color color)
        {
            if (!_materials.TryGetValue(color, out Material? material))
            {
                material = PlaceholderArt.CreateMaterial(color);
                _materials.Add(color, material);
            }

            return material;
        }
    }
}
