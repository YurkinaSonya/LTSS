using Game.Core.Application.Periods;
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
            OnBack,
            OnComplete,
            OnExpenseAmountChanged,
            OnApplyRequiredExpenseAmount,
            OnExpenseSourceChanged,
            OnAssetAction,
            OnConsumerCreditAction,
            OnMortgageAction);
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

    private void OnMortgageAction()
    {
        Context.PeriodGameplay?.OpenMortgageDialog();
    }
}
