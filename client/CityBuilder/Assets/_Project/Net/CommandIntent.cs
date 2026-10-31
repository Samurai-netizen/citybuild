#nullable enable

using System;
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
        /// <summary>Creates an intent; the factory methods are the way in, so each kind holds only the members it uses.</summary>
        private CommandIntent(CommandKind kind, string? contentId, int x, int y, int buildingId, int workers)
        {
            Kind = kind;
            ContentId = contentId;
            X = x;
            Y = y;
            BuildingId = buildingId;
            Workers = workers;
        }

        /// <summary>Which command it is.</summary>
        public CommandKind Kind { get; }

        /// <summary>The building type of a place-building intent, or the research of a start-research one; null for the others.</summary>
        public string? ContentId { get; }

        /// <summary>The tile's column of a place-building intent; 0 for the others.</summary>
        public int X { get; }

        /// <summary>The tile's row of a place-building intent; 0 for the others.</summary>
        public int Y { get; }

        /// <summary>The building of an upgrade or assign-workers intent; 0 for the others.</summary>
        public int BuildingId { get; }

        /// <summary>The absolute worker target of an assign-workers intent; 0 for the others.</summary>
        public int Workers { get; }

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

        /// <inheritdoc/>
        public bool Equals(CommandIntent? other)
        {
            return other != null && Kind == other.Kind && ContentId == other.ContentId && X == other.X && Y == other.Y
                && BuildingId == other.BuildingId && Workers == other.Workers;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as CommandIntent);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, ContentId, X, Y, BuildingId, Workers);
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
    }
}
