using System;
using System.Collections.Generic;
using Game.Domain.GameFlow;
using UnityEngine;

namespace Game.Core.Application.Periods
{
    public sealed class PeriodCalculationEngine : IPeriodCalculationEngine
    {
        private const double DefaultReferenceAmount = 1d;

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
                }
            }

            var uje = definition.CalculationSettings.BaseUje;

            foreach (var expenseDefinition in definition.ExpenseDefinitions)
            {
                if (expenseDefinition == null)
                {
                    continue;
                }

                var state = FindExpenseState(safeExpenses, expenseDefinition.Id);
                var amount = state != null ? Math.Max(0d, state.Amount) : 0d;
                var referenceAmount = expenseDefinition.UjeReferenceAmount > 0d
                    ? expenseDefinition.UjeReferenceAmount
                    : expenseDefinition.MinimumAmount > 0d
                        ? expenseDefinition.MinimumAmount
                        : DefaultReferenceAmount;
                var ratio = referenceAmount > 0d
                    ? Math.Min(1d, amount / referenceAmount)
                    : 0d;
                var contribution = Math.Max(0d, expenseDefinition.UjeWeight) * ratio;

                uje += contribution;
                ujeBreakdown.Add(new UjeBreakdownItem(
                    expenseDefinition.Id,
                    expenseDefinition.Title,
                    contribution,
                    expenseDefinition.UjeWeight,
                    referenceAmount));

                ValidateExpense(definition, expenseDefinition, state, validationIssues);
            }

            uje = Math.Max(0d, definition.CalculationSettings.MaximumUje > 0d
                ? Math.Min(definition.CalculationSettings.MaximumUje, uje)
                : uje);

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
                    GetCurrentBalance(assetDefinition.AssetType, cashBalance, depositBalance),
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

            if (cashBalance < -tolerance)
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

        private static double GetInitialBalance(PeriodRuntimeDefinition definition, PeriodAssetType assetType)
        {
            switch (assetType)
            {
                case PeriodAssetType.Cash:
                    return definition.InitialCashBalance;
                case PeriodAssetType.Deposit:
                    return definition.InitialDepositBalance;
                default:
                    return 0d;
            }
        }

        private static double GetCurrentBalance(
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
                default:
                    return 0d;
            }
        }
    }
}
