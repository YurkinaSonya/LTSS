using Game.Core.Application.Logging;
using Game.Core.Application.Navigation;
using Game.Core.Application.State;
using Zenject;

namespace Game.Core.Application.Bootstrap
{
    public sealed class ApplicationBootstrapper : IInitializable
    {
        private readonly ApplicationBootstrapSettings _settings;
        private readonly ApplicationStateMachine _stateMachine;
        private readonly IApplicationNavigationService _navigation;
        private readonly IApplicationStateStore _stateStore;
        private readonly IGameSessionService _gameSessionService;
        private readonly IUserActionLogger _userActionLogger;
        private readonly IAppLogger _logger;

        public ApplicationBootstrapper(
            ApplicationBootstrapSettings settings,
            ApplicationStateMachine stateMachine,
            IApplicationNavigationService navigation,
            IApplicationStateStore stateStore,
            IGameSessionService gameSessionService,
            IUserActionLogger userActionLogger,
            IAppLogger logger)
        {
            _settings = settings;
            _stateMachine = stateMachine;
            _navigation = navigation;
            _stateStore = stateStore;
            _gameSessionService = gameSessionService;
            _userActionLogger = userActionLogger;
            _logger = logger;
        }

        public void Initialize()
        {
            _userActionLogger.Log(
                UserActionType.ApplicationLifecycle,
                "application_bootstrap_started");

            _stateStore.Reset();
            _gameSessionService.ResetSession();

            _stateMachine.MoveTo(AppStateId.Bootstrapping);
            _navigation.MoveTo(_settings.InitialState, "bootstrap");

            _userActionLogger.Log(
                UserActionType.ApplicationLifecycle,
                "application_bootstrap_completed");

            _logger.Info("Application bootstrap completed.");
        }
    }
}
