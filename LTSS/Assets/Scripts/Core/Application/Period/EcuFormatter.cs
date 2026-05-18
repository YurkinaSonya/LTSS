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

        public static string FormatGrowthPercent(double? multiplier, string fallback = "-")
        {
            if (!multiplier.HasValue)
            {
                return fallback;
            }

            var percent = (multiplier.Value - 1d) * 100d;
            var prefix = percent > 0d
                ? "+"
                : string.Empty;
            return $"{prefix}{percent.ToString("0.###", CultureInfo.InvariantCulture)}%";
        }
    }
}
