using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.Logging;
using Game.Core.Application.State;

public sealed class LoginScreenView : ScreenView
{
    private InputField _loginInput;
    private InputField _passwordInput;
    private Button _submitButton;
    private Text _errorLabel;
    private bool _isBuilt;

    protected override void Awake()
    {
        base.Awake();
        BuildUiIfNeeded();
    }

    public override ScreenController Construct(Game.Core.Application.UI.UIContext context)
    {
        return new LoginScreenController(this, context);
    }

    public void BindSubmit(System.Action<string, string> callback)
    {
        if (_submitButton == null)
        {
            return;
        }

        _submitButton.onClick.RemoveAllListeners();

        if (callback != null)
        {
            _submitButton.onClick.AddListener(() =>
            {
                callback.Invoke(
                    _loginInput != null ? _loginInput.text : string.Empty,
                    _passwordInput != null ? _passwordInput.text : string.Empty);
            });
        }
    }

    public void SetBusy(bool isBusy)
    {
        if (_loginInput != null)
        {
            _loginInput.interactable = !isBusy;
        }

        if (_passwordInput != null)
        {
            _passwordInput.interactable = !isBusy;
        }

        if (_submitButton != null)
        {
            _submitButton.interactable = !isBusy;
        }
    }

    public void SetError(string message)
    {
        if (_errorLabel == null)
        {
            return;
        }

        _errorLabel.text = message ?? string.Empty;
        _errorLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(_errorLabel.text));
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
            new Color(0.05f, 0.07f, 0.09f, 0.96f));
        var card = RuntimeUiFactory.CreateCard(
            "Card",
            background,
            560f,
            new Color(0.10f, 0.13f, 0.17f, 0.94f));

        RuntimeUiFactory.CreateText(
            "Title",
            card,
            "Session Login",
            32,
            new Color(0.97f, 0.97f, 0.97f, 1f),
            TextAnchor.MiddleCenter,
            FontStyle.Bold);

        RuntimeUiFactory.CreateText(
            "Subtitle",
            card,
            "Authenticate and load the current experimental session bootstrap.",
            16,
            new Color(1f, 1f, 1f, 0.72f));

        _loginInput = RuntimeUiFactory.CreateInputField("LoginInput", card, "Login", false);
        _passwordInput = RuntimeUiFactory.CreateInputField("PasswordInput", card, "Password", true);

        _errorLabel = RuntimeUiFactory.CreateText(
            "ErrorLabel",
            card,
            string.Empty,
            15,
            new Color(1f, 0.52f, 0.52f, 1f));
        _errorLabel.gameObject.SetActive(false);

        _submitButton = RuntimeUiFactory.CreateButton(
            "SubmitButton",
            card,
            "Enter Session",
            new Color(0.88f, 0.72f, 0.31f, 1f),
            new Color(0.11f, 0.10f, 0.07f, 1f));

        _isBuilt = true;
    }
}

public sealed class LoginScreenController : ScreenController
{
    private readonly LoginScreenView _view;

    public LoginScreenController(LoginScreenView view, Game.Core.Application.UI.UIContext context)
        : base(view, context)
    {
        _view = view;
    }

    public override void Open()
    {
        base.Open();

        _view.BindSubmit(OnSubmit);
        Context.StateStore.StateChanged += ApplyState;
        ApplyState(Context.StateStore.Current);
    }

    public override void Dispose()
    {
        Context.StateStore.StateChanged -= ApplyState;
        _view.BindSubmit(null);
    }

    private void OnSubmit(string login, string password)
    {
        Context.UserActions?.Log(UserActionType.Interaction, "login_submit");
        Context.SessionCoordinator?.Login(login, password);
    }

    private void ApplyState(ApplicationStateSnapshot state)
    {
        _view.SetBusy(state.IsBusy);
        _view.SetError(state.LastError);
    }
}
