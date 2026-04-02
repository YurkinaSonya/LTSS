using System.Globalization;

namespace Game.Core.Application.Periods
{
    public static class EcuFormatter
    {
        private const string CurrencyCode = "ECU";

        public static string FormatAmount(double value)
        {
            return $"{value.ToString("0.##", CultureInfo.InvariantCulture)} {CurrencyCode}";
        }

        public static string FormatScalar(double? value, string fallback = "-")
        {
            return value.HasValue
                ? value.Value.ToString("0.###", CultureInfo.InvariantCulture)
                : fallback;
        }
    }
}
