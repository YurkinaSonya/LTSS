using Game.Domain.GameFlow;

namespace Game.Core.Application.State
{
    public sealed class LoginState : IApplicationState
    {
        private readonly IApplicationStateStore _stateStore;

        public AppStateId StateId => AppStateId.Login;

        public LoginState(IApplicationStateStore stateStore)
        {
            _stateStore = stateStore;
        }

        public void Enter()
        {
            _stateStore.SetState(state => state.With(
                appStateId: StateId,
                currentScreen: ScreenId.Login,
                gameFlowStage: GameFlowStage.MainMenu,
                gameFlowPhase: GameFlowPhase.Idle,
                isBusy: false,
                statusMessage: string.Empty));
        }

        public void Exit()
        {
        }
    }
}
