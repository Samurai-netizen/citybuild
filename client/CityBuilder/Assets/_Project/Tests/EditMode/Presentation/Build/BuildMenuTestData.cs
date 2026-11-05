#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Client.Tests.EditMode.Core;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Build
{
    /// <summary>The cities the build menu tests judge cards by: the real content of the fixtures and small cities made from it.</summary>
    internal static class BuildMenuTestData
    {
        /// <summary>
        /// A city of Era I with the given coins in the purse, goods in a warehouse (<see cref="CityTestData.GoodsStore"/>), completed research and
        /// buildings under construction (each takes a builder).
        /// </summary>
        public static CityState City(long coins, long logs, long planks, string[] research, int constructing)
        {
            var buildings = new List<Building>();
            for (int id = 1; id <= constructing; id++)
            {
                buildings.Add(new Building(id, new BuildingTypeId("house"), id, 0, 1, 0, BuildingPhase.Constructing, 0, 100, 0, 0, 0, 0));
            }

            var completed = new List<ResearchId>();
            foreach (string id in research)
            {
                completed.Add(new ResearchId(id));
            }

            buildings.AddRange(CityTestData.GoodsStore(logs, planks));
            return new CityState(0, 1, CityTestData.GoodsStoreId + 1, coins, buildings, completed, null);
        }

        /// <summary>The start of a new game (CR-0008): 800 coins in the purse, no goods, no research, free builders.</summary>
        public static CityState StartCity() => City(800, 0, 0, Array.Empty<string>(), 0);

        /// <summary>The real content of the fixtures.</summary>
        public static GameContent Content() => CityTestData.Content();
    }
}
