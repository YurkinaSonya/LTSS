using Game.Core.Application.UI;
using Game.Core.Application.Session;

public sealed class SessionReadyScreenController : ScreenController
{
    private readonly SessionReadyScreenView _view;

    public SessionReadyScreenController(SessionReadyScreenView view, UIContext context)
        : base(view, context)
    {
        _view = view;
    }

    public override void Open()
    {
        base.Open();

        _view.BindContinue(OnContinue);
        _view.BindLogout(OnLogout);
        ApplyRuntime(Context.SessionCoordinator != null
            ? Context.SessionCoordinator.CurrentRuntime
            : ClientRuntimeState.Empty);

        if (Context.SessionCoordinator != null)
        {
            Context.SessionCoordinator.RuntimeChanged += ApplyRuntime;
        }
    }

    public override void Dispose()
    {
        if (Context.SessionCoordinator != null)
        {
            Context.SessionCoordinator.RuntimeChanged -= ApplyRuntime;
        }

        _view.BindContinue(null);
        _view.BindLogout(null);
    }

    private void OnContinue()
    {
        Context.Navigation?.StartGameplay("session_ready_continue");
    }

    private void OnLogout()
    {
        Context.SessionCoordinator?.ClearSession("session_ready_logout");
    }

    private void ApplyRuntime(ClientRuntimeState runtimeState)
    {
        _view?.ApplyRuntime(runtimeState);
    }
}
