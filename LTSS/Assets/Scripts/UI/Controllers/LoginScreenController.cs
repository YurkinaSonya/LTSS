using Game.Core.Application.State;
using Game.Core.Application.UI;

public sealed class LoginScreenController : ScreenController
{
    private readonly LoginScreenView _view;

    public LoginScreenController(LoginScreenView view, UIContext context)
        : base(view, context)
    {
        _view = view;
    }

    public override void Open()
    {
        base.Open();

        _view.BindSubmit(OnSubmit);
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

        _view.BindSubmit(null);
    }

    private void OnSubmit(string login, string password)
    {
        _view.SetError(string.Empty);
        Context.SessionCoordinator?.Login(login, password);
    }

    private void ApplyState(ApplicationStateSnapshot state)
    {
        if (_view == null || state == null)
        {
            return;
        }

        _view.SetBusy(state.IsBusy);
        _view.SetError(state.LastError);
    }
}
