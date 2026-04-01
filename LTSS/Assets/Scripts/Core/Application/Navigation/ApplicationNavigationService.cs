using System.Collections.Generic;
using Game.Core.Application.Logging;
using Game.Core.Application.State;

namespace Game.Core.Application.Navigation
{
    public sealed class ApplicationNavigationService : IApplicationNavigationService
    {
        private readonly ApplicationStateMachine _stateMachine;
        private readonly IApplicationStateStore _stateStore;
        private readonly IPopupNavigationService _popupNavigation;
        private readonly IUserActionLogger _userActionLogger;

        public ApplicationNavigationService(
            ApplicationStateMachine stateMachine,
            IApplicationStateStore stateStore,
            IPopupNavigationService popupNavigation,
            IUserActionLogger userActionLogger)
        {
            _stateMachine = stateMachine;
            _stateStore = stateStore;
            _popupNavigation = popupNavigation;
            _userActionLogger = userActionLogger;
        }

        public void MoveTo(AppStateId stateId, string reason = null)
        {
            _popupNavigation.Clear("state_change");
            LogNavigation(stateId, reason);
            _stateMachine.MoveTo(stateId);
        }

        public void GoToLogin(string reason = null)
        {
            MoveTo(AppStateId.Login, reason);
        }

        public void GoToMainMenu(string reason = null)
        {
            MoveTo(AppStateId.MainMenu, reason);
        }

        public void StartGameplay(string reason = null)
        {
            MoveTo(AppStateId.Gameplay, reason);
        }

        public void ShowSessionReady(string reason = null)
        {
            MoveTo(AppStateId.SessionReady, reason);
        }

        public void ShowResults(string reason = null)
        {
            MoveTo(AppStateId.Results, reason);
        }

        public void ShowError(string message, string reason = null)
        {
            _stateStore.SetState(state => state.With(
                lastError: message ?? "Неизвестная ошибка."));

            MoveTo(AppStateId.FatalError, reason);
        }

        private void LogNavigation(AppStateId stateId, string reason)
        {
            var metadata = new Dictionary<string, string>
            {
                { "state", stateId.ToString() }
            };

            if (!string.IsNullOrEmpty(reason))
            {
                metadata["reason"] = reason;
            }

            _userActionLogger.Log(
                UserActionType.Navigation,
                "application_state_transition",
                metadata);
        }
    }
}
