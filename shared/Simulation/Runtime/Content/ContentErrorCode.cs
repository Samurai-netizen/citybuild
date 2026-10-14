#nullable enable

namespace CityBuilder.Simulation.Content
{
    /// <summary>
    /// What is wrong with a piece of content. <see cref="GameContentFactory"/> reports shape errors first (the first five codes): if there are
    /// any, it stops there. Otherwise it checks every rule and reports every rule error (<c>core-instructions/04-simulation-model.md</c>,
    /// content rules).
    /// </summary>
    public enum ContentErrorCode
    {
        /// <summary>A required value is missing or null, also a null element of a list or a null amount in a map.</summary>
        MissingValue,

        /// <summary>An id's text is not a lowercase letter followed by lowercase letters, digits or <c>_</c>, at most 64 characters in all.</summary>
        InvalidId,

        /// <summary>A text that must be one of a fixed set is not, for example a building kind or an effect type.</summary>
        InvalidValue,

        /// <summary>A value is given where none is allowed: upgrade data on level 1, or a field an effect type doesn't have.</summary>
        UnexpectedValue,

        /// <summary>A number is above its limit: <see cref="GameContentFactory.MaxQuantity"/>, 1000 permille, or one research slot.</summary>
        ValueTooLarge,

        /// <summary>A number that must be greater than zero is not: amounts in recipes and bonuses, labor, durations, caps, grid and slots.</summary>
        NotPositive,

        /// <summary>A number that may be zero is below it: amounts in costs and the starting inventory, the maximum labor reduction.</summary>
        Negative,

        /// <summary>Two definitions of the same kind share an id (or two eras a number), or a prerequisite is listed twice.</summary>
        DuplicateId,

        /// <summary>A reference names an era, item, building type or research node that the content does not define.</summary>
        UnknownReference,

        /// <summary>The era numbers are not exactly 1, 2, … n with at least one era.</summary>
        ErasNotContiguous,

        /// <summary>A building type's level numbers are not exactly 1, 2, … max with at least one level.</summary>
        LevelsNotContiguous,

        /// <summary>A house level provides no workers, takes workers, or has a recipe.</summary>
        InvalidHouseLevel,

        /// <summary>A producer level has no worker slot, provides workers, or has no recipe or a recipe without outputs.</summary>
        InvalidProducerLevel,

        /// <summary>A level has fewer max workers or provides fewer workers than the level below it.</summary>
        WorkersDecrease,

        /// <summary>A capped item has no base storage cap or more than one, or an uncapped item has one.</summary>
        StorageCapMismatch,

        /// <summary>An effect targets the wrong kind of thing: a labor reduction on a house, or a storage bonus on an uncapped item.</summary>
        InvalidEffectTarget,

        /// <summary>A locked building type is unlocked by no research node or by several, or a start building is unlocked by research.</summary>
        UnlockMismatch,

        /// <summary>Research prerequisites form a cycle, so the nodes on it can never be started.</summary>
        ResearchCycle,

        /// <summary>A prerequisite belongs to a later era than the research node that needs it.</summary>
        PrerequisiteInLaterEra,

        /// <summary>The labor reductions of one building type add up to more than <see cref="GameRules.MaxLaborReductionPermille"/>.</summary>
        LaborReductionTooHigh,

        /// <summary>With every labor reduction applied and all max workers assigned, a cycle would be shorter than <see cref="GameRules.MinCycleSeconds"/>.</summary>
        CycleTooShort,
    }
}
