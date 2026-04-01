using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.UI;

public sealed class ErrorScreenView : ScreenView
{
    private Text _errorLabel;
    private Button _returnButton;
    private bool _isBuilt;

    public override ScreenController Construct(UIContext context)
    {
        EnsureBuilt();
        return new ErrorScreenController(this, context);
    }

    public void BindReturn(Action callback)
    {
        EnsureBuilt();

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
        EnsureBuilt();

        if (_errorLabel != null)
        {
            _errorLabel.text = string.IsNullOrWhiteSpace(error)
                ? "Неизвестная ошибка."
                : error;
        }
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        var background = RuntimeUiFactory.CreateScreenBackground(transform);
        var card = RuntimeUiFactory.CreateCard("ErrorCard", background, new Vector2(520f, 280f));
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(32, 32, 34, 30),
            14f,
            TextAnchor.UpperCenter);

        RuntimeUiFactory.CreateTitle(content, "Ошибка");
        _errorLabel = RuntimeUiFactory.CreateCaption(content, string.Empty, TextAnchor.MiddleCenter);
        RuntimeUiFactory.AddSpacer(content, 6f);
        _returnButton = RuntimeUiFactory.CreatePrimaryButton(content, "Назад");
    }
}
