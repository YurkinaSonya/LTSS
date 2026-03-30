using Game.Domain.GameFlow;

namespace Game.Core.Application.State
{
    public sealed class BootstrappingState : IApplicationState
    {
        private readonly IApplicationStateStore _stateStore;

        public AppStateId StateId => AppStateId.Bootstrapping;

        public BootstrappingState(IApplicationStateStore stateStore)
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
                lastError: string.Empty));
        }

        public void Exit()
        {
            _stateStore.SetState(state => state.With(isBusy: false));
        }
    }
}
