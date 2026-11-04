#nullable enable

using System;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Dresses a building's view with the extras of visual v2 (CR-0009): when a body is built, the animator that moves its parts and its
    /// smoke (<see cref="BuildingAnimator"/>) and its store fill (<see cref="FillView"/>, from the level's fill model
    /// <c>Models/era&lt;N&gt;/&lt;typeId&gt;_l&lt;level&gt;_fill</c>); once a second, whether the building works and how full its store looks,
    /// from the displayed city. Display only: it reads the city and the content and never changes them. Like <see cref="CityView"/>, a call
    /// to <see cref="Show"/> that changes nothing allocates nothing.
    /// </summary>
    public static class BuildingDecor
    {
        /// <summary>The name of the fill model's instance under a body.</summary>
        public const string FillName = "Fill";

        /// <summary>What a fill model's name adds to its building model's name.</summary>
        public const string FillSuffix = "_fill";

        /// <summary>The path, under <c>Resources</c>, of the smoke puff model.</summary>
        public const string SmokePuffPath = BuildingLooks.ModelsFolder + "/common/fx_smoke_puff";

        /// <summary>The earliest era a fill model can be filed under; the lookup stops there.</summary>
        private const int FirstEra = 1;

        /// <summary>The path, under <c>Resources</c>, of the fill model of a building type at a level in one era: <c>Models/era1/sawmill_l1_fill</c>.</summary>
        /// <param name="typeId">The building type's id in the content.</param>
        /// <param name="era">The era whose folder holds the model.</param>
        /// <param name="level">The level, at least 1.</param>
        /// <returns>The path, without an extension.</returns>
        public static string FillPath(string typeId, int era, int level)
        {
            return BuildingLooks.ModelPath(typeId, era, level) + FillSuffix;
        }

        /// <summary>
        /// The fill model of a building type at a level: the one filed under <paramref name="era"/>, or else under the latest earlier era, like
        /// <see cref="BuildingLooks.FindModel"/>. Null for a building that keeps no goods (a cottage) or a level the art has none for.
        /// </summary>
        /// <param name="typeId">The building type's id in the content.</param>
        /// <param name="era">The era to draw it in, at least 1.</param>
        /// <param name="level">The level, at least 1.</param>
        /// <returns>The model, or null.</returns>
        public static GameObject? FindFill(string typeId, int era, int level)
        {
            for (int candidate = era; candidate >= FirstEra; candidate--)
            {
                GameObject? model = Resources.Load<GameObject>(FillPath(typeId, candidate, level));
                if (model != null)
                {
                    return model;
                }
            }

            return null;
        }

        /// <summary>
        /// Gives a view whose body was just built its moving parts, its smoke and its store fill. A body of primitives (no model) gets none.
        /// The fill shows nothing until the next <see cref="Show"/>.
        /// </summary>
        /// <param name="view">The view; its body, model and anchors are the new ones.</param>
        /// <param name="fillModel">The fill model of the building's level, or null (none, or the construction site is shown).</param>
        /// <exception cref="ArgumentNullException"><paramref name="view"/> is null.</exception>
        public static void Attach(BuildingView view, GameObject? fillModel)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            view.Fill = null;
            Transform? body = view.Body;
            if (body == null || view.Model == null)
            {
                view.Animator?.Bind(null, null);
                return;
            }

            if (fillModel != null)
            {
                GameObject fill = Object.Instantiate(fillModel, body, false);
                fill.name = FillName;
                view.Fill = new FillView(fill.transform);
            }

            MotionRig? rig = MotionRig.Find(body);
            SmokePuffs? smoke = null;
            if (view.SmokeAnchor != null)
            {
                GameObject? puff = Resources.Load<GameObject>(SmokePuffPath);
                if (puff != null)
                {
                    smoke = new SmokePuffs(puff, view.SmokeAnchor);
                }
            }

            if (rig == null && smoke == null)
            {
                view.Animator?.Bind(null, null);
                return;
            }

            if (view.Animator == null)
            {
                view.Animator = view.gameObject.AddComponent<BuildingAnimator>();
            }

            view.Animator.Bind(rig, smoke);
        }

        /// <summary>
        /// Shows whether the building works and how full its store is. The store of each item the fill model has steps for is today the city's
        /// stock of the item against its storage cap (an item without a cap shows nothing); CR-0008 moves goods into the buildings, and then this
        /// reads the building's own store.
        /// </summary>
        /// <param name="view">The building's view.</param>
        /// <param name="city">The displayed city.</param>
        /// <param name="content">The game content.</param>
        /// <param name="building">The building, as the city has it now.</param>
        /// <param name="status">The building's status (<see cref="BuildingStatusModel.Describe"/>).</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static void Show(BuildingView view, CityState city, GameContent content, Building building, BuildingStatus status)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (building == null)
            {
                throw new ArgumentNullException(nameof(building));
            }

            if (view.Animator != null)
            {
                view.Animator.Working = IsWorking(content, building, status);
            }

            FillView? fill = view.Fill;
            if (fill == null)
            {
                return;
            }

            for (int i = 0; i < fill.ItemCount; i++)
            {
                ItemId item = fill.ItemAt(i);
                city.Inventory.TryGetValue(item, out long amount);
                long limit = CityQueries.TryGetEffectiveCap(city, content, item, out long cap) ? cap : 0;
                fill.Show(i, StorageFill.StepFor(amount, limit, fill.StepCountAt(i)));
            }
        }

        /// <summary>
        /// Whether a building moves: a construction site always (its builder hammers), a producer while a cycle runs and nothing stalls it, a
        /// cottage always (smoke from its chimney). Kinds CR-0008 adds (science, couriers, warehouse) stay still until that task decides.
        /// </summary>
        /// <param name="content">The game content.</param>
        /// <param name="building">The building.</param>
        /// <param name="status">Its status.</param>
        /// <returns>True when the building's parts and smoke should move.</returns>
        public static bool IsWorking(GameContent content, Building building, BuildingStatus status)
        {
            if (BuildingLooks.ShowsConstructionSite(building.Phase))
            {
                return true;
            }

            if (!content.TryGetBuilding(building.TypeId, out BuildingDefinition? definition))
            {
                return false;
            }

            switch (definition.Kind)
            {
                case BuildingKind.House:
                    return true;
                case BuildingKind.Producer:
                    return building.Phase == BuildingPhase.Producing && !status.Stalled;
                default:
                    return false;
            }
        }
    }
}
