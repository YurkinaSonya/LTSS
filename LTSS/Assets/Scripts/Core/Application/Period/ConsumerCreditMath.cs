using System;
using Game.Domain.GameFlow;

namespace Game.Core.Application.Periods
{
    public static class ConsumerCreditMath
    {
        public const int DefaultTermPeriods = 5;

        public static double CalculateAnnuityPayment(double principal, double? rawRatePercent, int periods = DefaultTermPeriods)
        {
            var safePrincipal = Math.Max(0d, principal);
            var safePeriods = periods > 0 ? periods : DefaultTermPeriods;

            if (safePrincipal <= 0d)
            {
                return 0d;
            }

            var rate = NormalizePercentageToRate(rawRatePercent);

            if (rate <= 0d)
            {
                return safePrincipal / safePeriods;
            }

            var denominator = 1d - Math.Pow(1d + rate, -safePeriods);
            return denominator <= 0d
                ? safePrincipal / safePeriods
                : safePrincipal * rate / denominator;
        }

        public static double CalculateNextRemainingPrincipal(
            double remainingPrincipal,
            double paymentAmount,
            double? rawRatePercent)
        {
            var safePrincipal = Math.Max(0d, remainingPrincipal);
            var safePayment = Math.Max(0d, paymentAmount);
            var rate = NormalizePercentageToRate(rawRatePercent);
            var nextPrincipal = safePrincipal * (1d + Math.Max(0d, rate)) - safePayment;
            return nextPrincipal > 0d ? nextPrincipal : 0d;
        }

        public static double CalculateCreditPotential(PeriodRuntimeDefinition definition)
        {
            if (definition == null)
            {
                return 0d;
            }

            var income = Math.Max(0d, definition.CalculationSettings.CurrentIncomeEcu);
            var inflationMultiplier = definition.EconomyContext != null
                ? Math.Max(0.0001d, definition.EconomyContext.ExpenseInflationMultiplier)
                : 1d;
            var mandatoryBaseAmount = 0d;

            for (var index = 0; index < definition.ExpenseDefinitions.Count; index++)
            {
                var expense = definition.ExpenseDefinitions[index];

                if (expense == null)
                {
                    continue;
                }

                if (!string.Equals(expense.Id, "goods_services", StringComparison.Ordinal)
                    && !string.Equals(expense.Id, "housing_rent", StringComparison.Ordinal))
                {
                    continue;
                }

                mandatoryBaseAmount += expense.MinimumAmount > 0d
                    ? expense.MinimumAmount / inflationMultiplier
                    : 0d;
            }

            return Math.Max(0d, income - mandatoryBaseAmount);
        }

        public static string BuildExpenseId(string creditId)
        {
            return string.IsNullOrWhiteSpace(creditId)
                ? "consumer_credit_payment"
                : $"consumer_credit_payment:{creditId}";
        }

        public static bool IsCreditExpenseId(string expenseId)
        {
            return !string.IsNullOrWhiteSpace(expenseId)
                   && expenseId.StartsWith("consumer_credit_payment", StringComparison.Ordinal);
        }

        public static string ExtractCreditId(string expenseId)
        {
            if (string.IsNullOrWhiteSpace(expenseId))
            {
                return string.Empty;
            }

            var separatorIndex = expenseId.IndexOf(':');
            return separatorIndex >= 0 && separatorIndex < expenseId.Length - 1
                ? expenseId.Substring(separatorIndex + 1)
                : string.Empty;
        }

        public static double NormalizePercentageToRate(double? rawPercent)
        {
            if (!rawPercent.HasValue || rawPercent.Value <= 0d)
            {
                return 0d;
            }

            return rawPercent.Value / 100d;
        }
    }
}
