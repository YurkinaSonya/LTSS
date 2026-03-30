namespace Game.Core.Application.Logging
{
    public enum UserActionType
    {
        ApplicationLifecycle,
        Navigation,
        ScreenShown,
        PopupOpened,
        PopupClosed,
        SessionLifecycle,
        HttpRequest,
        HttpResponse,
        HttpError,
        Interaction
    }
}
