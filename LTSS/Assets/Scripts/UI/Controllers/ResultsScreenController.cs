using System;
using System.Globalization;
using Game.Core.Application.Periods;
using Game.Core.Application.UI;
using Game.Domain.GameFlow;

public sealed class ResultsScreenController : ScreenController
{
    private const double LongTermAssetUjeMultiplier = 1.2d;

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
            _view.Apply("-");
            return;
        }

        var assetsInUje = CalculateAssetsInUje(runtimeState, out _);
        var finalUje = runtimeState.Summary.Uje + assetsInUje;

        _view.Apply(finalUje.ToString("0.##", CultureInfo.InvariantCulture));
    }

    private static double CalculateAssetsInUje(
        PeriodRuntimeState runtimeState,
        out bool includesProjectedPds)
    {
        includesProjectedPds = false;

        if (runtimeState == null || runtimeState.Summary == null || runtimeState.Summary.AssetBalances == null)
        {
            return 0d;
        }

        var currentAssetsUje = 0d;
        var longTermAssetsEcu = 0d;

        for (var index = 0; index < runtimeState.Summary.AssetBalances.Count; index++)
        {
            var asset = runtimeState.Summary.AssetBalances[index];

            if (asset == null)
            {
                continue;
            }

            switch (asset.AssetType)
            {
                case PeriodAssetType.Cash:
                case PeriodAssetType.Deposit:
                    currentAssetsUje += Math.Max(0d, asset.CurrentAmount);
                    break;
                case PeriodAssetType.Apartment:
                    longTermAssetsEcu += Math.Max(0d, asset.CurrentAmount);
                    break;
                case PeriodAssetType.Pds:
                    longTermAssetsEcu += CalculatePdsResultsValue(runtimeState, asset.CurrentAmount, out var projected);
                    includesProjectedPds |= projected;
                    break;
            }
        }

        currentAssetsUje += CalculateUnfinishedEducationValue(runtimeState);

        var obligations = CalculateOutstandingObligations(runtimeState);
        return currentAssetsUje + longTermAssetsEcu * LongTermAssetUjeMultiplier - obligations;
    }

    private static double CalculateOutstandingObligations(PeriodRuntimeState runtimeState)
    {
        if (runtimeState == null
            || !runtimeState.HasDefinition
            || runtimeState.Definition.ConsumerCredits == null)
        {
            return 0d;
        }

        var total = 0d;

        for (var index = 0; index < runtimeState.Definition.ConsumerCredits.Count; index++)
        {
            var credit = runtimeState.Definition.ConsumerCredits[index];

            if (credit == null || credit.RemainingPrincipal <= 0.01d)
            {
                continue;
            }

            if (runtimeState.PeriodNumber <= credit.OriginationPeriodNumber)
            {
                total += Math.Max(0d, credit.RemainingPrincipal);
                continue;
            }

            var paymentAmount = ResolveCreditPaymentAmount(runtimeState, credit);
            var nextRemainingPrincipal = ConsumerCreditMath.CalculateNextRemainingPrincipal(
                credit.RemainingPrincipal,
                paymentAmount,
                credit.FixedRatePercent);
            var nextRemainingPeriods = Math.Max(0, credit.RemainingPeriods - 1);

            if (nextRemainingPeriods <= 0 || nextRemainingPrincipal <= 0.01d)
            {
                continue;
            }

            total += Math.Max(0d, nextRemainingPrincipal);
        }

        return total;
    }

    private static double ResolveCreditPaymentAmount(
        PeriodRuntimeState runtimeState,
        ConsumerCreditContractRuntime credit)
    {
        if (runtimeState == null || credit == null || runtimeState.Expenses == null)
        {
            return Math.Max(0d, credit != null ? credit.PeriodicPayment : 0d);
        }

        var expenseId = ConsumerCreditMath.BuildExpenseId(
            string.IsNullOrWhiteSpace(credit.ContractType)
                ? ConsumerCreditMath.ConsumerCreditKind
                : credit.ContractType,
            credit.CreditId);

        for (var index = 0; index < runtimeState.Expenses.Count; index++)
        {
            var expense = runtimeState.Expenses[index];

            if (expense != null
                && string.Equals(expense.ExpenseId, expenseId, StringComparison.Ordinal))
            {
                return Math.Max(0d, expense.Amount);
            }
        }

        return Math.Max(0d, credit.PeriodicPayment);
    }

    private static double CalculateUnfinishedEducationValue(PeriodRuntimeState runtimeState)
    {
        if (runtimeState == null || !runtimeState.HasDefinition)
        {
            return 0d;
        }

        var goal = runtimeState.Definition.EducationGoal;
        var currentContribution = 0d;

        if (runtimeState.Expenses != null)
        {
            for (var index = 0; index < runtimeState.Expenses.Count; index++)
            {
                var expense = runtimeState.Expenses[index];

                if (expense != null
                    && string.Equals(expense.ExpenseId, "education", StringComparison.Ordinal))
                {
                    currentContribution = Math.Max(0d, expense.Amount);
                    break;
                }
            }
        }

        var targetAmount = goal != null && goal.TargetAmount > 0d
            ? goal.TargetAmount
            : ConsumerCreditMath.CalculateEducationTargetAmount(runtimeState.Definition);
        var accumulatedAmount = goal != null
            ? Math.Max(0d, goal.AccumulatedAmount)
            : 0d;
        var totalCommitted = Math.Min(targetAmount, accumulatedAmount + currentContribution);
        var isCompleted = goal != null && goal.GoalReachedPeriodNumber > 0
            || totalCommitted + 0.01d >= targetAmount;

        return isCompleted
            ? 0d
            : Math.Max(0d, totalCommitted);
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
