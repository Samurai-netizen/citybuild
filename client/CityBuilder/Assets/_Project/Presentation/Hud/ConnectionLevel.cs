#nullable enable

namespace CityBuilder.Client.Presentation.Hud
{
    /// <summary>How the connection indicator of the HUD is coloured: the view maps a level to a colour, the model decides the level.</summary>
    public enum ConnectionLevel
    {
        /// <summary>The last request got a usable answer: what the HUD shows is current.</summary>
        Ok = 0,

        /// <summary>The client is starting; there is nothing wrong yet.</summary>
        Pending = 1,

        /// <summary>The connection is down or refused: what the HUD shows may be out of date.</summary>
        Problem = 2,
    }
}
