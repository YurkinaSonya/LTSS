using System.Collections.Generic;
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
            _logger.Info($"UserAction: {entry.Type} / {entry.Name}");
        }
    }
}
