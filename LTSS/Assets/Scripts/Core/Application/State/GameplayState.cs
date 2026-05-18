using Game.Domain.GameFlow;
using Game.Core.Application.Periods;
using Game.Core.Application.Session;
using Zenject;

namespace Game.Core.Application.State
{
    public sealed class GameplayState : IApplicationState
    {
        private readonly IApplicationStateStore _stateStore;
        private readonly IGameSessionService _gameSessionService;
        private readonly LazyInject<ISessionFlowCoordinator> _sessionFlowCoordinator;

        public AppStateId StateId => AppStateId.Gameplay;

        public GameplayState(
            IApplicationStateStore stateStore,
            IGameSessionService gameSessionService,
            LazyInject<ISessionFlowCoordinator> sessionFlowCoordinator)
        {
            _stateStore = stateStore;
            _gameSessionService = gameSessionService;
            _sessionFlowCoordinator = sessionFlowCoordinator;
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
                currentScreen: ScreenId.Loading,
                isBusy: true,
                statusMessage: "Подготовка сценария...",
                lastError: string.Empty));

            _sessionFlowCoordinator?.Value?.StartOrResume();
        }

        public void Exit()
        {
        }
    }
}
