#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Simulation.State
{
    /// <summary>
    /// How much of a base a trip or a courier's action moves (<c>core-instructions/04-simulation-model.md</c>, section 19): a preset of the
    /// content (always allowed), a percentage 1 to 100 or an exact number of at least 1 (each allowed once a research unlocked its
    /// mode). Its text is the API's form (<c>05</c> → Amounts): the preset id (<c>half</c>), a percentage (<c>50%</c>) or a number
    /// (<c>12</c>); <see cref="TryParse"/> reads it, <see cref="ToString"/> writes it. Immutable; two amounts with the same text are equal.
    /// </summary>
    public sealed class Amount : IEquatable<Amount>
    {
        /// <summary>The most digits an exact number has in the API's form (<c>05</c>): up to 999,999,999,999 goods.</summary>
        public const int MaxExactDigits = 12;

        /// <summary>The largest exact number, the most that <see cref="MaxExactDigits"/> digits write.</summary>
        public const long MaxExact = 999_999_999_999;

        /// <summary>Holds one amount; the factories check its values.</summary>
        private Amount(AmountMode? mode, AmountPresetId presetId, long value)
        {
            Mode = mode;
            PresetId = presetId;
            Value = value;
        }

        /// <summary>The mode the amount needs unlocked: null for a preset (always allowed), else <see cref="AmountMode.Percent"/> or <see cref="AmountMode.Exact"/>.</summary>
        public AmountMode? Mode { get; }

        /// <summary>The preset's id for a preset; the empty id otherwise. The rules check that the content has it.</summary>
        public AmountPresetId PresetId { get; }

        /// <summary>The percentage (1..100) or the exact number (1..<see cref="MaxExact"/>); 0 for a preset, whose percentage is the content's.</summary>
        public long Value { get; }

        /// <summary>A preset of the content, such as <c>all</c> or <c>half</c>.</summary>
        /// <param name="presetId">The preset's id, not empty. Whether the content has it is a rule (<c>INVALID_ARGUMENT</c>), checked when it is used.</param>
        /// <returns>The amount.</returns>
        /// <exception cref="ArgumentException"><paramref name="presetId"/> is the empty id.</exception>
        public static Amount Preset(AmountPresetId presetId) =>
            presetId.Value.Length > 0 ? new Amount(null, presetId, 0) : throw new ArgumentException("A preset amount needs a preset id.", nameof(presetId));

        /// <summary>A percentage of the base (<c>04</c> section 19).</summary>
        /// <param name="percent">1 to 100.</param>
        /// <returns>The amount.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="percent"/> is outside 1..100.</exception>
        public static Amount Percent(int percent) =>
            percent >= 1 && percent <= 100
                ? new Amount(AmountMode.Percent, default, percent)
                : throw new ArgumentOutOfRangeException(nameof(percent), percent, "A percentage is 1 to 100.");

        /// <summary>An exact number of goods (<c>04</c> section 19).</summary>
        /// <param name="quantity">1 to <see cref="MaxExact"/>.</param>
        /// <returns>The amount.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="quantity"/> is outside 1..<see cref="MaxExact"/>.</exception>
        public static Amount Exact(long quantity) =>
            quantity >= 1 && quantity <= MaxExact
                ? new Amount(AmountMode.Exact, default, quantity)
                : throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "An exact amount is 1 to 999,999,999,999.");

        /// <summary>
        /// Reads the API's form (<c>05</c> → Amounts): a preset id of the content-id form (<see cref="ContentIds.IsValid"/>), a percentage
        /// <c>1%</c> to <c>100%</c>, or an exact number <c>1</c> to <see cref="MaxExactDigits"/> digits; numbers without a leading zero, sign
        /// or space. Whether the content has the preset is not checked here.
        /// </summary>
        /// <param name="text">The text, or null.</param>
        /// <param name="amount">The amount, when the text has one of the three forms.</param>
        /// <returns>True when the text is an amount.</returns>
        public static bool TryParse(string? text, [NotNullWhen(true)] out Amount? amount)
        {
            amount = null;
            if (text is null || text.Length == 0)
            {
                return false;
            }

            if (ContentIds.IsValid(text))
            {
                amount = new Amount(null, new AmountPresetId(text), 0);
                return true;
            }

            bool percent = text[text.Length - 1] == '%';
            int digits = percent ? text.Length - 1 : text.Length;
            if (!TryReadNumber(text, digits, out long number))
            {
                return false;
            }

            if (percent ? number > 100 : digits > MaxExactDigits)
            {
                return false;
            }

            amount = percent ? new Amount(AmountMode.Percent, default, number) : new Amount(AmountMode.Exact, default, number);
            return true;
        }

        /// <summary>The API's form: the preset id, <c>50%</c> or <c>12</c>.</summary>
        /// <returns>The text <see cref="TryParse"/> reads back to an equal amount.</returns>
        public override string ToString() =>
            Mode switch
            {
                null => PresetId.Value,
                AmountMode.Percent => Value.ToString(CultureInfo.InvariantCulture) + "%",
                _ => Value.ToString(CultureInfo.InvariantCulture),
            };

        /// <summary>Whether the other amount has the same mode, preset and value (so the same text).</summary>
        /// <param name="other">The amount to compare with.</param>
        /// <returns>True when they are the same amount.</returns>
        public bool Equals(Amount? other) => other is not null && Mode == other.Mode && PresetId == other.PresetId && Value == other.Value;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Amount other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => unchecked((((Mode.HasValue ? (int)Mode.Value + 1 : 0) * 397) ^ PresetId.GetHashCode()) * 397) ^ Value.GetHashCode();

        /// <summary>
        /// Reads the first <paramref name="digits"/> characters of the text as a number from 1 to <see cref="MaxExact"/>: 1 to
        /// <see cref="MaxExactDigits"/> ASCII digits, the first not 0.
        /// </summary>
        private static bool TryReadNumber(string text, int digits, out long number)
        {
            number = 0;
            if (digits < 1 || digits > MaxExactDigits || text[0] == '0')
            {
                return false;
            }

            for (int index = 0; index < digits; index++)
            {
                char character = text[index];
                if (character < '0' || character > '9')
                {
                    return false;
                }

                number = (number * 10) + (character - '0');
            }

            return true;
        }
    }
}
