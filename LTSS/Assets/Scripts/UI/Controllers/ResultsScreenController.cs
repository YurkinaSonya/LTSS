using System;
using System.Globalization;
using Game.Core.Application.Periods;
using Game.Core.Application.UI;
using Game.Domain.GameFlow;

public sealed class ResultsScreenController : ScreenController
{
    private readonly ResultsScreenView _view;

    public ResultsScreenController(ResultsScreenView view, UIContext context)
        : base(view, context)
    {
        _view = view;
    }

    public override void Open()
    {
        base.Open();

        _view.BindReturn(OnReturn);
        ApplyResults(Context.PeriodGameplay != null
            ? Context.PeriodGameplay.Current
            : PeriodRuntimeState.Empty);

        if (Context.PeriodGameplay != null)
        {
            Context.PeriodGameplay.Changed += ApplyResults;
        }
    }

    public override void Dispose()
    {
        if (Context.PeriodGameplay != null)
        {
            Context.PeriodGameplay.Changed -= ApplyResults;
        }

        _view.BindReturn(null);
    }

    private void OnReturn()
    {
        Context.Navigation?.ShowSessionReady("results_back_to_session");
    }

    private void ApplyResults(PeriodRuntimeState runtimeState)
    {
        if (_view == null)
        {
            return;
        }

        if (runtimeState == null || !runtimeState.HasDefinition || runtimeState.Summary == null)
        {
            _view.Apply("-", "-", string.Empty);
            return;
        }

        var totalAssets = CalculateTotalAssets(runtimeState, out var includesProjectedPds);
        var moneyCaption = includesProjectedPds
            ? "с учетом прогнозируемого значения ПДС"
            : string.Empty;

        _view.Apply(
            runtimeState.Summary.Uje.ToString("0.##", CultureInfo.InvariantCulture),
            EcuFormatter.FormatAmount(totalAssets),
            moneyCaption);
    }

    private static double CalculateTotalAssets(
        PeriodRuntimeState runtimeState,
        out bool includesProjectedPds)
    {
        includesProjectedPds = false;

        if (runtimeState == null || runtimeState.Summary == null || runtimeState.Summary.AssetBalances == null)
        {
            return 0d;
        }

        var total = 0d;

        for (var index = 0; index < runtimeState.Summary.AssetBalances.Count; index++)
        {
            var asset = runtimeState.Summary.AssetBalances[index];

            if (asset == null)
            {
                continue;
            }

            if (asset.AssetType == PeriodAssetType.Pds)
            {
                total += CalculatePdsResultsValue(runtimeState, asset.CurrentAmount, out var projected);
                includesProjectedPds |= projected;
                continue;
            }

            total += Math.Max(0d, asset.CurrentAmount);
        }

        return total;
    }

    private static double CalculatePdsResultsValue(
        PeriodRuntimeState runtimeState,
        double currentAmount,
        out bool projected)
    {
        projected = false;

        if (runtimeState == null
            || !runtimeState.HasDefinition
            || runtimeState.Definition.PdsAccount == null)
        {
            return Math.Max(0d, currentAmount);
        }

        if (currentAmount <= 0.01d)
        {
            return 0d;
        }

        var activationPeriodNumber = runtimeState.Definition.PdsAccount.ActivationPeriodNumber;
        var completedParticipationPeriods = activationPeriodNumber > 0
            ? Math.Max(0, runtimeState.PeriodNumber - activationPeriodNumber + 1)
            : 0;
        var remainingPeriods = Math.Max(0, ConsumerCreditMath.PdsProjectionPeriods - completedParticipationPeriods);

        if (remainingPeriods <= 0)
        {
            return Math.Max(0d, currentAmount);
        }

        projected = true;

        return ConsumerCreditMath.CalculatePdsProjectedBalance(
            Math.Max(0d, currentAmount),
            0d,
            0d,
            runtimeState.Definition.EconomyContext != null
                ? runtimeState.Definition.EconomyContext.DepositRate
                : null,
            completedParticipationPeriods + 1,
            remainingPeriods);
    }
}
