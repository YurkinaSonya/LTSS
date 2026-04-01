using Game.Core.Application.Logging;
using Game.Core.Application.Navigation;
using Game.Core.Application.Networking;
using Game.Core.Application.Session;
using Game.Core.Application.State;
using Game.Core;

namespace Game.Core.Application.UI
{
    public sealed class UIContext
    {
        public IApplicationNavigationService Navigation { get; }
        public IPopupNavigationService Popups { get; }
        public IGameSessionService GameSession { get; }
        public ISessionCoordinator SessionCoordinator { get; }
        public IUserActionLogger UserActions { get; }
        public IAppLogger Logger { get; }
        public IEventAggregator Events { get; }
        public IApplicationStateStore StateStore { get; }
        public IApiClient ApiClient { get; }

        public UIContext(
            IApplicationNavigationService navigation,
            IPopupNavigationService popups,
            IGameSessionService gameSession,
            ISessionCoordinator sessionCoordinator,
            IUserActionLogger userActions,
            IAppLogger logger,
            IEventAggregator eventsProvider,
            IApplicationStateStore stateStore,
            IApiClient apiClient)
        {
            Navigation = navigation;
            Popups = popups;
            GameSession = gameSession;
            SessionCoordinator = sessionCoordinator;
            UserActions = userActions;
            Logger = logger;
            Events = eventsProvider;
            StateStore = stateStore;
            ApiClient = apiClient;
        }
    }
}
