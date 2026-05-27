using System;
using System.Collections.Generic;
using Game.Domain.GameFlow;
using UnityEngine;

namespace Game.Core.Application.Periods
{
    public sealed class PeriodCalculationEngine : IPeriodCalculationEngine
    {
        private const double ComparisonTolerance = 0.01d;
        private const double GoodsServicesMinBase = 20d;
        private const double GoodsServicesUpperBase = 60d;
        private const double HousingRentBase = 20d;
        private const double LeisureFirstBase = 10d;
        private const double LeisureUpperBase = 100d;
        private const double HolidayBase = 10d;
        private const double HolidayBonusUje = 15d;
        private const double LeisureZeroSpendPenalty = -10d;
        private const double DebtUjePenaltyRate = 0.5d;

        public PeriodCalculationSummary Recalculate(
            PeriodRuntimeDefinition definition,
            IReadOnlyList<PeriodExpenseState> expenses,
            IReadOnlyList<PeriodAssetOperationEntry> assetOperations)
        {
            if (definition == null)
            {
                return PeriodCalculationSummary.Empty;
            }

            var safeExpenses = expenses ?? Array.Empty<PeriodExpenseState>();
            var safeOperations = assetOperations ?? Array.Empty<PeriodAssetOperationEntry>();
            var validationIssues = new List<PeriodValidationIssue>();
            var ujeBreakdown = new List<UjeBreakdownItem>();

            var incomeAllocatedToExpenses = 0d;
            var incomeAllocatedToAssets = 0d;
            var totalExpenses = 0d;
            var cashBalance = definition.InitialCashBalance;
            var depositBalance = definition.InitialDepositBalance;

            foreach (var expenseState in safeExpenses) 
            {
                if (expenseState == null || expenseState.Amount <= 0d)
                {
                    continue;
                }

                totalExpenses += expenseState.Amount;

                switch (expenseState.Source)
                {
                    case FundsSourceType.CurrentIncome:
                        incomeAllocatedToExpenses += expenseState.Amount;
                        break;
                    case FundsSourceType.Cash:
                        cashBalance -= expenseState.Amount;
                        break;
                    case FundsSourceType.Deposit:
                        depositBalance -= expenseState.Amount;
                        break;
                }
            }

            foreach (var operation in safeOperations)
            {
                if (operation == null || operation.Amount <= 0d)
                {
                    continue;
                }

                if (IsCashAsset(operation.AssetId))
                {
                    ApplyCashOperation(operation, ref cashBalance, ref incomeAllocatedToAssets);
                    continue;
                }

                if (IsDepositAsset(operation.AssetId))
                {
                    ApplyDepositOperation(
                        operation,
                        ref cashBalance,
                        ref depositBalance,
                        ref incomeAllocatedToAssets);
                    continue;
                }

                if (IsPdsAsset(operation.AssetId))
                {
                    ApplyPdsOperation(
                        operation,
                        ref cashBalance,
                        ref depositBalance,
                        ref incomeAllocatedToAssets);
                }
            }

            var accumulatedUje = definition.CalculationSettings.BaseUje;
            var projectedUjeDelta = 0d;

            foreach (var expenseDefinition in definition.ExpenseDefinitions)
            {
                if (expenseDefinition == null)
                {
                    continue;
                }

                var state = FindExpenseState(safeExpenses, expenseDefinition.Id);
                var contribution = CalculateUjeContribution(definition, expenseDefinition, state);

                projectedUjeDelta += contribution;
                ujeBreakdown.Add(new UjeBreakdownItem(
                    expenseDefinition.Id,
                    expenseDefinition.Title,
                    contribution,
                    GetUjeBreakdownMaximumValue(definition, expenseDefinition),
                    GetUjeBreakdownReferenceAmount(definition, expenseDefinition)));

                ValidateExpense(definition, expenseDefinition, state, validationIssues);
            }

            ApplyResidenceOwnershipBonus(definition, ujeBreakdown, ref projectedUjeDelta);
            ApplyDebtPenaltyIfNeeded(definition, cashBalance, ujeBreakdown, ref projectedUjeDelta);

            var uje = accumulatedUje + projectedUjeDelta;

            var remainingToAllocate = definition.CalculationSettings.CurrentIncomeEcu
                - incomeAllocatedToExpenses
                - incomeAllocatedToAssets;
            var tolerance = Math.Max(0d, definition.ValidationSettings.CompletionRemainderTolerance);

            ValidateIncomeRemainder(definition, remainingToAllocate, tolerance, validationIssues);
            ValidateAssetBalances(definition, cashBalance, depositBalance, tolerance, validationIssues);

            if (definition.ValidationSettings.MinimumUjeToComplete > 0d
                && uje + tolerance < definition.ValidationSettings.MinimumUjeToComplete)
            {
                validationIssues.Add(new PeriodValidationIssue(
                    "uje_minimum_not_reached",
                    "УЖЭ ниже минимального допустимого уровня.",
                    "uje",
                    true));
            }

            var assetBalances = new List<PeriodAssetBalance>();

            foreach (var assetDefinition in definition.AssetDefinitions)
            {
                if (assetDefinition == null)
                {
                    continue;
                }

                assetBalances.Add(new PeriodAssetBalance(
                    assetDefinition.Id,
                    assetDefinition.Title,
                    assetDefinition.AssetType,
                    GetInitialBalance(definition, assetDefinition.AssetType),
                    GetCurrentBalance(definition, assetDefinition.AssetType, cashBalance, depositBalance),
                    assetDefinition.AllowsDeposit,
                    assetDefinition.AllowsWithdraw));
            }

            var canComplete = true;

            foreach (var issue in validationIssues)
            {
                if (issue != null && issue.IsBlocking)
                {
                    canComplete = false;
                    break;
                }
            }

            return new PeriodCalculationSummary(
                definition.CalculationSettings.CurrentIncomeEcu,
                incomeAllocatedToExpenses,
                incomeAllocatedToAssets,
                totalExpenses,
                remainingToAllocate,
                cashBalance,
                depositBalance,
                accumulatedUje,
                projectedUjeDelta,
                uje,
                ujeBreakdown,
                validationIssues,
                assetBalances,
                canComplete);
        }

        private static void ValidateExpense(
            PeriodRuntimeDefinition definition,
            PeriodExpenseDefinition expenseDefinition,
            PeriodExpenseState state,
            ICollection<PeriodValidationIssue> issues)
        {
            var amount = state != null ? Math.Max(0d, state.Amount) : 0d;

            if (definition.ValidationSettings.RequireRequiredExpenses
                && expenseDefinition.IsRequired
                && amount + definition.ValidationSettings.CompletionRemainderTolerance
                < Math.Max(expenseDefinition.MinimumAmount, 0.01d))
            {
                issues.Add(new PeriodValidationIssue(
                    "required_expense_missing",
                    $"Заполните обязательную статью «{expenseDefinition.Title}».",
                    expenseDefinition.Id,
                    true));
            }

            if (IsFixedAmountExpense(expenseDefinition)
                && amount > ComparisonTolerance
                && Math.Abs(amount - expenseDefinition.MinimumAmount) > definition.ValidationSettings.CompletionRemainderTolerance)
            {
                issues.Add(new PeriodValidationIssue(
                    "fixed_expense_amount",
                    $"Для статьи «{expenseDefinition.Title}» доступна только фиксированная сумма {EcuFormatter.FormatAmount(expenseDefinition.MinimumAmount)}.",
                    expenseDefinition.Id,
                    true));
            }

            if (false && string.Equals(expenseDefinition.Id, "holiday", StringComparison.Ordinal)
                && amount > ComparisonTolerance)
            {
                var fixedAmount = expenseDefinition.MaximumAmount > 0d
                    ? expenseDefinition.MaximumAmount
                    : ScaleThreshold(definition, HolidayBase);

                if (Math.Abs(amount - fixedAmount) > definition.ValidationSettings.CompletionRemainderTolerance)
                {
                    issues.Add(new PeriodValidationIssue(
                        "holiday_fixed_amount",
                        $"Для статьи «{expenseDefinition.Title}» доступна только фиксированная сумма {EcuFormatter.FormatAmount(fixedAmount)}.",
                        expenseDefinition.Id,
                        true));
                }
            }

            if (state == null)
            {
                return;
            }

            var isSourceAllowed = expenseDefinition.AllowedSources == null
                || expenseDefinition.AllowedSources.Count == 0;

            if (!isSourceAllowed)
            {
                foreach (var source in expenseDefinition.AllowedSources)
                {
                    if (source == state.Source)
                    {
                        isSourceAllowed = true;
                        break;
                    }
                }
            }

            if (!isSourceAllowed)
            {
                issues.Add(new PeriodValidationIssue(
                    "expense_source_invalid",
                    $"Для статьи «{expenseDefinition.Title}» выбран недопустимый источник.",
                    expenseDefinition.Id,
                    true));
            }
        }

        private static double CalculateUjeContribution(
            PeriodRuntimeDefinition definition,
            PeriodExpenseDefinition expenseDefinition,
            PeriodExpenseState state)
        {
            var amount = state != null ? Math.Max(0d, state.Amount) : 0d;

            switch (expenseDefinition.Id)
            {
                case "goods_services":
                    return CalculateGoodsServicesContribution(definition, expenseDefinition, amount);
                case "housing_rent":
                    return CalculateHousingContribution(expenseDefinition, amount);
                case "leisure":
                    return CalculateLeisureContribution(definition, amount);
                case "holiday":
                    return CalculateHolidayContribution(definition, expenseDefinition, amount);
                default:
                    return 0d;
            }
        }

        private static bool IsFixedAmountExpense(PeriodExpenseDefinition expenseDefinition)
        {
            return expenseDefinition != null
                   && expenseDefinition.MinimumAmount > ComparisonTolerance
                   && expenseDefinition.MaximumAmount > ComparisonTolerance
                   && Math.Abs(expenseDefinition.MaximumAmount - expenseDefinition.MinimumAmount) <= ComparisonTolerance;
        }

        private static double CalculateGoodsServicesContribution(
            PeriodRuntimeDefinition definition,
            PeriodExpenseDefinition expenseDefinition,
            double amount)
        {
            var minimumAmount = expenseDefinition.MinimumAmount > 0d
                ? expenseDefinition.MinimumAmount
                : ScaleThreshold(definition, GoodsServicesMinBase);
            var upperThreshold = expenseDefinition.UjeReferenceAmount > minimumAmount + ComparisonTolerance
                ? expenseDefinition.UjeReferenceAmount
                : ScaleThreshold(definition, GoodsServicesUpperBase);

            if (amount + ComparisonTolerance < minimumAmount)
            {
                return 0d;
            }

            double coefficient;

            if (amount <= minimumAmount + ComparisonTolerance)
            {
                coefficient = 1d;
            }
            else if (amount >= upperThreshold - ComparisonTolerance)
            {
                coefficient = 1.3d;
            }
            else
            {
                coefficient = Lerp(minimumAmount, upperThreshold, amount, 1d, 1.3d);
            }

            return amount * coefficient;
        }

        private static double CalculateHousingContribution(
            PeriodExpenseDefinition expenseDefinition,
            double amount)
        {
            if (expenseDefinition.MinimumAmount <= 0d
                || amount <= ComparisonTolerance
                || Math.Abs(amount - expenseDefinition.MinimumAmount) > ComparisonTolerance)
            {
                return 0d;
            }

            return amount;
        }

        private static double CalculateLeisureContribution(
            PeriodRuntimeDefinition definition,
            double amount)
        {
            var firstThreshold = ScaleThreshold(definition, LeisureFirstBase);
            var upperThreshold = ScaleThreshold(definition, LeisureUpperBase);

            if (amount <= ComparisonTolerance)
            {
                return LeisureZeroSpendPenalty;
            }

            double coefficient;

            if (amount < firstThreshold - ComparisonTolerance)
            {
                coefficient = Lerp(0d, firstThreshold, amount, 0d, 1d);
            }
            else if (amount <= firstThreshold + ComparisonTolerance)
            {
                coefficient = 1d;
            }
            else if (amount < upperThreshold - ComparisonTolerance)
            {
                coefficient = Lerp(firstThreshold, upperThreshold, amount, 1.1d, 2d);
            }
            else
            {
                coefficient = 2d;
            }

            return amount * coefficient;
        }

        private static double CalculateHolidayContribution(
            PeriodRuntimeDefinition definition,
            PeriodExpenseDefinition expenseDefinition,
            double amount)
        {
            if (amount <= ComparisonTolerance)
            {
                return 0d;
            }

            var fixedAmount = expenseDefinition.MaximumAmount > 0d
                ? expenseDefinition.MaximumAmount
                : ScaleThreshold(definition, HolidayBase);

            return amount + ComparisonTolerance >= fixedAmount
                ? HolidayBonusUje
                : 0d;
        }

        private static double GetUjeBreakdownMaximumValue(
            PeriodRuntimeDefinition definition,
            PeriodExpenseDefinition expenseDefinition)
        {
            switch (expenseDefinition.Id)
            {
                case "goods_services":
                    return 0d;
                case "housing_rent":
                    return expenseDefinition.MinimumAmount > 0d
                        ? expenseDefinition.MinimumAmount
                        : ScaleThreshold(definition, HousingRentBase);
                case "leisure":
                    return 0d;
                case "holiday":
                    return HolidayBonusUje;
                default:
                    return 0d;
            }
        }

        private static double GetUjeBreakdownReferenceAmount(
            PeriodRuntimeDefinition definition,
            PeriodExpenseDefinition expenseDefinition)
        {
            switch (expenseDefinition.Id)
            {
                case "goods_services":
                    return expenseDefinition.UjeReferenceAmount > 0d
                        ? Math.Max(expenseDefinition.MinimumAmount, expenseDefinition.UjeReferenceAmount)
                        : ScaleThreshold(definition, GoodsServicesUpperBase);
                case "housing_rent":
                    return expenseDefinition.MinimumAmount > 0d
                        ? expenseDefinition.MinimumAmount
                        : ScaleThreshold(definition, HousingRentBase);
                case "leisure":
                    return ScaleThreshold(definition, LeisureUpperBase);
                case "holiday":
                    return expenseDefinition.MaximumAmount > 0d
                        ? expenseDefinition.MaximumAmount
                        : ScaleThreshold(definition, HolidayBase);
                default:
                    return expenseDefinition.MinimumAmount > 0d
                        ? expenseDefinition.MinimumAmount
                        : 0d;
            }
        }

        private static double ScaleThreshold(PeriodRuntimeDefinition definition, double baseValue)
        {
            var multiplier = definition != null && definition.EconomyContext != null
                ? Math.Max(0.0001d, definition.EconomyContext.ExpenseInflationMultiplier)
                : 1d;
            return baseValue * multiplier;
        }

        private static double Lerp(double minX, double maxX, double currentX, double minY, double maxY)
        {
            if (maxX <= minX)
            {
                return maxY;
            }

            var progress = (currentX - minX) / (maxX - minX);
            progress = Math.Max(0d, Math.Min(1d, progress));
            return minY + (maxY - minY) * progress;
        }

        private static void ValidateIncomeRemainder(
            PeriodRuntimeDefinition definition,
            double remainingToAllocate,
            double tolerance,
            ICollection<PeriodValidationIssue> issues)
        {
            if (remainingToAllocate < -tolerance)
            {
                issues.Add(new PeriodValidationIssue(
                    "income_overspent",
                    "Располагаемый доход превышен.",
                    "remaining",
                    true));
                return;
            }

            if (definition.ValidationSettings.RequireAllIncomeAllocated
                && Math.Abs(remainingToAllocate) > tolerance)
            {
                issues.Add(new PeriodValidationIssue(
                    "income_not_allocated",
                    "Распределите весь располагаемый доход.",
                    "remaining",
                    true));
            }
        }

        private static void ValidateAssetBalances(
            PeriodRuntimeDefinition definition,
            double cashBalance,
            double depositBalance,
            double tolerance,
            ICollection<PeriodValidationIssue> issues)
        {
            if (!definition.ValidationSettings.PreventNegativeAssetBalances)
            {
                return;
            }

            var allowDebt = definition != null
                && definition.EconomyContext != null
                && definition.EconomyContext.HasPermanentIncomeLoss;

            if (!allowDebt && cashBalance < -tolerance)
            {
                issues.Add(new PeriodValidationIssue(
                    "cash_negative",
                    "Недостаточно средств в наличных.",
                    "asset.cash",
                    true));
            }

            if (depositBalance < -tolerance)
            {
                issues.Add(new PeriodValidationIssue(
                    "deposit_negative",
                    "На депозите недостаточно средств.",
                    "asset.deposit",
                    true));
            }
        }

        private static void ApplyDebtPenaltyIfNeeded(
            PeriodRuntimeDefinition definition,
            double cashBalance,
            ICollection<UjeBreakdownItem> ujeBreakdown,
            ref double projectedUjeDelta)
        {
            if (definition == null
                || definition.EconomyContext == null
                || !definition.EconomyContext.HasPermanentIncomeLoss
                || cashBalance >= -ComparisonTolerance)
            {
                return;
            }

            var debtAmount = Math.Abs(cashBalance);
            var penalty = -debtAmount * DebtUjePenaltyRate;
            projectedUjeDelta += penalty;

            if (ujeBreakdown != null)
            {
                ujeBreakdown.Add(new UjeBreakdownItem(
                    "debt_penalty",
                    "Штраф за долг",
                    penalty,
                    0d,
                    debtAmount));
            }
        }

        private static void ApplyResidenceOwnershipBonus(
            PeriodRuntimeDefinition definition,
            ICollection<UjeBreakdownItem> ujeBreakdown,
            ref double projectedUjeDelta)
        {
            if (definition == null || definition.ResidenceOwnership == null)
            {
                return;
            }

            projectedUjeDelta += ConsumerCreditMath.ApartmentOwnershipUjeBonus;

            if (ujeBreakdown != null)
            {
                ujeBreakdown.Add(new UjeBreakdownItem(
                    "apartment_ownership",
                    "Собственное жильё",
                    ConsumerCreditMath.ApartmentOwnershipUjeBonus,
                    ConsumerCreditMath.ApartmentOwnershipUjeBonus,
                    1d));
            }
        }

        private static void ApplyCashOperation(
            PeriodAssetOperationEntry operation,
            ref double cashBalance,
            ref double incomeAllocatedToAssets)
        {
            if (operation.Kind == AssetOperationKind.Deposit
                && operation.Source == FundsSourceType.CurrentIncome)
            {
                cashBalance += operation.Amount;
                incomeAllocatedToAssets += operation.Amount;
            }
        }

        private static void ApplyDepositOperation(
            PeriodAssetOperationEntry operation,
            ref double cashBalance,
            ref double depositBalance,
            ref double incomeAllocatedToAssets)
        {
            switch (operation.Kind)
            {
                case AssetOperationKind.Deposit:
                    if (operation.Source == FundsSourceType.CurrentIncome)
                    {
                        depositBalance += operation.Amount;
                        incomeAllocatedToAssets += operation.Amount;
                    }
                    else if (operation.Source == FundsSourceType.Cash)
                    {
                        cashBalance -= operation.Amount;
                        depositBalance += operation.Amount;
                    }
                    break;
                case AssetOperationKind.Withdraw:
                    depositBalance -= operation.Amount;
                    cashBalance += operation.Amount;
                    break;
            }
        }

        private static void ApplyPdsOperation(
            PeriodAssetOperationEntry operation,
            ref double cashBalance,
            ref double depositBalance,
            ref double incomeAllocatedToAssets)
        {
            if (operation.Kind != AssetOperationKind.Deposit)
            {
                return;
            }

            switch (operation.Source)
            {
                case FundsSourceType.CurrentIncome:
                    incomeAllocatedToAssets += operation.Amount;
                    break;
                case FundsSourceType.Cash:
                    cashBalance -= operation.Amount;
                    break;
                case FundsSourceType.Deposit:
                    depositBalance -= operation.Amount;
                    break;
            }
        }

        private static PeriodExpenseState FindExpenseState(
            IReadOnlyList<PeriodExpenseState> expenses,
            string expenseId)
        {
            if (expenses == null || string.IsNullOrWhiteSpace(expenseId))
            {
                return null;
            }

            for (var index = 0; index < expenses.Count; index++)
            {
                var item = expenses[index];

                if (item != null && string.Equals(item.ExpenseId, expenseId, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private static bool IsCashAsset(string assetId)
        {
            return string.Equals(assetId, "cash", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDepositAsset(string assetId)
        {
            return string.Equals(assetId, "deposit", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPdsAsset(string assetId)
        {
            return string.Equals(assetId, ConsumerCreditMath.PdsAssetId, StringComparison.OrdinalIgnoreCase);
        }

        private static double GetInitialBalance(PeriodRuntimeDefinition definition, PeriodAssetType assetType)
        {
            switch (assetType)
            {
                case PeriodAssetType.Cash:
                    return definition.InitialCashBalance;
                case PeriodAssetType.Deposit:
                    return definition.InitialDepositBalance;
                case PeriodAssetType.Apartment:
                    if (definition == null || definition.ResidenceOwnership == null)
                    {
                        return 0d;
                    }

                    return definition.ResidenceOwnership.PurchasePeriodNumber >= definition.Meta.PeriodNumber
                        ? 0d
                        : ConsumerCreditMath.CalculateResidenceCurrentValue(
                            definition.ResidenceOwnership,
                            definition.EconomyContext);
                case PeriodAssetType.Pds:
                    if (definition == null || definition.PdsAccount == null)
                    {
                        return 0d;
                    }

                    return definition.PdsAccount.ActivationPeriodNumber >= definition.Meta.PeriodNumber
                        ? 0d
                        : Math.Max(0d, definition.PdsAccount.Balance);
                default:
                    return 0d;
            }
        }

        private static double GetCurrentBalance(
            PeriodRuntimeDefinition definition,
            PeriodAssetType assetType,
            double cashBalance,
            double depositBalance)
        {
            switch (assetType)
            {
                case PeriodAssetType.Cash:
                    return cashBalance;
                case PeriodAssetType.Deposit:
                    return depositBalance;
                case PeriodAssetType.Apartment:
                    return ConsumerCreditMath.CalculateResidenceCurrentValue(
                        definition != null ? definition.ResidenceOwnership : null,
                        definition != null ? definition.EconomyContext : null);
                case PeriodAssetType.Pds:
                    return definition != null && definition.PdsAccount != null
                        ? Math.Max(0d, definition.PdsAccount.Balance)
                        : 0d;
                default:
                    return 0d;
            }
        }
    }
}
