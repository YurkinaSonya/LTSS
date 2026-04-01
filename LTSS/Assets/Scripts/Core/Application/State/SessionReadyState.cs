using Game.Domain.GameFlow;

namespace Game.Core.Application.State
{
    public sealed class SessionReadyState : IApplicationState
    {
        private readonly IApplicationStateStore _stateStore;

        public AppStateId StateId => AppStateId.SessionReady;

        public SessionReadyState(IApplicationStateStore stateStore)
        {
            _stateStore = stateStore;
        }

        public void Enter()
        {
            _stateStore.SetState(state => state.With(
                appStateId: StateId,
                currentScreen: ScreenId.SessionReady,
                gameFlowStage: GameFlowStage.MainMenu,
                gameFlowPhase: GameFlowPhase.Idle,
                isBusy: false,
                statusMessage: string.Empty,
                lastError: string.Empty));
        }

        public void Exit()
        {
        }
    }
}
