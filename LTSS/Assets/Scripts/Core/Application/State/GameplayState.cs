using Game.Domain.GameFlow;

namespace Game.Core.Application.State
{
    public sealed class GameplayState : IApplicationState
    {
        private readonly IApplicationStateStore _stateStore;
        private readonly IGameSessionService _gameSessionService;

        public AppStateId StateId => AppStateId.Gameplay;

        public GameplayState(
            IApplicationStateStore stateStore,
            IGameSessionService gameSessionService)
        {
            _stateStore = stateStore;
            _gameSessionService = gameSessionService;
        }

        public void Enter()
        {
            if (!_gameSessionService.Current.IsActive)
            {
                _gameSessionService.StartNewSession();
            }

            _gameSessionService.SetStage(GameFlowStage.Gameplay);
            _gameSessionService.SetPhase(GameFlowPhase.Preparation);

            _stateStore.SetState(state => state.With(
                appStateId: StateId,
                currentScreen: ScreenId.Gameplay,
                isBusy: false,
                statusMessage: string.Empty,
                lastError: string.Empty));
        }

        public void Exit()
        {
        }
    }
}
