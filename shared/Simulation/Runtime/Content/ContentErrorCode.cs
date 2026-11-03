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

        /// <summary>
        /// A number is above its limit: <see cref="GameContentFactory.MaxQuantity"/>, <see cref="GameContentFactory.MaxStoredSmallValue"/>, 1000
        /// permille, 100 percent, one research slot, or a route limit above the API's (16 stops, 8 actions).
        /// </summary>
        ValueTooLarge,

        /// <summary>
        /// A number that must be greater than zero is not: amounts in recipes and bonuses, labor, durations, store limits, carry limits, stamina,
        /// percentages, grid and slots, <c>maxPerCity</c>.
        /// </summary>
        NotPositive,

        /// <summary>A number that may be zero is below it: amounts in costs, the starting purse, the maximum labor reduction.</summary>
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

        /// <summary>A producer level has no worker slot, provides workers, has no recipe or a recipe without outputs, or has a carry limit or stamina.</summary>
        InvalidProducerLevel,

        /// <summary>A level has fewer max workers or provides fewer workers than the level below it.</summary>
        WorkersDecrease,

        /// <summary>A warehouse level has no store, or has workers, a recipe, a carry limit or stamina (<c>04</c> section 14 rule 3).</summary>
        InvalidWarehouseLevel,

        /// <summary>A courier station level has no worker slot, carry limit or stamina, or has a store, a recipe or workers provided (rule 3).</summary>
        InvalidCourierStationLevel,

        /// <summary>A science building level has workers, a recipe, a store, a carry limit or stamina (rule 3).</summary>
        InvalidScienceLevel,

        /// <summary>A courier station level has a smaller carry limit or less stamina than the level below it (rule 3).</summary>
        CourierLevelDecrease,

        /// <summary>
        /// A producer level's store misses a good of its recipe or holds less of it than one cycle's quantity (rule 9), or a store holds the
        /// currency (rule 4).
        /// </summary>
        StoreMismatch,

        /// <summary>A level's store holds fewer goods than the level below, or a lower limit for one of them (rule 9).</summary>
        StoreShrinks,

        /// <summary>The content has no currency or more than one (rule 4).</summary>
        CurrencyCount,

        /// <summary>
        /// The currency is used where only goods may be: a recipe input or a storage bonus (rule 4); or a road level costs something other than
        /// the currency (rule 11, CR-0008 D6).
        /// </summary>
        InvalidCurrencyUse,

        /// <summary>The content has no science building or more than one, or the science building's <c>maxPerCity</c> isn't 1 (rule 10).</summary>
        ScienceBuildingMismatch,

        /// <summary>An effect targets the wrong kind of thing: a labor reduction on a building that isn't a producer, or a storage bonus on the currency.</summary>
        InvalidEffectTarget,

        /// <summary>A locked building type is unlocked by no research node or by several, or a start building is unlocked by research.</summary>
        UnlockMismatch,

        /// <summary>Research prerequisites form a cycle, so the nodes on it can never be started.</summary>
        ResearchCycle,

        /// <summary>A prerequisite belongs to a later era than the research node that needs it.</summary>
        PrerequisiteInLaterEra,

        /// <summary>
        /// An item in a cost, a recipe or the starting inventory belongs to a later era than what uses it (the building type, the research node,
        /// the starting city).
        /// </summary>
        ItemInLaterEra,

        /// <summary>The labor reductions of one building type add up to more than <see cref="GameRules.MaxLaborReductionPermille"/>.</summary>
        LaborReductionTooHigh,

        /// <summary>With every labor reduction applied and all max workers assigned, a cycle would be shorter than <see cref="GameRules.MinCycleSeconds"/>.</summary>
        CycleTooShort,

        /// <summary>
        /// A road level is not faster than what it improves: its tile time must be below the ground time and below the level before
        /// (<c>core-instructions/04-simulation-model.md</c>, content rule 11).
        /// </summary>
        RoadNotFaster,

        /// <summary>
        /// The shortest possible walk, one step between two adjacent buildings with every walk bonus applied, is shorter than
        /// <c>rules.minWalkSeconds</c> (<c>04</c>, content rule 12).
        /// </summary>
        WalkTooShort,

        /// <summary>The walk bonuses of all research add up to more than <see cref="GameRules.MaxWalkBonusPermille"/> (rule 12).</summary>
        WalkBonusTooHigh,

        /// <summary>A number is below its least value above 0: a route of fewer than 2 stops (<c>maxRouteStops</c>, rule 13 and the shape).</summary>
        ValueTooSmall,

        /// <summary>The content has no amount preset or more than 8 (rule 13).</summary>
        InvalidAmountPresets,
    }
}
