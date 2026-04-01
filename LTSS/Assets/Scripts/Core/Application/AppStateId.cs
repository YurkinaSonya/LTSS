namespace Game.Core.Application
{
    public enum AppStateId
    {
        None,
        Bootstrapping,
        MainMenu,
        Gameplay,
        Results,
        Error,
        Login,
        Authenticating,
        LoadingSession,
        SessionReady,
        FatalError
    }
}
