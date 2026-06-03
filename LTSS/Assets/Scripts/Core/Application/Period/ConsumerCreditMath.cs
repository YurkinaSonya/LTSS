using System;
using Game.Domain.GameFlow;

namespace Game.Core.Application.Periods
{
    public static class ConsumerCreditMath
    {
        public const double DefaultBaseIncomeEcu = 100d;
        public const string ConsumerCreditKind = "consumer_credit";
        public const string MortgageKind = "mortgage";
        public const int DefaultTermPeriods = 5;
        public const int MortgageTermPeriods = 30;
        public const double ApartmentCostIncomeMultiplier = 3d;
        public const double MortgageDownPaymentRate = 0.2d;
        public const double ApartmentOwnershipUjeBonus = 20d;
        public const double PensionContributionRate = 0.06d;
        public const string ManualPdsEnrollmentGroupCode = "pds_calc";
        public const int PdsProjectionPeriods = 15;
        public const int PdsEnhancedMatchPeriods = 6;
        public const double PdsBaseContributionThreshold = 3d;
        public const double PdsEnhancedContributionThreshold = 33d;
        public const double PdsBaseMatchAmount = 3d;
        public const double PdsEnhancedMatchAmount = 6d;
        public const double EducationTargetIncomeMultiplier = 1.3d;
        public const double EducationIncomeMultiplier = 1.5d;
        public const int EducationIncomeDelayPeriods = 4;
        public const string ApartmentResidenceId = "apartment";
        public const string PdsAssetId = "pds";
        public const string DirectApartmentPurchaseMode = "direct";
        public const string MortgageApartmentPurchaseMode = "mortgage";

        public static double CalculateApartmentCost(PeriodRuntimeDefinition definition)
        {
            return definition != null
                ? CalculateApartmentCost(definition.EconomyContext)
                : 0d;
        }

        public static double CalculateApartmentCost(PeriodEconomyContext economyContext)
        {
            return CalculateApartmentCost(ResolveReferenceIncome(economyContext));
        }

        public static double CalculateApartmentCost(double referenceIncomeEcu)
        {
            return Math.Max(0d, referenceIncomeEcu) * ApartmentCostIncomeMultiplier;
        }

        public static double CalculateMortgageDownPayment(PeriodRuntimeDefinition definition)
        {
            return definition != null
                ? CalculateMortgageDownPayment(definition.EconomyContext)
                : 0d;
        }

        public static double CalculateMortgageDownPayment(PeriodEconomyContext economyContext)
        {
            return CalculateApartmentCost(economyContext) * MortgageDownPaymentRate;
        }

        public static double CalculateMortgagePrincipal(PeriodRuntimeDefinition definition)
        {
            return definition != null
                ? CalculateMortgagePrincipal(definition.EconomyContext)
                : 0d;
        }

        public static double CalculateMortgagePrincipal(PeriodEconomyContext economyContext)
        {
            var apartmentCost = CalculateApartmentCost(economyContext);
            var downPayment = CalculateMortgageDownPayment(economyContext);
            return Math.Max(0d, apartmentCost - downPayment);
        }

        public static double CalculateEducationTargetAmount(PeriodRuntimeDefinition definition)
        {
            return definition != null
                ? CalculateEducationTargetAmount(definition.EconomyContext)
                : 0d;
        }

        public static double CalculateEducationTargetAmount(PeriodEconomyContext economyContext)
        {
            return CalculateEducationTargetAmount(ResolveReferenceIncome(economyContext));
        }

        public static double CalculateEducationTargetAmount(double referenceIncomeEcu)
        {
            return Math.Max(0d, referenceIncomeEcu) * EducationTargetIncomeMultiplier;
        }

        public static double CalculatePdsContributionBonus(
            double totalContributionAmount,
            int activationPeriodNumber,
            int currentPeriodNumber)
        {
            if (activationPeriodNumber <= 0 || currentPeriodNumber <= 0)
            {
                return CalculatePdsContributionBonus(totalContributionAmount, PdsEnhancedMatchPeriods + 1);
            }

            var participationPeriodNumber = currentPeriodNumber - activationPeriodNumber + 1;
            return CalculatePdsContributionBonus(totalContributionAmount, participationPeriodNumber);
        }

        public static double CalculatePdsContributionBonus(
            double totalContributionAmount,
            int participationPeriodNumber)
        {
            var contribution = Math.Max(0d, totalContributionAmount);

            if (contribution + 0.01d < PdsBaseContributionThreshold)
            {
                return 0d;
            }

            if (contribution + 0.01d >= PdsEnhancedContributionThreshold
                && participationPeriodNumber > 0
                && participationPeriodNumber <= PdsEnhancedMatchPeriods)
            {
                return PdsEnhancedMatchAmount;
            }

            return PdsBaseMatchAmount;
        }

        public static double CalculatePdsProjectedBalance(
            double openingBalance,
            double recurringContributionAmount,
            double initialTransferAmount,
            double? ratePercent,
            int startingParticipationPeriodNumber = 1,
            int projectionPeriods = PdsProjectionPeriods)
        {
            var balance = Math.Max(0d, openingBalance);
            var contribution = Math.Max(0d, recurringContributionAmount);
            var transfer = Math.Max(0d, initialTransferAmount);
            var periods = projectionPeriods > 0 ? projectionPeriods : PdsProjectionPeriods;
            var rate = NormalizePercentageToRate(ratePercent);

            for (var index = 0; index < periods; index++)
            {
                if (index == 0 && transfer > 0d)
                {
                    balance += transfer;
                }

                if (contribution > 0d)
                {
                    balance += contribution;
                    balance += CalculatePdsContributionBonus(contribution, startingParticipationPeriodNumber + index);
                }

                if (rate > 0d)
                {
                    balance *= 1d + rate;
                }
            }

            return balance;
        }

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
            var mandatoryBaseAmount = 0d;

            for (var index = 0; index < definition.ExpenseDefinitions.Count; index++)
            {
                var expense = definition.ExpenseDefinitions[index];

                if (expense == null)
                {
                    continue;
                }

                if (!string.Equals(expense.Id, "goods_services", StringComparison.Ordinal)
                    && !string.Equals(expense.Id, "housing_rent", StringComparison.Ordinal)
                    && !string.Equals(expense.Id, "child_expense", StringComparison.Ordinal))
                {
                    continue;
                }

                mandatoryBaseAmount += Math.Max(0d, expense.MinimumAmount);
            }

            var basePotential = Math.Max(0d, income - mandatoryBaseAmount);
            var activeConsumerCreditPrincipal = 0d;

            if (definition.ConsumerCredits != null)
            {
                for (var index = 0; index < definition.ConsumerCredits.Count; index++)
                {
                    var credit = definition.ConsumerCredits[index];

                    if (credit == null
                        || !string.Equals(credit.ContractType, ConsumerCreditKind, StringComparison.Ordinal)
                        || credit.RemainingPrincipal <= 0.01d)
                    {
                        continue;
                    }

                    activeConsumerCreditPrincipal += Math.Max(0d, credit.RemainingPrincipal);
                }
            }

            return Math.Max(0d, basePotential - activeConsumerCreditPrincipal);
        }

        public static string BuildExpenseId(string creditId)
        {
            return BuildExpenseId(ConsumerCreditKind, creditId);
        }

        public static string BuildExpenseId(string contractType, string creditId)
        {
            var prefix = string.Equals(contractType, MortgageKind, StringComparison.Ordinal)
                ? "mortgage_payment"
                : "consumer_credit_payment";

            return string.IsNullOrWhiteSpace(creditId)
                ? prefix
                : $"{prefix}:{creditId}";
        }

        public static bool IsCreditExpenseId(string expenseId)
        {
            return !string.IsNullOrWhiteSpace(expenseId)
                   && (expenseId.StartsWith("consumer_credit_payment", StringComparison.Ordinal)
                       || expenseId.StartsWith("mortgage_payment", StringComparison.Ordinal));
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

        public static string ExtractContractType(string expenseId)
        {
            if (string.IsNullOrWhiteSpace(expenseId))
            {
                return string.Empty;
            }

            if (expenseId.StartsWith("mortgage_payment", StringComparison.Ordinal))
            {
                return MortgageKind;
            }

            if (expenseId.StartsWith("consumer_credit_payment", StringComparison.Ordinal))
            {
                return ConsumerCreditKind;
            }

            return string.Empty;
        }

        public static double NormalizePercentageToRate(double? rawPercent)
        {
            if (!rawPercent.HasValue || rawPercent.Value <= 0d)
            {
                return 0d;
            }

            return rawPercent.Value / 100d;
        }

        public static double CalculateResidenceCurrentValue(
            ResidenceOwnershipRuntime residenceOwnership,
            PeriodEconomyContext economyContext)
        {
            if (residenceOwnership == null)
            {
                return 0d;
            }

            var basePrice = Math.Max(0d, residenceOwnership.PurchasePrice);
            var purchaseInflationMultiplier = residenceOwnership.PurchaseInflationMultiplier > 0d
                ? residenceOwnership.PurchaseInflationMultiplier
                : 1d;
            var currentInflationMultiplier = economyContext != null
                ? Math.Max(0.0001d, economyContext.ExpenseInflationMultiplier)
                : 1d;

            return basePrice * currentInflationMultiplier / purchaseInflationMultiplier;
        }

        private static double ResolveReferenceIncome(PeriodEconomyContext economyContext)
        {
            if (economyContext == null)
            {
                return DefaultBaseIncomeEcu;
            }

            if (economyContext.ReferenceIncomeEcu > 0d)
            {
                return economyContext.ReferenceIncomeEcu;
            }

            if (economyContext.CurrentIncomeEcu > 0d)
            {
                return economyContext.CurrentIncomeEcu;
            }

            return economyContext.BaseIncomeEcu > 0d
                ? economyContext.BaseIncomeEcu
                : DefaultBaseIncomeEcu;
        }
    }
}
