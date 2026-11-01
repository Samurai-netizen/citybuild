#nullable enable

namespace CityBuilder.Client.Presentation.Research
{
    /// <summary>Where a research node stands for the displayed city (M0-52), derived from the city and never stored.</summary>
    public enum ResearchStatus
    {
        /// <summary>A prerequisite is not researched yet.</summary>
        Locked,

        /// <summary>Every prerequisite is researched and the node is neither running nor done: the player may finance it.</summary>
        Available,

        /// <summary>It is the research in the city's one research slot.</summary>
        InProgress,

        /// <summary>It is researched; its effects apply.</summary>
        Done,
    }
}
