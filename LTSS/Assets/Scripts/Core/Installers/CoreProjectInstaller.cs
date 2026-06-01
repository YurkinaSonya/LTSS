using UnityEngine;
using Zenject;
using Game.Core.Application.Bootstrap;
using Game.Core.Application.Logging;
using Game.Core.Application.Navigation;
using Game.Core.Application.Networking;
using Game.Core.Application.Periods;
using Game.Core.Application.Session;
using Game.Core.Application.State;
using Game.Core.Events;

namespace Game.Core.Installers
{
    /// <summary>
    /// Defines global, domain-agnostic bindings.
    /// This installer represents the root of infrastructure composition.
    /// </summary>
    public sealed class CoreProjectInstaller : MonoInstaller
    {
        [Header("Optional Scene Services")]
        [SerializeField] private CoroutineRunner _coroutineRunner;

        [Header("Application")]
        [SerializeField] private UIController _uiController;
        [SerializeField] private ApplicationBootstrapSettings _bootstrapSettings =
            new ApplicationBootstrapSettings();
        [SerializeField] private ApiServiceSettings _apiSettings =
            new ApiServiceSettings();

        public override void InstallBindings()
        {
            var coroutineRunner = ResolveCoroutineRunner();
            var uiController = ResolveUiController();

            if (coroutineRunner != null)
            {
                Container.Bind<CoroutineRunner>()
                    .FromInstance(coroutineRunner)
                    .AsSingle()
                    .NonLazy();
            }

            Container.Bind<IEventAggregator>()
                .To<EventAggregator>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IAppLogger>()
                .To<UnityAppLogger>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IUserActionLogger>()
                .To<UserActionLogger>()
                .AsSingle()
                .NonLazy();

            Container.BindInstance(_bootstrapSettings).IfNotBound();
            Container.BindInstance(_apiSettings).IfNotBound();

            Container.Bind<IApplicationStateStore>()
                .To<ApplicationStateStore>()
                .AsSingle()
                .NonLazy();

            Container.Bind<ApplicationStateMachine>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IGameSessionService>()
                .To<GameSessionService>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IApplicationNavigationService>()
                .To<ApplicationNavigationService>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IPopupNavigationService>()
                .To<PopupNavigationService>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IJsonSerializer>()
                .To<JsonUtilitySerializer>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IJsonNodeParser>()
                .To<JsonNodeParser>()
                .AsSingle()
                .NonLazy();

            Container.Bind<ISessionConfigRuntimeBuilder>()
                .To<SessionConfigRuntimeBuilder>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IApiClient>()
                .To<UnityWebRequestApiClient>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IAuthApiClient>()
                .To<AuthApiClient>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IRunApiClient>()
                .To<RunApiClient>()
                .AsSingle()
                .NonLazy();

            Container.Bind<ISessionRuntimeFactory>()
                .To<SessionRuntimeFactory>()
                .AsSingle()
                .NonLazy();

            Container.Bind<ISessionPersistenceService>()
                .To<SessionPersistenceService>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IPeriodCalculationEngine>()
                .To<PeriodCalculationEngine>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IPeriodRuntimeFactory>()
                .To<PeriodRuntimeFactory>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IPeriodCheckpointBuilder>()
                .To<PeriodCheckpointBuilder>()
                .AsSingle()
                .NonLazy();

            Container.Bind<ICheckpointSender>()
                .To<CheckpointSender>()
                .AsSingle()
                .NonLazy();

            Container.Bind<ISurveySubmissionSender>()
                .To<SurveySubmissionSender>()
                .AsSingle()
                .NonLazy();

            Container.Bind<ILogBatchSender>()
                .To<LogBatchSender>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<RunTelemetryService>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IPeriodGameplayService>()
                .To<PeriodGameplayService>()
                .AsSingle()
                .NonLazy();

            Container.Bind<ISessionFlowCoordinator>()
                .To<SessionFlowCoordinator>()
                .AsSingle()
                .NonLazy();

            Container.Bind<ISessionCoordinator>()
                .To<SessionCoordinator>()
                .AsSingle()
                .NonLazy();

            Container.Bind<IApplicationState>().To<BootstrappingState>().AsSingle();
            Container.Bind<IApplicationState>().To<MainMenuState>().AsSingle();
            Container.Bind<IApplicationState>().To<GameplayState>().AsSingle();
            Container.Bind<IApplicationState>().To<ResultsState>().AsSingle();
            Container.Bind<IApplicationState>().To<ErrorState>().AsSingle();
            Container.Bind<IApplicationState>().To<LoginState>().AsSingle();
            Container.Bind<IApplicationState>().To<AuthenticatingState>().AsSingle();
            Container.Bind<IApplicationState>().To<LoadingSessionState>().AsSingle();
            Container.Bind<IApplicationState>().To<SessionReadyState>().AsSingle();
            Container.Bind<IApplicationState>().To<FatalErrorState>().AsSingle();

            if (uiController != null)
            {
                Container.BindInterfacesAndSelfTo<UIController>()
                    .FromInstance(uiController)
                    .AsSingle()
                    .NonLazy();

                Container.QueueForInject(uiController);
                Container.BindExecutionOrder<UIController>(-200);
            }

            Container.BindInterfacesTo<ApplicationBootstrapper>()
                .AsSingle()
                .NonLazy();

            Container.BindExecutionOrder<ApplicationBootstrapper>(-100);
        }

        private CoroutineRunner ResolveCoroutineRunner()
        {
            if (_coroutineRunner != null)
            {
                return _coroutineRunner;
            }

            var projectContext = GetComponentInParent<ProjectContext>();

            if (projectContext != null)
            {
                _coroutineRunner = projectContext.GetComponentInChildren<CoroutineRunner>(true);
            }

            if (_coroutineRunner != null)
            {
                return _coroutineRunner;
            }

            var runnerObject = new GameObject("CoroutineRunner");
            runnerObject.transform.SetParent(transform, false);
            _coroutineRunner = runnerObject.AddComponent<CoroutineRunner>();
            return _coroutineRunner;
        }

        private UIController ResolveUiController()
        {
            if (_uiController != null)
            {
                return _uiController;
            }

            var projectContext = GetComponentInParent<ProjectContext>();

            if (projectContext == null)
            {
                return null;
            }

            _uiController = projectContext.GetComponentInChildren<UIController>(true);
            return _uiController;
        }
    }
}
