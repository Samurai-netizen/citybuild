#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using CityBuilder.Simulation.State;

namespace CityBuilder.Client.Presentation.Build
{
    /// <summary>
    /// The build menu's state without a screen: one <see cref="BuildCardModel"/> per building type of the content, in the content's order, and
    /// which type is selected. The cards follow the latest city the client has (<see cref="Update"/>): a research that unlocks a type, a freed
    /// builder or a payment shows within a second. Selecting is a UX state for the placement flow (M0-46); it decides nothing and never calls
    /// the server. A locked card cannot be selected; an unaffordable one can (the hint warns, the server decides).
    /// </summary>
    public sealed class BuildMenuModel
    {
        /// <summary>The game content the cards are made from.</summary>
        private readonly GameContent _content;

        /// <summary>The cards, replaced as a whole when any of them changed.</summary>
        private IReadOnlyList<BuildCardModel> _cards;

        /// <summary>Creates the menu for <paramref name="city"/>: one card for every building type of <paramref name="content"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="content"/> or <paramref name="city"/> is null.</exception>
        public BuildMenuModel(GameContent content, CityState city)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _cards = MakeCards(content, city ?? throw new ArgumentNullException(nameof(city)));
        }

        /// <summary>Raised after <see cref="Cards"/> changed (a text, a lock or a hint); never when an update changes nothing.</summary>
        public event Action? CardsChanged;

        /// <summary>Raised after <see cref="Selected"/> changed.</summary>
        public event Action? SelectionChanged;

        /// <summary>The cards, one per building type, in the content's order.</summary>
        public IReadOnlyList<BuildCardModel> Cards => _cards;

        /// <summary>The type the player picked, or null when none is.</summary>
        public BuildingTypeId? Selected { get; private set; }

        /// <summary>
        /// Makes the cards again for <paramref name="city"/>. Raises <see cref="CardsChanged"/> when any card differs. A selected type that is
        /// locked now (a city that went back to an earlier state) is deselected.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="city"/> is null.</exception>
        public void Update(CityState city)
        {
            IReadOnlyList<BuildCardModel> cards = MakeCards(_content, city ?? throw new ArgumentNullException(nameof(city)));
            bool changed = cards.Count != _cards.Count;
            for (int index = 0; !changed && index < cards.Count; index++)
            {
                changed = !cards[index].Equals(_cards[index]);
            }

            if (!changed)
            {
                return;
            }

            _cards = cards;
            if (Selected.HasValue && IsLockedOrMissing(Selected.Value))
            {
                Selected = null;
                SelectionChanged?.Invoke();
            }

            CardsChanged?.Invoke();
        }

        /// <summary>
        /// A tap on a card: selects its type, or deselects it when it is the selected one. A locked or unknown type changes nothing.
        /// </summary>
        /// <returns>True when the selection changed.</returns>
        public bool TryToggle(BuildingTypeId typeId)
        {
            if (IsLockedOrMissing(typeId))
            {
                return false;
            }

            Selected = Selected.HasValue && Selected.Value.Equals(typeId) ? (BuildingTypeId?)null : typeId;
            SelectionChanged?.Invoke();
            return true;
        }

        /// <summary>Deselects the type, if there is one (the placement flow's cancel).</summary>
        public void ClearSelection()
        {
            if (!Selected.HasValue)
            {
                return;
            }

            Selected = null;
            SelectionChanged?.Invoke();
        }

        /// <summary>Whether the type has no card or its card is locked: neither can be selected.</summary>
        private bool IsLockedOrMissing(BuildingTypeId typeId)
        {
            for (int index = 0; index < _cards.Count; index++)
            {
                if (_cards[index].TypeId.Equals(typeId))
                {
                    return _cards[index].IsLocked;
                }
            }

            return true;
        }

        /// <summary>One card per building type of the content for <paramref name="city"/>.</summary>
        private static IReadOnlyList<BuildCardModel> MakeCards(GameContent content, CityState city)
        {
            var cards = new List<BuildCardModel>(content.Buildings.Count);
            for (int index = 0; index < content.Buildings.Count; index++)
            {
                cards.Add(BuildCardModel.Create(content, city, content.Buildings[index]));
            }

            return cards;
        }
    }
}
