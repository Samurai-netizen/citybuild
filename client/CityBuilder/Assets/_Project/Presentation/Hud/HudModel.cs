#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using CityBuilder.Client.Core;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.Hud
{
    /// <summary>
    /// What the HUD says, without a screen: one <see cref="ItemChipModel"/> per item of the city's eras, the workers (free / provided), the builders
    /// (busy / slots), the <see cref="ResearchRingModel"/>, the era's name and the connection indicator. It follows the displayed city
    /// (<see cref="Update"/>, once a second) and the connection state, and reads nothing else, so it can only show what the engine computed from the
    /// server's snapshot. It decides nothing and sends nothing. <see cref="Changed"/> is raised only when a value differs, so a second in which only the
    /// clock moved redraws nothing.
    /// </summary>
    public sealed class HudModel
    {
        /// <summary>The game content the names, caps and rates come from.</summary>
        private readonly GameContent _content;

        /// <summary>The chips, replaced as a whole when any of them changed.</summary>
        private IReadOnlyList<ItemChipModel> _chips;

        /// <summary>Creates the model for <paramref name="city"/> and <paramref name="connection"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="content"/> or <paramref name="city"/> is null.</exception>
        public HudModel(GameContent content, CityState city, ConnectionState connection)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            _chips = MakeChips(content, city);
            WorkersText = WorkersTextFor(content, city);
            BuildersText = BuildersTextFor(content, city);
            Research = ResearchRingModel.Create(content, city);
            EraName = EraNameFor(content, city);
            ConnectionText = ConnectionTextFor(connection);
            ConnectionLevel = ConnectionLevelFor(connection);
        }

        /// <summary>Raised after any value changed; never when an update changes nothing.</summary>
        public event Action? Changed;

        /// <summary>The item chips, in the content's order.</summary>
        public IReadOnlyList<ItemChipModel> Chips => _chips;

        /// <summary>The free workers out of the workers the houses provide, such as <c>6 / 8</c>.</summary>
        public string WorkersText { get; private set; }

        /// <summary>The busy builders out of the builder slots, such as <c>1 / 2</c>.</summary>
        public string BuildersText { get; private set; }

        /// <summary>The research ring.</summary>
        public ResearchRingModel Research { get; private set; }

        /// <summary>The name of the city's era, such as <c>Early Middle Ages</c>.</summary>
        public string EraName { get; private set; }

        /// <summary>The connection in a word, such as <c>Online</c> or <c>Offline</c>.</summary>
        public string ConnectionText { get; private set; }

        /// <summary>How the connection indicator is coloured.</summary>
        public ConnectionLevel ConnectionLevel { get; private set; }

        /// <summary>
        /// Makes every value again for <paramref name="city"/> and <paramref name="connection"/>. Raises <see cref="Changed"/> when any differs.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
        public void Update(CityState city, ConnectionState connection)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            bool changed = false;

            IReadOnlyList<ItemChipModel> chips = MakeChips(_content, city);
            bool chipsChanged = chips.Count != _chips.Count;
            for (int index = 0; !chipsChanged && index < chips.Count; index++)
            {
                chipsChanged = !chips[index].Equals(_chips[index]);
            }

            if (chipsChanged)
            {
                _chips = chips;
                changed = true;
            }

            string workers = WorkersTextFor(_content, city);
            if (workers != WorkersText)
            {
                WorkersText = workers;
                changed = true;
            }

            string builders = BuildersTextFor(_content, city);
            if (builders != BuildersText)
            {
                BuildersText = builders;
                changed = true;
            }

            ResearchRingModel research = ResearchRingModel.Create(_content, city);
            if (!research.Equals(Research))
            {
                Research = research;
                changed = true;
            }

            string era = EraNameFor(_content, city);
            if (era != EraName)
            {
                EraName = era;
                changed = true;
            }

            string connectionText = ConnectionTextFor(connection);
            if (connectionText != ConnectionText)
            {
                ConnectionText = connectionText;
                changed = true;
            }

            ConnectionLevel level = ConnectionLevelFor(connection);
            if (level != ConnectionLevel)
            {
                ConnectionLevel = level;
                changed = true;
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>One chip for every item that belongs to the city's era or an earlier one, in the content's order.</summary>
        private static IReadOnlyList<ItemChipModel> MakeChips(GameContent content, CityState city)
        {
            var chips = new List<ItemChipModel>();
            foreach (ItemDefinition item in content.Items)
            {
                if (item.Era <= city.Era)
                {
                    chips.Add(ItemChipModel.Create(content, city, item));
                }
            }

            return chips;
        }

        /// <summary><c>6 / 8</c>: the workers no building holds, out of the workers the houses provide.</summary>
        private static string WorkersTextFor(GameContent content, CityState city)
        {
            return Fraction(CityQueries.FreeWorkers(city, content), CityQueries.WorkersProvided(city, content));
        }

        /// <summary><c>1 / 2</c>: the builders at work, out of the builder slots of the rules.</summary>
        private static string BuildersTextFor(GameContent content, CityState city)
        {
            return Fraction(CityQueries.BuildersBusy(city), content.Rules.BuilderSlots);
        }

        /// <summary>The era's name from the content; <c>Era 2</c> when the content does not know the number.</summary>
        private static string EraNameFor(GameContent content, CityState city)
        {
            return content.TryGetEra(city.Era, out EraDefinition? era)
                ? era.Name
                : "Era " + city.Era.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The connection state in one word for the indicator.</summary>
        private static string ConnectionTextFor(ConnectionState connection)
        {
            switch (connection)
            {
                case ConnectionState.Connecting:
                    return "Connecting";
                case ConnectionState.Connected:
                    return "Online";
                case ConnectionState.Offline:
                    return "Offline";
                case ConnectionState.Unauthorized:
                    return "Signed out";
                case ConnectionState.IncompatibleVersion:
                    return "Update needed";
                default:
                    return "Server problem";
            }
        }

        /// <summary>Connected is ok, connecting is pending, everything else is a problem.</summary>
        private static ConnectionLevel ConnectionLevelFor(ConnectionState connection)
        {
            switch (connection)
            {
                case ConnectionState.Connected:
                    return ConnectionLevel.Ok;
                case ConnectionState.Connecting:
                    return ConnectionLevel.Pending;
                default:
                    return ConnectionLevel.Problem;
            }
        }

        /// <summary><c>part / whole</c> with plain digits.</summary>
        private static string Fraction(long part, long whole)
        {
            return part.ToString(CultureInfo.InvariantCulture) + " / " + whole.ToString(CultureInfo.InvariantCulture);
        }
    }
}
