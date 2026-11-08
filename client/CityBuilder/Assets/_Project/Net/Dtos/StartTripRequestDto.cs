using System;

namespace CityBuilder.Client.Net
{
    /// <summary>
    /// Request body of <c>POST /api/v1/commands/start-trip</c> (CR-0008, <c>05-api-contract.md</c>):
    /// <c>{ "requestId": "…", "sourceId": 1, "targetId": 2, "itemId": "logs", "amount": "all" }</c>.
    /// </summary>
    [Serializable]
    public sealed class StartTripRequestDto
    {
        /// <summary>Client-generated UUID reused on every retry of the command.</summary>
        public string requestId = string.Empty;

        /// <summary>Id of the building in the player's city whose store the worker takes the good from.</summary>
        public int sourceId;

        /// <summary>Id of the building in the player's city that receives the good.</summary>
        public int targetId;

        /// <summary>Content id of the good to carry, for example <c>logs</c>.</summary>
        public string itemId = string.Empty;

        /// <summary>How much to carry (<c>all</c>, <c>half</c>, <c>quarter</c>, a percentage like <c>25%</c>, or an exact quantity).</summary>
        public string amount = string.Empty;

        /// <summary>Creates an empty body for <c>JsonUtility</c>.</summary>
        public StartTripRequestDto()
        {
        }

        /// <summary>Creates a body for the given request id, source and target building ids, item id and amount text.</summary>
        public StartTripRequestDto(Guid requestId, int sourceId, int targetId, string itemId, string amount)
        {
            this.requestId = requestId.ToString("D");
            this.sourceId = sourceId;
            this.targetId = targetId;
            this.itemId = itemId ?? string.Empty;
            this.amount = amount ?? string.Empty;
        }
    }
}
