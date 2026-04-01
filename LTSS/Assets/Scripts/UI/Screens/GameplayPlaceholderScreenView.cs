using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.UI;

public sealed class GameplayPlaceholderScreenView : ScreenView
{
    [Header("Content References")]
    [SerializeField] private Text _detailsLabel;
    [SerializeField] private Button _backButton;

    public override ScreenController Construct(UIContext context)
    {
        return new GameplayPlaceholderScreenController(this, context);
    }

    public void BindBack(Action callback)
    {
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
        if (_detailsLabel != null)
        {
            _detailsLabel.text = text ?? string.Empty;
        }
    }
}
