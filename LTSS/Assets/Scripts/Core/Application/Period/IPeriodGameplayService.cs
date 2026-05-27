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
        void ApplyRequiredExpenseAmount(string expenseId);
        void SetExpenseSource(string expenseId, FundsSourceType source);
        void CycleExpenseSource(string expenseId);
        void OpenAssetDialog(string assetId, AssetOperationKind kind);
        void OpenConsumerCreditDialog();
        void OpenApartmentPurchaseDialog();
        void OpenApartmentSaleDialog();
        void OpenMortgageDialog();
        void OpenPdsDialog();
        void SetAssetDialogSource(FundsSourceType source);
        void CycleAssetDialogSource();
        void SubmitAssetDialog(string rawAmount);
        bool TrySubmitConsumerCredit(string rawAmount, out string errorMessage);
        bool TryBuyApartment(out string errorMessage);
        bool TrySellApartment(out string errorMessage);
        bool TrySubmitMortgage(out string errorMessage);
        bool TryActivatePds(string rawContributionAmount, string rawPensionTransferAmount, out string errorMessage);
        bool TrySubmitPds(string rawContributionAmount, string rawPensionTransferAmount, out string errorMessage);
        void CloseAssetDialog();
        void ApplyPermanentIncomeLoss();
        void SubmitPeriod();
        void ClearRuntime();
    }
}
