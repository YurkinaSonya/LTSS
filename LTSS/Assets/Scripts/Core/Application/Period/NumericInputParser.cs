using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Application.Periods
{
    public static class NumericInputParser
    {
        public static void Configure(InputField inputField)
        {
            if (inputField == null)
            {
                return;
            }

            inputField.contentType = InputField.ContentType.Custom;
            inputField.characterValidation = InputField.CharacterValidation.None;
            inputField.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
            inputField.onValidateInput = ValidateCharacter;
        }

        public static bool TryParseNonNegativeAmount(string rawAmount, out double amount)
        {
            amount = 0d;
            var normalized = Normalize(rawAmount);

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return true;
            }

            if (IsIncompleteDecimalInput(normalized))
            {
                return false;
            }

            return double.TryParse(
                       normalized,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out amount)
                   && amount >= 0d;
        }

        public static bool ShouldPreserveFocusedInput(string currentText, string formattedValue)
        {
            var currentNormalized = Normalize(currentText);
            var formattedNormalized = Normalize(formattedValue);

            if (string.IsNullOrWhiteSpace(currentNormalized))
            {
                return false;
            }

            if (IsIncompleteDecimalInput(currentNormalized))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(formattedNormalized)
                && double.TryParse(
                    currentNormalized,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var zeroCandidate)
                && System.Math.Abs(zeroCandidate) <= 0.0000001d)
            {
                return true;
            }

            if (!currentNormalized.Contains("."))
            {
                return false;
            }

            if (!double.TryParse(
                    currentNormalized,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var currentAmount))
            {
                return false;
            }

            if (!double.TryParse(
                    formattedNormalized,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var formattedAmount))
            {
                return false;
            }

            return System.Math.Abs(currentAmount - formattedAmount) <= 0.0000001d
                   && !string.Equals(currentNormalized, formattedNormalized, System.StringComparison.Ordinal);
        }

        private static char ValidateCharacter(string currentText, int charIndex, char addedChar)
        {
            if (char.IsDigit(addedChar))
            {
                return addedChar;
            }

            if (addedChar != '.' && addedChar != ',')
            {
                return '\0';
            }

            var safeText = currentText ?? string.Empty;
            return safeText.IndexOf('.') >= 0 || safeText.IndexOf(',') >= 0
                ? '\0'
                : addedChar;
        }

        private static string Normalize(string rawAmount)
        {
            var normalized = (rawAmount ?? string.Empty)
                .Trim()
                .Replace(" ", string.Empty)
                .Replace("\u00A0", string.Empty)
                .Replace(',', '.');

            return normalized.StartsWith(".")
                ? $"0{normalized}"
                : normalized;
        }

        private static bool IsIncompleteDecimalInput(string normalized)
        {
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            return normalized.EndsWith(".", System.StringComparison.Ordinal);
        }
    }
}
