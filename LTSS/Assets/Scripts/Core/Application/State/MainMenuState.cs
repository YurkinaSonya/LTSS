using Game.Domain.GameFlow;

namespace Game.Core.Application.State
{
    public sealed class MainMenuState : IApplicationState
    {
        private readonly IApplicationStateStore _stateStore;
        private readonly IGameSessionService _gameSessionService;

        public AppStateId StateId => AppStateId.MainMenu;

        public MainMenuState(
            IApplicationStateStore stateStore,
            IGameSessionService gameSessionService)
        {
            _stateStore = stateStore;
            _gameSessionService = gameSessionService;
        }

        public void Enter()
        {
            _gameSessionService.ResetSession();

            _stateStore.SetState(state => state.With(
                appStateId: StateId,
                currentScreen: ScreenId.MainMenu,
                gameFlowStage: GameFlowStage.MainMenu,
                gameFlowPhase: GameFlowPhase.Idle,
                isBusy: false,
                lastError: string.Empty));
        }

        public void Exit()
        {
        }
    }
}
