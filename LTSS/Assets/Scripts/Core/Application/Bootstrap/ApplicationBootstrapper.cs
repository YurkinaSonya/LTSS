using Game.Core.Application.Logging;
using Game.Core.Application.Navigation;
using Game.Core.Application.Session;
using Game.Core.Application.State;
using Zenject;

namespace Game.Core.Application.Bootstrap
{
    public sealed class ApplicationBootstrapper : IInitializable
    {
        private readonly ApplicationBootstrapSettings _settings;
        private readonly ApplicationStateMachine _stateMachine;
        private readonly IApplicationStateStore _stateStore;
        private readonly IGameSessionService _gameSessionService;
        private readonly ISessionCoordinator _sessionCoordinator;
        private readonly IUserActionLogger _userActionLogger;
        private readonly IAppLogger _logger;

        public ApplicationBootstrapper(
            ApplicationBootstrapSettings settings,
            ApplicationStateMachine stateMachine,
            IApplicationStateStore stateStore,
            IGameSessionService gameSessionService,
            ISessionCoordinator sessionCoordinator,
            IUserActionLogger userActionLogger,
            IAppLogger logger)
        {
            _settings = settings;
            _stateMachine = stateMachine;
            _stateStore = stateStore;
            _gameSessionService = gameSessionService;
            _sessionCoordinator = sessionCoordinator;
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
            _sessionCoordinator.RestoreIfPossible(ResolveFallbackState());

            _userActionLogger.Log(
                UserActionType.ApplicationLifecycle,
                "application_bootstrap_initialized");

            _logger.Info("Application bootstrap initialized.");
        }

        private AppStateId ResolveFallbackState()
        {
            if (_settings.InitialState == AppStateId.MainMenu)
            {
                return AppStateId.Login;
            }

            return _settings.InitialState == AppStateId.None
                ? AppStateId.Login
                : _settings.InitialState;
        }
    }
}
