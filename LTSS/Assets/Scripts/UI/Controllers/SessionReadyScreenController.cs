using System;
using Game.Core.Application.Session;
using Game.Core.Application.State;
using Game.Core.Application.UI;

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
        _view.BindTesterSkip(OnTesterSkip);
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
        _view.BindTesterSkip(null);
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

    private void OnTesterSkip()
    {
        var runtimeState = Context.SessionCoordinator != null
            ? Context.SessionCoordinator.CurrentRuntime
            : ClientRuntimeState.Empty;

        if (!CanContinue(runtimeState) || !CanShowTesterSkip(runtimeState))
        {
            return;
        }

        Context.SessionFlow?.SkipPreSessionFlowForTesting();
        Context.Navigation?.StartGameplay("session_ready_tester_skip_pre_session");
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
            canContinue ? "Дальше" : "Завершено",
            CanShowTesterSkip(runtimeState));
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

    private static bool CanShowTesterSkip(ClientRuntimeState runtimeState)
    {
        if (runtimeState == null
            || !runtimeState.HasSession
            || runtimeState.Bootstrap == null
            || runtimeState.Bootstrap.Participant == null
            || runtimeState.Bootstrap.Session == null
            || runtimeState.Bootstrap.Session.SessionConfig == null)
        {
            return false;
        }

        if (!string.Equals(
                runtimeState.Bootstrap.Participant.AssignedGroupCode,
                "test",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var config = runtimeState.Bootstrap.Session.SessionConfig.Runtime;
        return config != null
               && config.IsValid
               && config.PreSessionFlow != null
               && config.PreSessionFlow.IsEnabled
               && config.PreSessionFlow.HasSteps;
    }
}
