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
        /// <summary>A city of Era I with the given stock, completed research and buildings under construction (each takes a builder).</summary>
        public static CityState City(long coins, long logs, long planks, string[] research, int constructing)
        {
            var stock = new List<KeyValuePair<ItemId, long>>
            {
                new KeyValuePair<ItemId, long>(new ItemId("coins"), coins),
                new KeyValuePair<ItemId, long>(new ItemId("logs"), logs),
                new KeyValuePair<ItemId, long>(new ItemId("planks"), planks),
            };
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

            return new CityState(0, 1, constructing + 1, stock, buildings, completed, null);
        }

        /// <summary>The start of a new game: 700 coins, 40 logs, 10 planks, no research, free builders.</summary>
        public static CityState StartCity() => City(700, 40, 10, Array.Empty<string>(), 0);

        /// <summary>The real content of the fixtures.</summary>
        public static GameContent Content() => CityTestData.Content();
    }
}
