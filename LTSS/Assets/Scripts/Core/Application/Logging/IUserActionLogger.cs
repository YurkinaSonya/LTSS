using System.Collections.Generic;

namespace Game.Core.Application.Logging
{
    public interface IUserActionLogger
    {
        IReadOnlyList<UserActionLogEntry> Entries { get; }

        void Log(
            UserActionType type,
            string name,
            IDictionary<string, string> metadata = null);
    }
}
