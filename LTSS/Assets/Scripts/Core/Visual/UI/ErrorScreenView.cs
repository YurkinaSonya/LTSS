using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.State;

public sealed class ErrorScreenView : ScreenView
{
    private Text _errorLabel;
    private Button _returnButton;
    private bool _isBuilt;

    protected override void Awake()
    {
        base.Awake();
        BuildUiIfNeeded();
    }

    public override ScreenController Construct(Game.Core.Application.UI.UIContext context)
    {
        return new ErrorScreenController(this, context);
    }

    public void BindReturn(System.Action callback)
    {
        if (_returnButton == null)
        {
            return;
        }

        _returnButton.onClick.RemoveAllListeners();

        if (callback != null)
        {
            _returnButton.onClick.AddListener(() => callback.Invoke());
        }
    }

    public void SetError(string error)
    {
        if (_errorLabel != null)
        {
            _errorLabel.text = string.IsNullOrWhiteSpace(error)
                ? "Unknown fatal error."
                : error;
        }
    }

    private void BuildUiIfNeeded()
    {
        if (_isBuilt)
        {
            return;
        }

        var background = RuntimeUiFactory.CreateFullscreenPanel(
            "Background",
            transform,
            new Color(0.10f, 0.04f, 0.05f, 0.98f));
        var card = RuntimeUiFactory.CreateCard(
            "Card",
            background,
            720f,
            new Color(0.22f, 0.09f, 0.10f, 0.95f));

        RuntimeUiFactory.CreateText(
            "Title",
            card,
            "Fatal Error",
            30,
            new Color(1f, 0.86f, 0.86f, 1f),
            TextAnchor.MiddleCenter,
            FontStyle.Bold);

        RuntimeUiFactory.CreateText(
            "Description",
            card,
            "The client could not produce a valid session runtime. Reset local state and return to login.",
            16,
            new Color(1f, 1f, 1f, 0.74f));

        _errorLabel = RuntimeUiFactory.CreateText(
            "ErrorLabel",
            card,
            string.Empty,
            16,
            new Color(1f, 0.82f, 0.82f, 1f));

        _returnButton = RuntimeUiFactory.CreateButton(
            "ReturnButton",
            card,
            "Return to Login",
            new Color(0.88f, 0.72f, 0.31f, 1f),
            new Color(0.11f, 0.10f, 0.07f, 1f));

        _isBuilt = true;
    }
}

public sealed class ErrorScreenController : ScreenController
{
    private readonly ErrorScreenView _view;

    public ErrorScreenController(
        ErrorScreenView view,
        Game.Core.Application.UI.UIContext context)
        : base(view, context)
    {
        _view = view;
    }

    public override void Open()
    {
        base.Open();
        _view.BindReturn(OnReturn);
        Context.StateStore.StateChanged += ApplyState;
        ApplyState(Context.StateStore.Current);
    }

    public override void Dispose()
    {
        Context.StateStore.StateChanged -= ApplyState;
        _view.BindReturn(null);
    }

    private void OnReturn()
    {
        Context.SessionCoordinator?.ClearSession("fatal_error_reset");
    }

    private void ApplyState(ApplicationStateSnapshot state)
    {
        _view.SetError(state.LastError);
    }
}
