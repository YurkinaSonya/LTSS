using Game.Domain.GameFlow;
using Game.Core.Application.Periods;
using Zenject;

namespace Game.Core.Application.State
{
    public sealed class GameplayState : IApplicationState
    {
        private readonly IApplicationStateStore _stateStore;
        private readonly IGameSessionService _gameSessionService;
        private readonly LazyInject<IPeriodGameplayService> _periodGameplayService;

        public AppStateId StateId => AppStateId.Gameplay;

        public GameplayState(
            IApplicationStateStore stateStore,
            IGameSessionService gameSessionService,
            LazyInject<IPeriodGameplayService> periodGameplayService)
        {
            _stateStore = stateStore;
            _gameSessionService = gameSessionService;
            _periodGameplayService = periodGameplayService;
        }

        public void Enter()
        {
            if (!_gameSessionService.Current.IsActive)
            {
                _gameSessionService.StartNewSession();
            }

            _gameSessionService.SetStage(GameFlowStage.Gameplay);
            _gameSessionService.SetPhase(GameFlowPhase.LoadingData);

            _stateStore.SetState(state => state.With(
                appStateId: StateId,
                currentScreen: ScreenId.Gameplay,
                isBusy: false,
                statusMessage: string.Empty,
                lastError: string.Empty));

            _periodGameplayService?.Value?.ActivateCurrentPeriod();
        }

        public void Exit()
        {
        }
    }
}
