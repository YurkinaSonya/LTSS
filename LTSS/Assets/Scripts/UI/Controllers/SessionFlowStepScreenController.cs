using System.Collections.Generic;
using Game.Core.Application.Session;
using Game.Core.Application.State;
using Game.Core.Application.UI;

public sealed class SessionFlowStepScreenController : ScreenController
{
    private readonly SessionFlowStepScreenView _view;

    public SessionFlowStepScreenController(SessionFlowStepScreenView view, UIContext context)
        : base(view, context)
    {
        _view = view;
    }

    public override void Open()
    {
        base.Open();

        _view.BindPrimary(OnPrimary);
        _view.BindSecondary(OnSecondary);

        if (Context.SessionFlow != null)
        {
            Context.SessionFlow.Changed += OnFlowChanged;
        }

        if (Context.StateStore != null)
        {
            Context.StateStore.StateChanged += OnApplicationStateChanged;
        }

        Refresh();
    }

    public override void Dispose()
    {
        if (Context.SessionFlow != null)
        {
            Context.SessionFlow.Changed -= OnFlowChanged;
        }

        if (Context.StateStore != null)
        {
            Context.StateStore.StateChanged -= OnApplicationStateChanged;
        }

        _view.BindPrimary(null);
        _view.BindSecondary(null);
    }

    private void OnPrimary()
    {
        var flowState = Context.SessionFlow != null
            ? Context.SessionFlow.Current
            : SessionFlowRuntimeState.Empty;
        var viewModel = flowState != null
            ? flowState.ActiveStepView
            : SessionFlowStepViewModel.Empty;

        if (viewModel.RendererKind == SessionFlowRendererKind.Survey)
        {
            if (!_view.TryPrepareCurrentSurveyPage())
            {
                return;
            }

            if (_view.HasNextSurveyPage())
            {
                _view.AdvanceToNextSurveyPage();
                return;
            }

            Context.SessionFlow?.SubmitActiveSurvey(_view.CollectAnswers());
            return;
        }

        Context.SessionFlow?.CompleteActiveStep();
    }

    private void OnSecondary()
    {
        Context.SessionFlow?.SkipActiveStep();
    }

    private void OnFlowChanged(SessionFlowRuntimeState runtimeState)
    {
        Refresh();
    }

    private void OnApplicationStateChanged(ApplicationStateSnapshot snapshot)
    {
        Refresh();
    }

    private void Refresh()
    {
        var flowState = Context.SessionFlow != null
            ? Context.SessionFlow.Current
            : SessionFlowRuntimeState.Empty;
        _view.Apply(flowState);
    }
}
