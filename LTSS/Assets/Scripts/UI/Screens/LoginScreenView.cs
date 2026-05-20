using System;
using System.Runtime.InteropServices;
using Game.Core.Application.UI;
using UnityEngine;
using UnityEngine.UI;

public sealed class LoginScreenView : ScreenView
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void LTSS_InitClipboardBridge(string gameObjectName, string callbackMethodName);

    [DllImport("__Internal")]
    private static extern void LTSS_SetClipboardBridgeEnabled(bool enabled);
#endif

    private InputField _loginInput;
    private InputField _passwordInput;
    private Button _submitButton;
    private Text _errorLabel;
    private bool _clipboardBridgeInitialized;
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

    public void HandleBrowserPaste(string pastedText)
    {
        EnsureBuilt();

        var targetInput = ResolveFocusedInput();

        if (targetInput == null || string.IsNullOrEmpty(pastedText))
        {
            return;
        }

        InsertText(targetInput, NormalizeSingleLineText(pastedText));
    }

    private void OnEnable()
    {
        SetClipboardBridgeEnabledSafe(true);
    }

    private void OnDisable()
    {
        SetClipboardBridgeEnabledSafe(false);
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
        EnsureClipboardBridgeInitialized();
    }

    private void EnsureClipboardBridgeInitialized()
    {
        if (_clipboardBridgeInitialized)
        {
            return;
        }

        _clipboardBridgeInitialized = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        LTSS_InitClipboardBridge(gameObject.name, nameof(HandleBrowserPaste));
#endif

        SetClipboardBridgeEnabledSafe(isActiveAndEnabled);
    }

    private void SetClipboardBridgeEnabledSafe(bool enabled)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_clipboardBridgeInitialized)
        {
            LTSS_SetClipboardBridgeEnabled(enabled);
        }
#endif
    }

    private InputField ResolveFocusedInput()
    {
        if (_passwordInput != null && _passwordInput.isFocused)
        {
            return _passwordInput;
        }

        if (_loginInput != null && _loginInput.isFocused)
        {
            return _loginInput;
        }

        return null;
    }

    private static string NormalizeSingleLineText(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\r\n", string.Empty)
                .Replace("\n", string.Empty)
                .Replace("\r", string.Empty);
    }

    private static void InsertText(InputField input, string pastedText)
    {
        if (input == null || string.IsNullOrEmpty(pastedText))
        {
            return;
        }

        var currentText = input.text ?? string.Empty;
        var selectionStart = currentText.Length;
        var selectionEnd = currentText.Length;

        if (input.isFocused)
        {
            selectionStart = Mathf.Clamp(Math.Min(input.selectionAnchorPosition, input.selectionFocusPosition), 0, currentText.Length);
            selectionEnd = Mathf.Clamp(Math.Max(input.selectionAnchorPosition, input.selectionFocusPosition), 0, currentText.Length);
        }

        var nextText = currentText.Substring(0, selectionStart)
                       + pastedText
                       + currentText.Substring(selectionEnd);
        var nextCaretPosition = selectionStart + pastedText.Length;

        input.text = nextText;
        input.caretPosition = nextCaretPosition;
        input.selectionAnchorPosition = nextCaretPosition;
        input.selectionFocusPosition = nextCaretPosition;
        input.ForceLabelUpdate();
    }
}
