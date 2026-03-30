using Game.Core.Application.UI;

public class ScreenController
{
    private readonly ScreenView _view;

    protected UIContext Context { get; }

    public ScreenController(ScreenView view, UIContext context)
    {
        _view = view;
        Context = context;
    }

    public virtual void Open()
    {
        _view?.OpenScreen();
    }

    public void Close()
    {
        Dispose();

        if (_view == null)
        {
            return;
        }

        _view.CloseScreen();
        _view.DestroyScreenView();
    }

    public virtual void Dispose()
    {
    }
}
