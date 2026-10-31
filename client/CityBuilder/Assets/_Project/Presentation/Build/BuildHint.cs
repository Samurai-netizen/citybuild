#nullable enable

namespace CityBuilder.Client.Presentation.Build
{
    /// <summary>
    /// What a card says may go wrong when the player builds it now, judged from the latest city the client has. A hint is only a hint: the
    /// card stays selectable and the server's checks decide (<c>09</c> UX rules). The order is the server's: the builder is checked before the cost.
    /// </summary>
    public enum BuildHint
    {
        /// <summary>Nothing to warn about.</summary>
        None,

        /// <summary>Every builder slot is busy with a construction or an upgrade.</summary>
        NoFreeBuilder,

        /// <summary>The stock does not cover the cost.</summary>
        CannotAfford,
    }
}
