using Game.Core.Application;
using Game.Core.Application.State;
using Game.Core.Application.UI;

public sealed class LoadingScreenController : ScreenController
{
    private readonly LoadingScreenView _view;

    public LoadingScreenController(LoadingScreenView view, UIContext context)
        : base(view, context)
    {
        _view = view;
    }

    public override void Open()
    {
        base.Open();

        ApplyState(Context.StateStore != null
            ? Context.StateStore.Current
            : null);

        if (Context.StateStore != null)
        {
            Context.StateStore.StateChanged += ApplyState;
        }
    }

    public override void Dispose()
    {
        if (Context.StateStore != null)
        {
            Context.StateStore.StateChanged -= ApplyState;
        }
    }

    private void ApplyState(ApplicationStateSnapshot state)
    {
        if (_view == null || state == null)
        {
            return;
        }

        _view.SetContent(ResolveTitle(state.AppStateId), ResolveStatus(state));
    }

    private static string ResolveTitle(AppStateId appStateId)
    {
        switch (appStateId)
        {
            case AppStateId.Bootstrapping:
                return "Initializing";
            case AppStateId.Authenticating:
                return "Authenticating";
            case AppStateId.LoadingSession:
                return "Loading Session";
            default:
                return "Loading";
        }
    }

    private static string ResolveStatus(ApplicationStateSnapshot state)
    {
        if (state == null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(state.StatusMessage)
            ? "Please wait..."
            : state.StatusMessage;
    }
}
