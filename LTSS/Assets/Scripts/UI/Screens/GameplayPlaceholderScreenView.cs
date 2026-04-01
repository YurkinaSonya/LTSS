using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.UI;

public sealed class GameplayPlaceholderScreenView : ScreenView
{
    private Text _detailsLabel;
    private Button _backButton;
    private bool _isBuilt;

    public override ScreenController Construct(UIContext context)
    {
        EnsureBuilt();
        return new GameplayPlaceholderScreenController(this, context);
    }

    public void BindBack(Action callback)
    {
        EnsureBuilt();

        if (_backButton == null)
        {
            return;
        }

        _backButton.onClick.RemoveAllListeners();

        if (callback != null)
        {
            _backButton.onClick.AddListener(() => callback.Invoke());
        }
    }

    public void SetDetails(string text)
    {
        EnsureBuilt();

        if (_detailsLabel != null)
        {
            _detailsLabel.text = text ?? string.Empty;
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
        var card = RuntimeUiFactory.CreateCard("GameplayCard", background, new Vector2(700f, 420f));
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(34, 34, 34, 30),
            12f);

        RuntimeUiFactory.CreateTitle(content, "Сессия", TextAnchor.MiddleLeft);
        _detailsLabel = RuntimeUiFactory.CreateCaption(content, string.Empty);
        RuntimeUiFactory.AddSpacer(content, 8f);
        _backButton = RuntimeUiFactory.CreateSecondaryButton(content, "Назад");
    }
}
