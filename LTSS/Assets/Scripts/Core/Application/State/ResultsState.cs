using Game.Domain.GameFlow;

namespace Game.Core.Application.State
{
    public sealed class ResultsState : IApplicationState
    {
        private readonly IApplicationStateStore _stateStore;
        private readonly IGameSessionService _gameSessionService;

        public AppStateId StateId => AppStateId.Results;

        public ResultsState(
            IApplicationStateStore stateStore,
            IGameSessionService gameSessionService)
        {
            _stateStore = stateStore;
            _gameSessionService = gameSessionService;
        }

        public void Enter()
        {
            _gameSessionService.CompleteSession();

            _stateStore.SetState(state => state.With(
                appStateId: StateId,
                currentScreen: ScreenId.Results,
                gameFlowStage: GameFlowStage.Results,
                gameFlowPhase: GameFlowPhase.Summary,
                isBusy: false));
        }

        public void Exit()
        {
        }
    }
}
