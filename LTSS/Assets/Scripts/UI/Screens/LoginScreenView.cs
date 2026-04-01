using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.UI;

public sealed class LoginScreenView : ScreenView
{
    private InputField _loginInput;
    private InputField _passwordInput;
    private Button _submitButton;
    private Text _errorLabel;
    private bool _isBuilt;

    public override ScreenController Construct(UIContext context)
    {
        EnsureBuilt();
        return new LoginScreenController(this, context);
    }

    public void BindSubmit(Action<string, string> callback)
    {
        EnsureBuilt();

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
        EnsureBuilt();

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
        EnsureBuilt();

        if (_errorLabel == null)
        {
            return;
        }

        _errorLabel.text = message ?? string.Empty;
        _errorLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(_errorLabel.text));
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        var background = RuntimeUiFactory.CreateScreenBackground(transform);
        var card = RuntimeUiFactory.CreateCard("LoginCard", background, new Vector2(460f, 404f));
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(32, 32, 34, 32),
            16f,
            TextAnchor.UpperCenter);

        RuntimeUiFactory.CreateTitle(content, "Вход");
        //RuntimeUiFactory.CreateCaption(content, "Логин и пароль", TextAnchor.MiddleCenter);
        RuntimeUiFactory.AddSpacer(content, 6f);

        var formContent = RuntimeUiFactory.CreatePanel(
            "FormPanel",
            content,
            new RectOffset(18, 18, 18, 18),
            12f,
            RuntimeUiFactory.ElevatedSurfaceColor);

        _loginInput = RuntimeUiFactory.CreateInputField(formContent, "Логин");
        _passwordInput = RuntimeUiFactory.CreateInputField(formContent, "Пароль", true);
        _errorLabel = RuntimeUiFactory.CreateErrorText(formContent);

        RuntimeUiFactory.AddSpacer(content, 2f);
        _submitButton = RuntimeUiFactory.CreatePrimaryButton(content, "Войти");
    }
}
