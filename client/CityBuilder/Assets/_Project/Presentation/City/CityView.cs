#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The buildings of the city on the screen (M0-44). <see cref="Show"/> takes the displayed city (the <c>DisplayCity</c>'s current state) and
    /// reconciles the views with it by building id: a building without a view gets one, a view whose building changed is updated in place, and
    /// a view whose building is gone is destroyed, so there is exactly one view per building and none left over. It runs once a second with
    /// the display city, so a call where nothing changed walks the buildings and allocates nothing. It decides nothing, shows a building under
    /// construction or upgrade as the construction site (nothing finished appears before the server says so, <c>09</c> "UX rules") and never calls the server.
    /// Given the content, it also shows each building's status (M0-50): a ring, a bubble and worker pips (<see cref="BuildingStatusView"/>), drawn only
    /// when they differ from the last call, and a stalled building looks slightly washed out (<see cref="StalledTint"/>). <see cref="Destroy"/> removes everything it made.
    /// </summary>
    public sealed class CityView
    {
        /// <summary>The name of the object that holds the building views.</summary>
        public const string RootName = "Buildings";

        /// <summary>The views by building id.</summary>
        private readonly Dictionary<int, BuildingView> _views = new Dictionary<int, BuildingView>();

        /// <summary>The ids seen in the city in the current call to <see cref="Show"/>; kept to avoid a new set each second.</summary>
        private readonly HashSet<int> _seen = new HashSet<int>();

        /// <summary>The ids to remove in the current call; kept for the same reason.</summary>
        private readonly List<int> _gone = new List<int>();

        /// <summary>The factory that builds and refreshes the views.</summary>
        private readonly BuildingViewFactory _factory;

        /// <summary>The camera the statuses face, or null.</summary>
        private readonly Transform? _facing;

        /// <summary>The pictures the statuses share, made when the first status is shown.</summary>
        private StatusArt? _statusArt;

        /// <summary>Gives stalled buildings their washed-out look.</summary>
        private readonly StalledTint _tint = new StalledTint();

        /// <summary>Whether <see cref="Destroy"/> has run.</summary>
        private bool _destroyed;

        /// <summary>Creates the view under a new object called <c>Buildings</c> that is a child of <paramref name="parent"/>.</summary>
        /// <param name="grid">The grid the buildings stand on.</param>
        /// <param name="parent">The transform the buildings hang under.</param>
        /// <param name="facing">The camera the statuses turn to face, or null to leave them as they are.</param>
        /// <exception cref="ArgumentNullException"><paramref name="grid"/> or <paramref name="parent"/> is null.</exception>
        public CityView(CityGrid grid, Transform parent, Transform? facing = null)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            _factory = new BuildingViewFactory(grid);
            _facing = facing;
            Root = new GameObject(RootName);
            Root.transform.SetParent(parent, false);
        }

        /// <summary>The object that holds every building view.</summary>
        public GameObject Root { get; }

        /// <summary>The number of buildings shown.</summary>
        public int Count => _views.Count;

        /// <summary>The view of a building, or false when none is shown.</summary>
        /// <param name="buildingId">The building's id in its city.</param>
        /// <param name="view">The view, or null.</param>
        /// <returns>True when the building has a view.</returns>
        public bool TryGetView(int buildingId, out BuildingView? view)
        {
            return _views.TryGetValue(buildingId, out view);
        }

        /// <summary>
        /// Makes the views match <paramref name="city"/>: adds a view for each new building, updates the views of changed ones and removes the
        /// views of buildings that are no longer there. Calling it again with the same city changes nothing.
        /// </summary>
        /// <param name="city">The displayed city; its clock is the clock the rings show.</param>
        /// <param name="content">The game content, which the statuses need to tell why a building stalls; null shows no status.</param>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">The view was destroyed.</exception>
        public void Show(CityState city, GameContent? content = null)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (_destroyed)
            {
                throw new ObjectDisposedException(nameof(CityView));
            }

            _seen.Clear();
            IReadOnlyList<Building> buildings = city.Buildings;
            for (int i = 0; i < buildings.Count; i++)
            {
                Building building = buildings[i];
                _seen.Add(building.Id);
                if (_views.TryGetValue(building.Id, out BuildingView? view))
                {
                    _factory.Refresh(view, building, city.Era);
                }
                else
                {
                    view = _factory.Create(building, city.Era, Root.transform);
                    _views.Add(building.Id, view);
                }

                if (content != null)
                {
                    ShowStatus(view, city, content, building);
                }
            }

            if (_seen.Count == _views.Count)
            {
                return;
            }

            _gone.Clear();
            foreach (KeyValuePair<int, BuildingView> entry in _views)
            {
                if (!_seen.Contains(entry.Key))
                {
                    _gone.Add(entry.Key);
                }
            }

            for (int i = 0; i < _gone.Count; i++)
            {
                BuildingView view = _views[_gone[i]];
                _views.Remove(_gone[i]);

                // Out of the tree at once, so a delayed destroy leaves no view that still counts as a child this frame.
                view.transform.SetParent(null, false);
                PlaceholderArt.Destroy(view.gameObject);
            }
        }

        /// <summary>Destroys every view, the root object and the materials. Safe to call twice.</summary>
        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;
            _views.Clear();
            PlaceholderArt.Destroy(Root);
            _factory.Destroy();
            _statusArt?.Destroy();
        }

        /// <summary>Draws the status of one building on its view (its washed-out look when stalled, its ring, bubble and pips), making the status object and the shared pictures the first time.</summary>
        private void ShowStatus(BuildingView view, CityState city, GameContent content, Building building)
        {
            BuildingStatus status = BuildingStatusModel.Describe(city, content, building);
            _tint.Apply(view, status.Stalled);
            BuildingStatusView? statusView = view.Status;
            if (statusView == null)
            {
                // A building with nothing to show gets no status object at all, so a city of cottages carries none.
                if (status == default)
                {
                    return;
                }

                _statusArt ??= new StatusArt();
                statusView = BuildingStatusView.Create(view, _statusArt, _facing);
            }

            statusView.Apply(status);
        }
    }
}
