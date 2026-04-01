using Game.Domain.GameFlow;

namespace Game.Core.Application.Periods
{
    public static class PeriodContractMapper
    {
        public static FundsSourceType ToFundsSource(string rawValue)
        {
            switch ((rawValue ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "currentincome":
                case "current_income":
                case "income":
                case "salary":
                    return FundsSourceType.CurrentIncome;
                case "cash":
                case "cash_balance":
                case "wallet":
                    return FundsSourceType.Cash;
                case "deposit":
                case "savings":
                    return FundsSourceType.Deposit;
                default:
                    return FundsSourceType.Unknown;
            }
        }

        public static string ToFundsSourceCode(FundsSourceType value)
        {
            switch (value)
            {
                case FundsSourceType.CurrentIncome:
                    return "current_income";
                case FundsSourceType.Cash:
                    return "cash";
                case FundsSourceType.Deposit:
                    return "deposit";
                default:
                    return "unknown";
            }
        }

        public static string ToFundsSourceLabel(FundsSourceType value)
        {
            switch (value)
            {
                case FundsSourceType.CurrentIncome:
                    return "Текущий доход";
                case FundsSourceType.Cash:
                    return "Наличные";
                case FundsSourceType.Deposit:
                    return "Депозит";
                default:
                    return "Источник";
            }
        }

        public static AssetOperationKind ToAssetOperationKind(string rawValue)
        {
            switch ((rawValue ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "deposit":
                case "topup":
                case "add":
                    return AssetOperationKind.Deposit;
                case "withdraw":
                case "takeout":
                case "remove":
                    return AssetOperationKind.Withdraw;
                default:
                    return AssetOperationKind.None;
            }
        }

        public static string ToAssetOperationKindCode(AssetOperationKind value)
        {
            switch (value)
            {
                case AssetOperationKind.Deposit:
                    return "deposit";
                case AssetOperationKind.Withdraw:
                    return "withdraw";
                default:
                    return "none";
            }
        }

        public static PeriodFlowState ToPeriodFlowState(string rawValue)
        {
            switch ((rawValue ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "loadingdata":
                case "loading_data":
                    return PeriodFlowState.LoadingData;
                case "periodintro":
                case "period_intro":
                case "intro":
                    return PeriodFlowState.PeriodIntro;
                case "periodactive":
                case "period_active":
                case "active":
                    return PeriodFlowState.PeriodActive;
                case "periodvalidation":
                case "period_validation":
                case "validation":
                    return PeriodFlowState.PeriodValidation;
                case "periodclosing":
                case "period_closing":
                case "closing":
                    return PeriodFlowState.PeriodClosing;
                case "periodcheckpointsubmitting":
                case "period_checkpoint_submitting":
                case "submitting":
                    return PeriodFlowState.PeriodCheckpointSubmitting;
                case "periodclosed":
                case "period_closed":
                case "closed":
                    return PeriodFlowState.PeriodClosed;
                default:
                    return PeriodFlowState.None;
            }
        }

        public static string ToPeriodFlowStateCode(PeriodFlowState value)
        {
            switch (value)
            {
                case PeriodFlowState.LoadingData:
                    return "loading_data";
                case PeriodFlowState.PeriodIntro:
                    return "period_intro";
                case PeriodFlowState.PeriodActive:
                    return "period_active";
                case PeriodFlowState.PeriodValidation:
                    return "period_validation";
                case PeriodFlowState.PeriodClosing:
                    return "period_closing";
                case PeriodFlowState.PeriodCheckpointSubmitting:
                    return "period_checkpoint_submitting";
                case PeriodFlowState.PeriodClosed:
                    return "period_closed";
                default:
                    return "none";
            }
        }

        public static GameFlowPhase ToGameFlowPhase(PeriodFlowState value)
        {
            switch (value)
            {
                case PeriodFlowState.LoadingData:
                    return GameFlowPhase.LoadingData;
                case PeriodFlowState.PeriodIntro:
                    return GameFlowPhase.PeriodIntro;
                case PeriodFlowState.PeriodValidation:
                    return GameFlowPhase.PeriodValidation;
                case PeriodFlowState.PeriodClosing:
                    return GameFlowPhase.PeriodClosing;
                case PeriodFlowState.PeriodCheckpointSubmitting:
                    return GameFlowPhase.PeriodCheckpointSubmitting;
                case PeriodFlowState.PeriodClosed:
                    return GameFlowPhase.PeriodClosed;
                case PeriodFlowState.PeriodActive:
                    return GameFlowPhase.PeriodActive;
                default:
                    return GameFlowPhase.Preparation;
            }
        }
    }
}
