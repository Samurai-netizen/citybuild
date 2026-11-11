#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// What a player wants the server to do, before any request id exists: a command kind and its arguments, nothing the server decides
    /// (no cost, level or player id). Two intents with the same kind and arguments are equal; <see cref="CommandSender"/> treats a repeat
    /// of a pending intent as the same intent, so a double tap does not send twice. Make one with the factory methods.
    /// </summary>
    public sealed class CommandIntent : IEquatable<CommandIntent>
    {
        /// <summary>An empty road tile list shared by non-road intents.</summary>
        private static readonly IReadOnlyList<RoadTileRequestDto> EmptyTiles = Array.Empty<RoadTileRequestDto>();

        /// <summary>An empty stop list shared by non-route intents.</summary>
        private static readonly IReadOnlyList<RouteStopRequestDto> EmptyStops = Array.Empty<RouteStopRequestDto>();

        /// <summary>Creates an intent; the factory methods are the way in, so each kind holds only the members it uses.</summary>
        private CommandIntent(
            CommandKind kind,
            string? contentId,
            int x,
            int y,
            int buildingId,
            int workers,
            int targetId = 0,
            string? amount = null,
            IReadOnlyList<RoadTileRequestDto>? tiles = null,
            int slot = 0,
            IReadOnlyList<RouteStopRequestDto>? stops = null,
            int lineId = 0)
        {
            Kind = kind;
            ContentId = contentId;
            X = x;
            Y = y;
            BuildingId = buildingId;
            Workers = workers;
            TargetId = targetId;
            Amount = amount;
            Tiles = tiles ?? EmptyTiles;
            Slot = slot;
            Stops = stops ?? EmptyStops;
            LineId = lineId;
        }

        /// <summary>Which command it is.</summary>
        public CommandKind Kind { get; }

        /// <summary>The building type of a place-building intent, the research of a start-research one, or the item id of a start-trip one; null for the others.</summary>
        public string? ContentId { get; }

        /// <summary>The tile's column of a place-building intent; 0 for the others.</summary>
        public int X { get; }

        /// <summary>The tile's row of a place-building intent; 0 for the others.</summary>
        public int Y { get; }

        /// <summary>The building of an upgrade or assign-workers intent, or the source building of a start-trip intent; 0 for the others.</summary>
        public int BuildingId { get; }

        /// <summary>The absolute worker target of an assign-workers intent; 0 for the others.</summary>
        public int Workers { get; }

        /// <summary>The target building of a start-trip intent; 0 for the others.</summary>
        public int TargetId { get; }

        /// <summary>The amount text (<c>all</c>, <c>half</c>, <c>quarter</c>, <c>25%</c>, or an exact quantity) of a start-trip intent; null for the others.</summary>
        public string? Amount { get; }

        /// <summary>The tiles of a <c>place-roads</c>, <c>upgrade-roads</c> or <c>remove-roads</c> intent; empty for the others.</summary>
        public IReadOnlyList<RoadTileRequestDto> Tiles { get; }

        /// <summary>The delivery line of a cancel-delivery intent, or 0 to cancel all of the courier's lines and its route; 0 for the others.</summary>
        public int LineId { get; }

        /// <summary>The courier slot of a set-route or cancel-delivery intent; 0 for the others.</summary>
        public int Slot { get; }

        /// <summary>The stops of a set-route intent (empty clears the route); empty for the others.</summary>
        public IReadOnlyList<RouteStopRequestDto> Stops { get; }

        /// <summary>Build <paramref name="buildingTypeId"/> on the tile (<paramref name="x"/>, <paramref name="y"/>).</summary>
        /// <exception cref="ArgumentException">The type id is null or blank.</exception>
        public static CommandIntent PlaceBuilding(string buildingTypeId, int x, int y)
        {
            return new CommandIntent(CommandKind.PlaceBuilding, RequireId(buildingTypeId, nameof(buildingTypeId)), x, y, 0, 0);
        }

        /// <summary>Raise the building <paramref name="buildingId"/> one level.</summary>
        public static CommandIntent UpgradeBuilding(int buildingId)
        {
            return new CommandIntent(CommandKind.UpgradeBuilding, null, 0, 0, buildingId, 0);
        }

        /// <summary>Give the building <paramref name="buildingId"/> exactly <paramref name="workers"/> workers (0 pauses it).</summary>
        public static CommandIntent AssignWorkers(int buildingId, int workers)
        {
            return new CommandIntent(CommandKind.AssignWorkers, null, 0, 0, buildingId, workers);
        }

        /// <summary>Start the research <paramref name="researchId"/>.</summary>
        /// <exception cref="ArgumentException">The research id is null or blank.</exception>
        public static CommandIntent StartResearch(string researchId)
        {
            return new CommandIntent(CommandKind.StartResearch, RequireId(researchId, nameof(researchId)), 0, 0, 0, 0);
        }

        /// <summary>Send a free worker from <paramref name="sourceId"/> to <paramref name="targetId"/> carrying <paramref name="amount"/> of <paramref name="itemId"/> (CR-0008).</summary>
        /// <exception cref="ArgumentException">The item id or amount text is null or blank.</exception>
        public static CommandIntent StartTrip(int sourceId, int targetId, string itemId, string amount)
        {
            return new CommandIntent(
                CommandKind.StartTrip,
                RequireId(itemId, nameof(itemId)),
                0,
                0,
                sourceId,
                0,
                targetId,
                RequireId(amount, nameof(amount)));
        }

        /// <summary>Place level-1 roads on <paramref name="tiles"/> (CR-0008).</summary>
        /// <exception cref="ArgumentException"><paramref name="tiles"/> is null or empty.</exception>
        public static CommandIntent PlaceRoads(IReadOnlyList<RoadTileRequestDto> tiles)
        {
            return new CommandIntent(CommandKind.PlaceRoads, null, 0, 0, 0, 0, tiles: RequireTiles(tiles, nameof(tiles)));
        }

        /// <summary>Raise the roads on <paramref name="tiles"/> one level (CR-0008).</summary>
        /// <exception cref="ArgumentException"><paramref name="tiles"/> is null or empty.</exception>
        public static CommandIntent UpgradeRoads(IReadOnlyList<RoadTileRequestDto> tiles)
        {
            return new CommandIntent(CommandKind.UpgradeRoads, null, 0, 0, 0, 0, tiles: RequireTiles(tiles, nameof(tiles)));
        }

        /// <summary>Remove the roads on <paramref name="tiles"/> (CR-0008).</summary>
        /// <exception cref="ArgumentException"><paramref name="tiles"/> is null or empty.</exception>
        public static CommandIntent RemoveRoads(IReadOnlyList<RoadTileRequestDto> tiles)
        {
            return new CommandIntent(CommandKind.RemoveRoads, null, 0, 0, 0, 0, tiles: RequireTiles(tiles, nameof(tiles)));
        }

        /// <summary>Set the route of courier <paramref name="slot"/> at station <paramref name="stationId"/> to <paramref name="stops"/>; none clears it (CR-0008).</summary>
        /// <exception cref="ArgumentException"><paramref name="stops"/> is null or has a null entry.</exception>
        public static CommandIntent SetRoute(int stationId, int slot, IReadOnlyList<RouteStopRequestDto> stops)
        {
            return new CommandIntent(CommandKind.SetRoute, null, 0, 0, stationId, 0, slot: slot, stops: RequireStops(stops, nameof(stops)));
        }

        /// <summary>
        /// Cancel delivery line <paramref name="lineId"/> of courier <paramref name="slot"/> at station <paramref name="stationId"/>, or with 0 all of
        /// its lines and its route (CR-0008 amendment 1).
        /// </summary>
        public static CommandIntent CancelDelivery(int stationId, int slot, int lineId)
        {
            return new CommandIntent(CommandKind.CancelDelivery, null, 0, 0, stationId, 0, slot: slot, lineId: lineId);
        }

        /// <summary>Give every courier its full stamina again (CR-0008); it has no argument.</summary>
        public static CommandIntent ReenergizeCouriers()
        {
            return new CommandIntent(CommandKind.ReenergizeCouriers, null, 0, 0, 0, 0);
        }

        /// <inheritdoc/>
        public bool Equals(CommandIntent? other)
        {
            return other != null && Kind == other.Kind && ContentId == other.ContentId && X == other.X && Y == other.Y
                && BuildingId == other.BuildingId && Workers == other.Workers && TargetId == other.TargetId && Amount == other.Amount
                && SameTiles(Tiles, other.Tiles) && Slot == other.Slot && SameStops(Stops, other.Stops) && LineId == other.LineId;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as CommandIntent);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int tilesHash = Tiles.Count;
            for (int i = 0; i < Tiles.Count; i++)
            {
                tilesHash = HashCode.Combine(tilesHash, Tiles[i]);
            }

            return HashCode.Combine(Kind, ContentId, X, Y, BuildingId, Workers, TargetId, HashCode.Combine(Amount, tilesHash, Slot, Stops.Count, LineId));
        }

        /// <summary>A one-line form for logs, for example <c>AssignWorkers(building 3, workers 2)</c>.</summary>
        public override string ToString()
        {
            switch (Kind)
            {
                case CommandKind.PlaceBuilding:
                    return "PlaceBuilding(" + ContentId + " at " + X + "," + Y + ")";
                case CommandKind.UpgradeBuilding:
                    return "UpgradeBuilding(building " + BuildingId + ")";
                case CommandKind.AssignWorkers:
                    return "AssignWorkers(building " + BuildingId + ", workers " + Workers + ")";
                case CommandKind.StartTrip:
                    return "StartTrip(" + BuildingId + "->" + TargetId + " " + ContentId + " " + Amount + ")";
                case CommandKind.PlaceRoads:
                    return "PlaceRoads(" + FormatTiles(Tiles) + ")";
                case CommandKind.UpgradeRoads:
                    return "UpgradeRoads(" + FormatTiles(Tiles) + ")";
                case CommandKind.RemoveRoads:
                    return "RemoveRoads(" + FormatTiles(Tiles) + ")";
                case CommandKind.SetRoute:
                    return "SetRoute(station " + BuildingId + ", slot " + Slot + ", " + Stops.Count + " stops)";
                case CommandKind.ReenergizeCouriers:
                    return "ReenergizeCouriers()";
                case CommandKind.CancelDelivery:
                    return "CancelDelivery(station " + BuildingId + ", slot " + Slot + ", " + (LineId == 0 ? "all" : "line " + LineId) + ")";
                default:
                    return "StartResearch(" + ContentId + ")";
            }
        }

        /// <summary>Sends the command of this intent under <paramref name="requestId"/> through <paramref name="api"/>.</summary>
        internal Task<ApiResult<CitySnapshotDto>> SendAsync(ApiClient api, Guid requestId, CancellationToken cancellationToken)
        {
            switch (Kind)
            {
                case CommandKind.PlaceBuilding:
                    return api.PlaceBuildingAsync(new PlaceBuildingRequestDto(requestId, ContentId!, X, Y), cancellationToken);
                case CommandKind.UpgradeBuilding:
                    return api.UpgradeBuildingAsync(new UpgradeBuildingRequestDto(requestId, BuildingId), cancellationToken);
                case CommandKind.AssignWorkers:
                    return api.AssignWorkersAsync(new AssignWorkersRequestDto(requestId, BuildingId, Workers), cancellationToken);
                case CommandKind.StartTrip:
                    return api.StartTripAsync(new StartTripRequestDto(requestId, BuildingId, TargetId, ContentId!, Amount!), cancellationToken);
                case CommandKind.PlaceRoads:
                    return api.PlaceRoadsAsync(new RoadTilesRequestDto(requestId, Tiles), cancellationToken);
                case CommandKind.UpgradeRoads:
                    return api.UpgradeRoadsAsync(new RoadTilesRequestDto(requestId, Tiles), cancellationToken);
                case CommandKind.RemoveRoads:
                    return api.RemoveRoadsAsync(new RoadTilesRequestDto(requestId, Tiles), cancellationToken);
                case CommandKind.SetRoute:
                    return api.SetRouteAsync(new SetRouteRequestDto(requestId, BuildingId, Slot, Stops), cancellationToken);
                case CommandKind.ReenergizeCouriers:
                    return api.ReenergizeCouriersAsync(new ReenergizeCouriersRequestDto(requestId), cancellationToken);
                case CommandKind.CancelDelivery:
                    return api.CancelDeliveryAsync(
                        new CancelDeliveryRequestDto(requestId, BuildingId, Slot, LineId == 0 ? (int?)null : LineId), cancellationToken);
                default:
                    return api.StartResearchAsync(new StartResearchRequestDto(requestId, ContentId!), cancellationToken);
            }
        }

        /// <summary>The id, or an exception when it is null or blank: no content has such an id, so no request is worth making.</summary>
        private static string RequireId(string? id, string name)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("The id must not be blank.", name);
            }

            return id!;
        }

        /// <summary>Copies a non-empty tile list and refuses null, empty, or null entries.</summary>
        private static IReadOnlyList<RoadTileRequestDto> RequireTiles(IReadOnlyList<RoadTileRequestDto>? tiles, string name)
        {
            if (tiles == null || tiles.Count == 0)
            {
                throw new ArgumentException("At least one road tile is required.", name);
            }

            var copy = new RoadTileRequestDto[tiles.Count];
            for (int i = 0; i < tiles.Count; i++)
            {
                copy[i] = tiles[i] ?? throw new ArgumentException("A road tile entry must not be null.", name);
            }

            return copy;
        }

        /// <summary>Copies a stop list and refuses null or null entries; an empty list is valid (it clears the route).</summary>
        private static IReadOnlyList<RouteStopRequestDto> RequireStops(IReadOnlyList<RouteStopRequestDto>? stops, string name)
        {
            if (stops == null)
            {
                throw new ArgumentException("The stop list must not be null.", name);
            }

            var copy = new RouteStopRequestDto[stops.Count];
            for (int i = 0; i < stops.Count; i++)
            {
                copy[i] = stops[i] ?? throw new ArgumentException("A route stop must not be null.", name);
            }

            return copy;
        }

        /// <summary>Whether two stop lists have equal stops in the same order.</summary>
        private static bool SameStops(IReadOnlyList<RouteStopRequestDto> left, IReadOnlyList<RouteStopRequestDto> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!left[i].Equals(right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether two tile lists have the same tiles in the same order.</summary>
        private static bool SameTiles(IReadOnlyList<RoadTileRequestDto> left, IReadOnlyList<RoadTileRequestDto> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!left[i].Equals(right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Formats a road tile list for <see cref="ToString"/>.</summary>
        private static string FormatTiles(IReadOnlyList<RoadTileRequestDto> tiles)
        {
            var parts = new string[tiles.Count];
            for (int i = 0; i < tiles.Count; i++)
            {
                parts[i] = tiles[i].X + "," + tiles[i].Y;
            }

            return string.Join(";", parts);
        }
    }
}
