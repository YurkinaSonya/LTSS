using Game.Domain.GameFlow;

namespace Game.Core.Application.State
{
    public sealed class LoadingSessionState : IApplicationState
    {
        private readonly IApplicationStateStore _stateStore;

        public AppStateId StateId => AppStateId.LoadingSession;

        public LoadingSessionState(IApplicationStateStore stateStore)
        {
            _stateStore = stateStore;
        }

        public void Enter()
        {
            _stateStore.SetState(state => state.With(
                appStateId: StateId,
                currentScreen: ScreenId.Loading,
                gameFlowStage: GameFlowStage.Bootstrap,
                gameFlowPhase: GameFlowPhase.Preparation,
                isBusy: true,
                statusMessage: "Загрузка сессии...",
                lastError: string.Empty));
        }

        public void Exit()
        {
        }
    }
}
