using Game.Core.Application.State;
using Game.Core.Application.UI;

public sealed class ErrorScreenController : ScreenController
{
    private readonly ErrorScreenView _view;

    public ErrorScreenController(ErrorScreenView view, UIContext context)
        : base(view, context)
    {
        _view = view;
    }

    public override void Open()
    {
        base.Open();

        _view.BindReturn(OnReturn);
        ApplyState(Context.StateStore != null
            ? Context.StateStore.Current
            : null);

        if (Context.StateStore != null)
        {
            Context.StateStore.StateChanged += ApplyState;
        }
    }

    public override void Dispose()
    {
        if (Context.StateStore != null)
        {
            Context.StateStore.StateChanged -= ApplyState;
        }

        _view.BindReturn(null);
    }

    private void OnReturn()
    {
        Context.SessionCoordinator?.ClearSession("fatal_error_return");
    }

    private void ApplyState(ApplicationStateSnapshot state)
    {
        _view?.SetError(state?.LastError);
    }
}
