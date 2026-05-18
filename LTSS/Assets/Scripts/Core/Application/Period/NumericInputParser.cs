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

            return double.TryParse(
                       normalized,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out amount)
                   && amount >= 0d;
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
            return (rawAmount ?? string.Empty)
                .Trim()
                .Replace(" ", string.Empty)
                .Replace("\u00A0", string.Empty)
                .Replace(',', '.');
        }
    }
}
