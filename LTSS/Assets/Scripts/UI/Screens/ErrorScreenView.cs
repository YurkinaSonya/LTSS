using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.UI;

public sealed class ErrorScreenView : ScreenView
{
    [Header("Content References")]
    [SerializeField] private Text _errorLabel;
    [SerializeField] private Button _returnButton;

    public override ScreenController Construct(UIContext context)
    {
        return new ErrorScreenController(this, context);
    }

    public void BindReturn(Action callback)
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
}
