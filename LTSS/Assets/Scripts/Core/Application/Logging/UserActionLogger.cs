using System.Collections.Generic;
using System.Text;
using Game.Core.Events;

namespace Game.Core.Application.Logging
{
    public sealed class UserActionLogger : IUserActionLogger
    {
        private const int MaxEntries = 512;

        private readonly List<UserActionLogEntry> _entries = new List<UserActionLogEntry>();
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppLogger _logger;

        public IReadOnlyList<UserActionLogEntry> Entries => _entries;

        public UserActionLogger(
            IEventAggregator eventAggregator,
            IAppLogger logger)
        {
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public void Log(
            UserActionType type,
            string name,
            IDictionary<string, string> metadata = null)
        {
            var entry = new UserActionLogEntry(type, name, metadata);

            _entries.Add(entry);

            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(0);
            }

            _eventAggregator.Publish(new EventsProvider.UserActionLoggedEvent(entry));
            _logger.Info(BuildLogMessage(entry));
        }

        private static string BuildLogMessage(UserActionLogEntry entry)
        {
            if (entry == null)
            {
                return "UserAction: Unknown";
            }

            var builder = new StringBuilder();
            builder.Append("UserAction: ")
                .Append(entry.Type)
                .Append(" / ")
                .Append(entry.Name);

            if (entry.Metadata == null || entry.Metadata.Count == 0)
            {
                return builder.ToString();
            }

            var hasMetadata = false;

            foreach (var item in entry.Metadata)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    continue;
                }

                var value = SanitizeMetadataValue(item.Value);

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                builder.Append(hasMetadata ? ", " : " | ");
                builder.Append(item.Key).Append('=').Append(value);
                hasMetadata = true;
            }

            return builder.ToString();
        }

        private static string SanitizeMetadataValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var compact = value
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ')
                .Trim();

            while (compact.Contains("  "))
            {
                compact = compact.Replace("  ", " ");
            }

            const int maxLength = 320;

            if (compact.Length > maxLength)
            {
                compact = compact.Substring(0, maxLength) + "...";
            }

            return compact;
        }
    }
}
