#nullable enable
using System;
using System.Collections.Generic;

namespace CityBuilder.Simulation.Content
{
    /// <summary>The global rules of the game content (<c>core-instructions/23-mvp-content.md</c>, global rules).</summary>
    public sealed class GameRules
    {
        private readonly Dictionary<ItemId, long> _baseStorageCapByItem;

        internal GameRules(
            int gridWidth,
            int gridHeight,
            int builderSlots,
            int researchSlots,
            IReadOnlyList<ItemQuantity> baseStorageCaps,
            long maxOfflineProductionSeconds,
            long minCycleSeconds,
            int maxLaborReductionPermille)
        {
            GridWidth = gridWidth;
            GridHeight = gridHeight;
            BuilderSlots = builderSlots;
            ResearchSlots = researchSlots;
            BaseStorageCaps = baseStorageCaps ?? throw new ArgumentNullException(nameof(baseStorageCaps));
            MaxOfflineProductionSeconds = maxOfflineProductionSeconds;
            MinCycleSeconds = minCycleSeconds;
            MaxLaborReductionPermille = maxLaborReductionPermille;

            _baseStorageCapByItem = new Dictionary<ItemId, long>();
            foreach (ItemQuantity cap in baseStorageCaps)
            {
                // The first entry wins; content validation reports a second one before this content can exist.
                _baseStorageCapByItem.TryAdd(cap.Item, cap.Quantity);
            }
        }

        /// <summary>The grid's width in tiles: x runs from 0 to <c>GridWidth − 1</c>.</summary>
        public int GridWidth { get; }

        /// <summary>The grid's height in tiles: y runs from 0 to <c>GridHeight − 1</c>.</summary>
        public int GridHeight { get; }

        /// <summary>How many constructions and upgrades may run at once.</summary>
        public int BuilderSlots { get; }

        /// <summary>How many research nodes may run at once: exactly 1 in M0.</summary>
        public int ResearchSlots { get; }

        /// <summary>The base storage cap of every capped item (and of no other item), sorted by item id. Research bonuses add to it.</summary>
        public IReadOnlyList<ItemQuantity> BaseStorageCaps { get; }

        /// <summary>
        /// How many seconds of one settlement may start production cycles; after that the city is dormant and only timers run
        /// (<c>core-instructions/04-simulation-model.md</c>, section 2).
        /// </summary>
        public long MaxOfflineProductionSeconds { get; }

        /// <summary>The shortest cycle content may allow, in seconds: with every labor reduction and all max workers, no cycle is shorter.</summary>
        public long MinCycleSeconds { get; }

        /// <summary>The largest total labor reduction of one building type, in permille (500 = half the labor).</summary>
        public int MaxLaborReductionPermille { get; }

        /// <summary>Looks up the base storage cap of an item.</summary>
        /// <param name="item">The item.</param>
        /// <param name="cap">The base cap, when the item is capped; otherwise 0.</param>
        /// <returns>True when the item is capped and so has a base cap.</returns>
        public bool TryGetBaseStorageCap(ItemId item, out long cap) => _baseStorageCapByItem.TryGetValue(item, out cap);
    }
}
