using Game.Core.Application.Logging;
using Game.Core.Application.State;
using Game.Domain.GameFlow;

namespace Game.Core.Events
{
    /// <summary>
    /// Specific container of event-like classes for EventAggregator.
    /// There defines this classes as basical containers of data, 
    /// transferring cross parts of system by events.
    /// </summary>
    public static class EventsProvider
    {
        public class CloseAllPopupsEvent
        {

        }

        public class OpenScreenEvent
        {
            public Enums.GameStage Stage;
            public OpenScreenEvent(Enums.GameStage stage)
            {
                Stage = stage;
            }
        }

        public class ChangeBlurEvent
        {
            public readonly bool IsActive;
            public ChangeBlurEvent(bool isActive)
            {
                IsActive = isActive;
            }
        }

        public sealed class ApplicationStateChangedEvent
        {
            public ApplicationStateSnapshot State { get; }

            public ApplicationStateChangedEvent(ApplicationStateSnapshot state)
            {
                State = state;
            }
        }

        public sealed class GameSessionChangedEvent
        {
            public GameSessionSnapshot Session { get; }

            public GameSessionChangedEvent(GameSessionSnapshot session)
            {
                Session = session;
            }
        }

        public sealed class UserActionLoggedEvent
        {
            public UserActionLogEntry Entry { get; }

            public UserActionLoggedEvent(UserActionLogEntry entry)
            {
                Entry = entry;
            }
        }

        public sealed class ApiRequestCompletedEvent
        {
            public string Method { get; }
            public string Url { get; }
            public bool IsSuccess { get; }
            public long StatusCode { get; }
            public string Error { get; }

            public ApiRequestCompletedEvent(
                string method,
                string url,
                bool isSuccess,
                long statusCode,
                string error)
            {
                Method = method ?? string.Empty;
                Url = url ?? string.Empty;
                IsSuccess = isSuccess;
                StatusCode = statusCode;
                Error = error ?? string.Empty;
            }
        }
    }
}
