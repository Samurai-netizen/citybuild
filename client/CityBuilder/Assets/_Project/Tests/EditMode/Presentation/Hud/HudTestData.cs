#nullable enable

using System.Collections.Generic;
using CityBuilder.Client.Tests.EditMode.Core;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Hud
{
    /// <summary>The cities the HUD tests show: small cities of Era I made from the real content of the fixtures.</summary>
    internal static class HudTestData
    {
        /// <summary>The real content of the fixtures.</summary>
        public static GameContent Content() => CityTestData.Content();

        /// <summary>A city of Era I at <paramref name="clock"/> with the given stock, buildings, completed research and running research.</summary>
        public static CityState City(
            long clock,
            long coins,
            long logs,
            long planks,
            IEnumerable<Building> buildings,
            string[]? research = null,
            ActiveResearch? active = null)
        {
            var stock = new List<KeyValuePair<ItemId, long>>
            {
                new KeyValuePair<ItemId, long>(new ItemId("coins"), coins),
                new KeyValuePair<ItemId, long>(new ItemId("logs"), logs),
                new KeyValuePair<ItemId, long>(new ItemId("planks"), planks),
            };
            var completed = new List<ResearchId>();
            foreach (string id in research ?? new string[0])
            {
                completed.Add(new ResearchId(id));
            }

            return new CityState(clock, 1, 100, stock, new List<Building>(buildings), completed, active);
        }

        /// <summary>A finished cottage, which provides workers.</summary>
        public static Building House(int id, int x)
        {
            return new Building(id, new BuildingTypeId("house"), x, 0, 1, 0, BuildingPhase.Idle, 0, null, 0, 0, 0, 0);
        }

        /// <summary>A cottage under construction, which holds one builder.</summary>
        public static Building Constructing(int id, int x)
        {
            return new Building(id, new BuildingTypeId("house"), x, 0, 1, 0, BuildingPhase.Constructing, 0, 100, 0, 0, 0, 0);
        }

        /// <summary>A producer of <paramref name="typeId"/> at level 1 running its first cycle of <paramref name="labor"/> with <paramref name="workers"/> workers.</summary>
        public static Building Producer(int id, int x, string typeId, int workers, long labor)
        {
            long cycleSeconds = (labor + workers - 1) / workers;
            return new Building(id, new BuildingTypeId(typeId), x, 0, 1, workers, BuildingPhase.Producing, 0, cycleSeconds, 0, labor, labor, 0);
        }

        /// <summary>The research <paramref name="id"/> from <paramref name="startedAt"/> to <paramref name="endsAt"/>.</summary>
        public static ActiveResearch Research(string id, long startedAt, long endsAt)
        {
            return new ActiveResearch(new ResearchId(id), startedAt, endsAt);
        }
    }
}
