#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Simulation.State;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Builds and refreshes the objects that stand for buildings (M0-44): the placeholder look for a building's type and the city's era
    /// (<see cref="BuildingLooks"/>), made from primitives with no collider, stretched upward for the level and the phase. It keeps one material
    /// per colour for all buildings and destroys them in <see cref="Destroy"/>. Display only: it reads a <see cref="Building"/> and never changes it.
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
        /// Brings a view up to date with its building: a new body when the type or the era changed (or there is none yet), and a new height
        /// when the level or the phase changed. Does nothing, and allocates nothing, when the view already shows exactly this.
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
            if (view.Body == null || view.Era != era || !string.Equals(view.TypeId, typeId, StringComparison.Ordinal))
            {
                BuildBody(view, BuildingLooks.Get(typeId, era));
                view.TypeId = typeId;
                view.Era = era;
                changed = true;
            }

            int height = BuildingLooks.HeightPermille(building.Level, building.Phase);
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

        /// <summary>Replaces the view's body with the primitives of <paramref name="look"/>.</summary>
        private void BuildBody(BuildingView view, BuildingLook look)
        {
            if (view.Body != null)
            {
                // Out of the tree at once, so a delayed destroy leaves no second body beside the new one this frame.
                view.Body.SetParent(null, false);
                PlaceholderArt.Destroy(view.Body.gameObject);
            }

            var body = new GameObject(BodyName);
            body.transform.SetParent(view.transform, false);
            for (int i = 0; i < look.Parts.Count; i++)
            {
                CreatePart(body.transform, look.Parts[i]);
            }

            view.Body = body.transform;
            view.Look = look;
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
