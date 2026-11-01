#nullable enable

namespace CityBuilder.Client.Presentation.Hud
{
    /// <summary>Which way an item's net rate points as the chip shows it, so the view can colour the rate without reading the text.</summary>
    public enum RateTrend
    {
        /// <summary>The shown rate is zero (also a rate too small to show a tenth of an item per hour).</summary>
        Flat = 0,

        /// <summary>The buildings make more of the item than they use.</summary>
        Rising = 1,

        /// <summary>The buildings use more of the item than they make.</summary>
        Falling = 2,
    }
}
