using System;
using System.Collections.Generic;
using Game.Core.Application.Periods;
using Game.Core.Application.Session;
using Game.Core.Application.UI;
using Game.Domain.GameFlow;

public sealed class GameplayPlaceholderScreenController : ScreenController
{
    private readonly GameplayPlaceholderScreenView _view;

    public GameplayPlaceholderScreenController(GameplayPlaceholderScreenView view, UIContext context)
        : base(view, context)
    {
        _view = view;
    }

    public override void Open()
    {
        base.Open();

        if (Context.PeriodGameplay != null)
        {
            Context.PeriodGameplay.Changed += ApplyState;
        }

        ApplyState(Context.PeriodGameplay != null
            ? Context.PeriodGameplay.Current
            : PeriodRuntimeState.Empty);
    }

    public override void Dispose()
    {
        if (Context.PeriodGameplay != null)
        {
            Context.PeriodGameplay.Changed -= ApplyState;
        }
    }

    private void ApplyState(PeriodRuntimeState runtimeState)
    {
        var latestState = Context.PeriodGameplay != null
            ? Context.PeriodGameplay.Current
            : runtimeState;

        _view?.Render(
            latestState ?? PeriodRuntimeState.Empty,
            ResolveInstructionTitle(),
            ResolveInstructionBody(),
            OnBack,
            OnComplete,
            OnExpenseAmountChanged,
            OnApplyRequiredExpenseAmount,
            OnExpenseSourceChanged,
            OnAssetAction,
            OnConsumerCreditAction,
            OnApartmentPurchaseAction,
            OnMortgageAction,
            OnPdsAction,
            ResolveNewFeatures(latestState));
    }

    private string ResolveInstructionTitle()
    {
        var step = ResolveInstructionStep();
        return step != null && !string.IsNullOrWhiteSpace(step.Title)
            ? step.Title
            : "Инструкция";
    }

    private string ResolveInstructionBody()
    {
        var step = ResolveInstructionStep();
        return step != null
            ? step.Body ?? string.Empty
            : string.Empty;
    }

    private FlowStepRuntime ResolveInstructionStep()
    {
        var flowState = Context.SessionFlow != null
            ? Context.SessionFlow.Current
            : SessionFlowRuntimeState.Empty;
        var config = flowState != null
            ? flowState.Config
            : SessionConfigRuntime.Empty;
        var steps = config != null && config.PreSessionFlow != null
            ? config.PreSessionFlow.Steps
            : null;

        if (steps == null)
        {
            return null;
        }

        for (var index = steps.Count - 1; index >= 0; index--)
        {
            var step = steps[index];

            if (step != null
                && step.Type == SessionFlowStepType.Instruction
                && !string.IsNullOrWhiteSpace(step.Body))
            {
                return step;
            }
        }

        for (var index = steps.Count - 1; index >= 0; index--)
        {
            var step = steps[index];

            if (step != null
                && !string.IsNullOrWhiteSpace(step.Body)
                && !IsSurveyStepType(step.Type))
            {
                return step;
            }
        }

        return null;
    }

    private static bool IsSurveyStepType(SessionFlowStepType type)
    {
        switch (type)
        {
            case SessionFlowStepType.InstructionQuiz:
            case SessionFlowStepType.PreTest:
            case SessionFlowStepType.PostTest:
            case SessionFlowStepType.PostPeriodSurvey:
                return true;
            default:
                return false;
        }
    }

    private IReadOnlyCollection<string> ResolveNewFeatures(PeriodRuntimeState runtimeState)
    {
        if (runtimeState == null
            || !runtimeState.HasDefinition
            || runtimeState.Definition.Meta == null
            || runtimeState.PeriodNumber <= 1)
        {
            return Array.Empty<string>();
        }

        var config = Context.SessionFlow != null
            ? Context.SessionFlow.Current.Config
            : SessionConfigRuntime.Empty;

        if (config == null
            || !config.TryGetPeriod(runtimeState.PeriodNumber - 1, out var previousPeriod)
            || previousPeriod == null)
        {
            return Array.Empty<string>();
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentFeatures = runtimeState.Definition.Meta.EnabledFeatures ?? Array.Empty<string>();

        for (var index = 0; index < currentFeatures.Count; index++)
        {
            var feature = currentFeatures[index];

            if (!string.IsNullOrWhiteSpace(feature) && !previousPeriod.HasFeature(feature))
            {
                result.Add(feature);
            }
        }

        return result.Count > 0
            ? result
            : Array.Empty<string>();
    }

    private void OnBack()
    {
        Context.Navigation?.ShowSessionReady("gameplay_back");
    }

    private void OnComplete()
    {
        Context.PeriodGameplay?.SubmitPeriod();
    }

    private void OnExpenseAmountChanged(string expenseId, string rawAmount)
    {
        Context.PeriodGameplay?.SetExpenseAmount(expenseId, rawAmount);
    }

    private void OnApplyRequiredExpenseAmount(string expenseId)
    {
        Context.PeriodGameplay?.ApplyRequiredExpenseAmount(expenseId);
    }

    private void OnExpenseSourceChanged(string expenseId, FundsSourceType source)
    {
        Context.PeriodGameplay?.SetExpenseSource(expenseId, source);
    }

    private void OnAssetAction(string assetId, AssetOperationKind kind)
    {
        Context.PeriodGameplay?.OpenAssetDialog(assetId, kind);
    }

    private void OnConsumerCreditAction()
    {
        Context.PeriodGameplay?.OpenConsumerCreditDialog();
    }

    private void OnApartmentPurchaseAction()
    {
        Context.PeriodGameplay?.OpenApartmentPurchaseDialog();
    }

    private void OnMortgageAction()
    {
        Context.PeriodGameplay?.OpenMortgageDialog();
    }

    private void OnPdsAction()
    {
        Context.PeriodGameplay?.OpenPdsDialog();
    }
}
