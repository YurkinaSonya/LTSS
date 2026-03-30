namespace Game.Core.Application.Navigation
{
    public interface IApplicationNavigationService
    {
        void MoveTo(AppStateId stateId, string reason = null);
        void GoToMainMenu(string reason = null);
        void StartGameplay(string reason = null);
        void ShowResults(string reason = null);
        void ShowError(string message, string reason = null);
    }
}
