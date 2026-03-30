using System.Collections.Generic;

namespace Game.Domain.GameFlow
{
    public sealed class GameMetricBag
    {
        private readonly Dictionary<string, double> _numericMetrics;
        private readonly Dictionary<string, string> _stringMetrics;

        public IReadOnlyDictionary<string, double> NumericMetrics => _numericMetrics;
        public IReadOnlyDictionary<string, string> StringMetrics => _stringMetrics;

        public GameMetricBag()
        {
            _numericMetrics = new Dictionary<string, double>();
            _stringMetrics = new Dictionary<string, string>();
        }

        private GameMetricBag(
            Dictionary<string, double> numericMetrics,
            Dictionary<string, string> stringMetrics)
        {
            _numericMetrics = numericMetrics;
            _stringMetrics = stringMetrics;
        }

        public void SetNumber(string key, double value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            _numericMetrics[key] = value;
        }

        public void Increment(string key, double delta = 1d)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var currentValue = GetNumberOrDefault(key);
            _numericMetrics[key] = currentValue + delta;
        }

        public double GetNumberOrDefault(string key, double defaultValue = 0d)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            return _numericMetrics.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public void SetText(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            _stringMetrics[key] = value ?? string.Empty;
        }

        public string GetTextOrDefault(string key, string defaultValue = "")
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            return _stringMetrics.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public GameMetricBag Clone()
        {
            return new GameMetricBag(
                new Dictionary<string, double>(_numericMetrics),
                new Dictionary<string, string>(_stringMetrics));
        }
    }
}
