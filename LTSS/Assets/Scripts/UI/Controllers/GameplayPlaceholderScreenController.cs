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
        _view?.Render(
            runtimeState ?? PeriodRuntimeState.Empty,
            OnBack,
            OnComplete,
            OnExpenseAmountChanged,
            OnExpenseSourceToggle,
            OnAssetAction);
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

    private void OnExpenseSourceToggle(string expenseId)
    {
        Context.PeriodGameplay?.CycleExpenseSource(expenseId);
    }

    private void OnAssetAction(string assetId, AssetOperationKind kind)
    {
        Context.PeriodGameplay?.OpenAssetDialog(assetId, kind);
    }
}
