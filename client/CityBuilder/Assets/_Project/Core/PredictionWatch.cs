#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.Settlement;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Core
{
    /// <summary>
    /// A development check of the waiting loop (M0-54, <c>docs/testing/LOCAL-E2E.md</c>): at every new snapshot it reports, as text, when the
    /// display went back in time or a counter fell, and when the snapshot differs from the engine run on the snapshot before it, with both cities as
    /// JSON, so a mismatch can become a regression test. It reads only; what the player sees is not changed by it. <c>GameRoot</c> makes it in the
    /// Editor and in development builds. It lives as long as the display city it listens to, so it never unsubscribes.
    /// </summary>
    public sealed class PredictionWatch
    {
        /// <summary>The store whose snapshots are compared.</summary>
        private readonly ClientCityStore _store;

        /// <summary>The content the engine runs with.</summary>
        private readonly GameContent _content;

        /// <summary>Whether a command is on its way: its answer changes the city on purpose, so that snapshot says nothing about the prediction.</summary>
        private readonly Func<bool> _commandsPending;

        /// <summary>Takes what is found.</summary>
        private readonly Action<string> _report;

        /// <summary>The snapshot before the one the store holds now, or null before the first.</summary>
        private CitySnapshot? _previous;

        /// <summary>Starts listening to <paramref name="displayCity"/>.</summary>
        /// <param name="store">The city store under the display city.</param>
        /// <param name="displayCity">The display city.</param>
        /// <param name="content">The content the engine runs with.</param>
        /// <param name="commandsPending">Says whether a command is on its way.</param>
        /// <param name="report">Takes what is found; the game passes its log.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public PredictionWatch(ClientCityStore store, DisplayCity displayCity, GameContent content, Func<bool> commandsPending, Action<string> report)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _commandsPending = commandsPending ?? throw new ArgumentNullException(nameof(commandsPending));
            _report = report ?? throw new ArgumentNullException(nameof(report));
            if (displayCity == null)
            {
                throw new ArgumentNullException(nameof(displayCity));
            }

            _previous = store.Snapshot;
            displayCity.SnapshotReplaced += OnSnapshotReplaced;
        }

        /// <summary>
        /// Every stored value of <paramref name="city"/> as one line of JSON, in an order that does not depend on the dictionary or the list: the
        /// purse, research by id, buildings by id with their store and needs (goods by id), roads by tile (column, then row, the engine's order),
        /// trips by id (CR-0008), couriers with their loads and routes with their stops, both by (station, slot) as the engine keeps them
    /// (CR-0008-T1 part G). Two cities that are the same give the same text.
        /// </summary>
        /// <param name="city">The city.</param>
        /// <returns>The text.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
        public static string ToJson(CityState city)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            var completed = new List<string>(city.CompletedResearch.Count);
            foreach (ResearchId id in city.CompletedResearch)
            {
                completed.Add(id.Value);
            }

            completed.Sort(StringComparer.Ordinal);

            var buildings = new SortedDictionary<int, Building>();
            foreach (Building building in city.Buildings)
            {
                buildings.Add(building.Id, building);
            }

            var text = new StringBuilder();
            text.Append(Invariant($"{{\"clock\":{city.Clock},\"era\":{city.Era},\"nextBuildingId\":{city.NextBuildingId},\"purse\":{city.Purse}"));
            text.Append(Invariant($",\"nextTripId\":{city.NextTripId},\"completedResearch\":["));
            string separator = string.Empty;
            foreach (string id in completed)
            {
                text.Append(separator).Append('"').Append(id).Append('"');
                separator = ",";
            }

            text.Append("],\"activeResearch\":");
            ActiveResearch? research = city.ActiveResearch;
            text.Append(research == null
                ? "null"
                : Invariant($"{{\"id\":\"{research.Id.Value}\",\"startedAt\":{Time(research.StartedAt)},\"endsAt\":{Time(research.EndsAt)},\"needs\":{Amounts(research.Needs)}}}"));
            text.Append(",\"buildings\":[");
            separator = string.Empty;
            foreach (Building building in buildings.Values)
            {
                text.Append(separator).Append(ToJson(building));
                separator = ",";
            }

            text.Append("],\"roads\":[");
            separator = string.Empty;
            foreach (RoadTile road in city.Roads)
            {
                text.Append(separator).Append(Invariant($"{{\"x\":{road.X},\"y\":{road.Y},\"level\":{road.Level}}}"));
                separator = ",";
            }

            text.Append("],\"trips\":[");
            separator = string.Empty;
            foreach (Trip trip in city.Trips)
            {
                text.Append(separator).Append(Invariant(
                    $"{{\"id\":{trip.Id},\"sourceId\":{trip.SourceId},\"targetId\":{trip.TargetId},\"itemId\":\"{trip.Item.Value}\",\"quantity\":{trip.Quantity},\"departedAt\":{trip.DepartedAt},\"arrivesAt\":{trip.ArrivesAt}}}"));
                separator = ",";
            }

            text.Append("],\"couriers\":[");
            separator = string.Empty;
            foreach (Courier courier in city.Couriers)
            {
                text.Append(separator).Append(Invariant(
                    $"{{\"stationId\":{courier.StationId},\"slot\":{courier.Slot},\"state\":\"{courier.State}\",\"toStop\":{courier.ToStop},\"departedAt\":{Time(courier.DepartedAt)},\"arrivesAt\":{Time(courier.ArrivesAt)},\"stamina\":{courier.Stamina},\"load\":["));
                string partSeparator = string.Empty;
                foreach (CourierLoad part in courier.Loads)
                {
                    text.Append(partSeparator).Append(Invariant($"{{\"itemId\":\"{part.Item.Value}\",\"fromId\":{part.FromId},\"quantity\":{part.Quantity}}}"));
                    partSeparator = ",";
                }

                text.Append("]}");
                separator = ",";
            }

            text.Append("],\"routes\":[");
            separator = string.Empty;
            foreach (Route route in city.Routes)
            {
                text.Append(separator).Append(Invariant($"{{\"stationId\":{route.StationId},\"slot\":{route.Slot},\"stops\":["));
                string stopSeparator = string.Empty;
                foreach (RouteStop stop in route.Stops)
                {
                    text.Append(stopSeparator).Append(Invariant($"{{\"buildingId\":{stop.BuildingId},\"actions\":["));
                    string actionSeparator = string.Empty;
                    foreach (RouteAction action in stop.Actions)
                    {
                        text.Append(actionSeparator).Append(Invariant($"{{\"kind\":\"{action.Kind}\",\"itemId\":\"{action.Item.Value}\",\"amount\":\"{action.Amount}\"}}"));
                        actionSeparator = ",";
                    }

                    text.Append("]}");
                    stopSeparator = ",";
                }

                text.Append("]}");
                separator = ",";
            }

            return text.Append("]}").ToString();
        }

        /// <summary>A nullable time as JSON: the number, or <c>null</c>.</summary>
        private static string Time(long? time) => time.HasValue ? Invariant($"{time.Value}") : "null";

        /// <summary>Quantities as a JSON object, goods by id in ordinal order.</summary>
        private static string Amounts(IReadOnlyDictionary<ItemId, long> amounts)
        {
            var sorted = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (KeyValuePair<ItemId, long> amount in amounts)
            {
                sorted.Add(amount.Key.Value, amount.Value);
            }

            var text = new StringBuilder("{");
            string separator = string.Empty;
            foreach (KeyValuePair<string, long> amount in sorted)
            {
                text.Append(separator).Append(Invariant($"\"{amount.Key}\":{amount.Value}"));
                separator = ",";
            }

            return text.Append('}').ToString();
        }

        /// <summary>One building's stored values as JSON.</summary>
        private static string ToJson(Building building)
        {
            string ends = building.PhaseEndsAt.HasValue ? Invariant($"{building.PhaseEndsAt.Value}") : "null";
            return Invariant(
                $"{{\"id\":{building.Id},\"typeId\":\"{building.TypeId.Value}\",\"x\":{building.X},\"y\":{building.Y},\"level\":{building.Level},\"assignedWorkers\":{building.AssignedWorkers},\"phase\":\"{building.Phase}\",\"phaseStartedAt\":{building.PhaseStartedAt},\"phaseEndsAt\":{ends},\"targetLevel\":{building.TargetLevel},\"cycleLabor\":{building.CycleLabor},\"laborRemaining\":{building.LaborRemaining},\"segmentStartedAt\":{building.SegmentStartedAt},\"store\":{Amounts(building.Store)},\"needs\":{Amounts(building.Needs)}}}");
        }

        /// <summary>A text with plain digits, never in the machine's culture.</summary>
        private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

        /// <summary>The display city took a new snapshot and showed <paramref name="shown"/> until now: looks for what the player would have noticed.</summary>
        private void OnSnapshotReplaced(CityState shown)
        {
            CitySnapshot snapshot = _store.Snapshot!;
            CitySnapshot? previous = _previous;
            _previous = snapshot;
            bool commandsPending = _commandsPending();
            ReportGoingBack(shown, snapshot, commandsPending);
            if (previous != null && !commandsPending && snapshot.Clock >= previous.Clock)
            {
                ReportMismatch(previous, snapshot);
            }
        }

        /// <summary>Reports a clock that went back and, unless a command explains it, each item whose counter fell.</summary>
        private void ReportGoingBack(CityState shown, CitySnapshot snapshot, bool commandsPending)
        {
            if (snapshot.Clock >= shown.Clock)
            {
                return;
            }

            CityState city = snapshot.CopyCity();
            var text = new StringBuilder();
            text.Append(Invariant($"PredictionWatch: the clock went back {shown.Clock - snapshot.Clock} s (shown {shown.Clock}, state version {snapshot.StateVersion} at {snapshot.Clock})."));
            if (!commandsPending)
            {
                foreach (ItemDefinition item in _content.Items)
                {
                    long before = CityQueries.Total(shown, _content, item.Id);
                    long now = CityQueries.Total(city, _content, item.Id);
                    if (now < before)
                    {
                        text.Append(Invariant($" {item.Id.Value} {before} -> {now}."));
                    }
                }
            }

            _report(text.ToString());
        }

        /// <summary>Runs the engine on <paramref name="previous"/> up to the clock of <paramref name="snapshot"/>, as the server's settlement did, and reports a difference.</summary>
        private void ReportMismatch(CitySnapshot previous, CitySnapshot snapshot)
        {
            CityState predicted = previous.CopyCity();
            try
            {
                Simulator.Advance(predicted, _content, snapshot.Clock - previous.Clock);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException || exception is OverflowException)
            {
                // The display city has logged this one already; there is nothing to compare.
                return;
            }

            string expected = ToJson(predicted);
            string actual = ToJson(snapshot.CopyCity());
            if (expected != actual)
            {
                _report(Invariant($"PredictionWatch: mismatch at state version {snapshot.StateVersion}.\nprevious  {ToJson(previous.CopyCity())}\npredicted {expected}\nsnapshot  {actual}"));
            }
        }
    }
}
