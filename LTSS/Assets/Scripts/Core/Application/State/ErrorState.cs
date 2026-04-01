namespace Game.Core.Application.State
{
    public sealed class ErrorState : IApplicationState
    {
        private readonly IApplicationStateStore _stateStore;

        public AppStateId StateId => AppStateId.Error;

        public ErrorState(IApplicationStateStore stateStore)
        {
            _stateStore = stateStore;
        }

        public void Enter()
        {
            _stateStore.SetState(state => state.With(
                appStateId: StateId,
                currentScreen: ScreenId.Error,
                isBusy: false,
                statusMessage: string.Empty));
        }

        public void Exit()
        {
        }
    }
}
