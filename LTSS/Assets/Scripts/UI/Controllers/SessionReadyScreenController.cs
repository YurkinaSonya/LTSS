using Game.Core.Application.UI;
using Game.Core.Application.Session;
using Game.Core.Application.State;

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
        Refresh();

        if (Context.SessionCoordinator != null)
        {
            Context.SessionCoordinator.RuntimeChanged += OnRuntimeChanged;
        }

        if (Context.StateStore != null)
        {
            Context.StateStore.StateChanged += OnApplicationStateChanged;
        }
    }

    public override void Dispose()
    {
        if (Context.SessionCoordinator != null)
        {
            Context.SessionCoordinator.RuntimeChanged -= OnRuntimeChanged;
        }

        if (Context.StateStore != null)
        {
            Context.StateStore.StateChanged -= OnApplicationStateChanged;
        }

        _view.BindContinue(null);
        _view.BindLogout(null);
    }

    private void OnContinue()
    {
        var runtimeState = Context.SessionCoordinator != null
            ? Context.SessionCoordinator.CurrentRuntime
            : ClientRuntimeState.Empty;

        if (CanContinue(runtimeState))
        {
            Context.Navigation?.StartGameplay("session_ready_continue");
        }
    }

    private void OnLogout()
    {
        Context.SessionCoordinator?.ClearSession("session_ready_logout");
    }

    private void OnRuntimeChanged(ClientRuntimeState runtimeState)
    {
        Refresh();
    }

    private void OnApplicationStateChanged(ApplicationStateSnapshot snapshot)
    {
        Refresh();
    }

    private void Refresh()
    {
        var runtimeState = Context.SessionCoordinator != null
            ? Context.SessionCoordinator.CurrentRuntime
            : ClientRuntimeState.Empty;
        var appState = Context.StateStore != null
            ? Context.StateStore.Current
            : ApplicationStateSnapshot.Default;
        var canContinue = CanContinue(runtimeState);
        var statusMessage = ResolveStatusMessage(runtimeState, appState);

        _view?.ApplyRuntime(
            runtimeState,
            statusMessage,
            canContinue,
            canContinue ? "Дальше" : "Завершено");
    }

    private static bool CanContinue(ClientRuntimeState runtimeState)
    {
        if (runtimeState == null || !runtimeState.HasSession || runtimeState.Bootstrap == null || runtimeState.Bootstrap.Run == null)
        {
            return false;
        }

        return runtimeState.Bootstrap.Run.RunStatus != RunLifecycleStatus.Completed
               && runtimeState.Bootstrap.Run.RunStatus != RunLifecycleStatus.Aborted;
    }

    private static string ResolveStatusMessage(ClientRuntimeState runtimeState, ApplicationStateSnapshot appState)
    {
        if (appState != null && !string.IsNullOrWhiteSpace(appState.StatusMessage))
        {
            return appState.StatusMessage;
        }

        if (runtimeState == null || !runtimeState.HasSession || runtimeState.Bootstrap == null || runtimeState.Bootstrap.Run == null)
        {
            return string.Empty;
        }

        return runtimeState.Bootstrap.Run.RunStatus == RunLifecycleStatus.Completed
            ? "Все периоды завершены. Далее будет пост-экспериментальный этап."
            : string.Empty;
    }
}
