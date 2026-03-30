using System;
using System.Collections.Generic;

namespace Game.Core.Application.Logging
{
    public sealed class UserActionLogEntry
    {
        private readonly Dictionary<string, string> _metadata;

        public DateTime TimestampUtc { get; }
        public UserActionType Type { get; }
        public string Name { get; }
        public IReadOnlyDictionary<string, string> Metadata => _metadata;

        public UserActionLogEntry(
            UserActionType type,
            string name,
            IDictionary<string, string> metadata = null)
        {
            TimestampUtc = DateTime.UtcNow;
            Type = type;
            Name = string.IsNullOrEmpty(name) ? type.ToString() : name;
            _metadata = metadata != null
                ? new Dictionary<string, string>(metadata)
                : new Dictionary<string, string>();
        }
    }
}
