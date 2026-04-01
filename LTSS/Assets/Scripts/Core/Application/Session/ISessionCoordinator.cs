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
        void ClearSession(string reason = null);
    }
}
