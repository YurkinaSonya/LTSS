using System;

namespace Game.Core.Application.Session
{
    public interface ISessionCoordinator
    {
        ClientRuntimeState CurrentRuntime { get; }
        event Action<ClientRuntimeState> RuntimeChanged;

        void RestoreIfPossible(AppStateId fallbackState = AppStateId.Login);
        void Login(string login, string password);
        void LoadBootstrap();
        void RefreshBootstrapInPlace(Action<bool, string> onCompleted = null);
        void UpdateLocalRunProgress(int currentPeriodNumber, RunLifecycleStatus runStatus);
        void CompleteRunLocally(string statusMessage = null, string reason = null);
        void ClearSession(string reason = null);
    }
}
