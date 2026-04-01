using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.UI;

public sealed class LoginScreenView : ScreenView
{
    [Header("Form References")]
    [SerializeField] private InputField _loginInput;
    [SerializeField] private InputField _passwordInput;
    [SerializeField] private Button _submitButton;
    [SerializeField] private Text _errorLabel;

    public override ScreenController Construct(UIContext context)
    {
        return new LoginScreenController(this, context);
    }

    public void BindSubmit(Action<string, string> callback)
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
}
