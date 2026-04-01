using System;
using Game.Domain.GameFlow;

namespace Game.Core.Application.Periods
{
    public interface IPeriodGameplayService
    {
        PeriodRuntimeState Current { get; }
        event Action<PeriodRuntimeState> Changed;

        void ActivateCurrentPeriod();
        void SetExpenseAmount(string expenseId, string rawAmount);
        void CycleExpenseSource(string expenseId);
        void OpenAssetDialog(string assetId, AssetOperationKind kind);
        void CycleAssetDialogSource();
        void SubmitAssetDialog(string rawAmount);
        void CloseAssetDialog();
        void SubmitPeriod();
        void ClearRuntime();
    }
}
