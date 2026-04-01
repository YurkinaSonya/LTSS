using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.UI;

public sealed class LoadingScreenView : ScreenView
{
    [Header("Content References")]
    [SerializeField] private Text _titleLabel;
    [SerializeField] private Text _statusLabel;

    public override ScreenController Construct(UIContext context)
    {
        return new LoadingScreenController(this, context);
    }

    public void SetContent(string title, string status)
    {
        if (_titleLabel != null)
        {
            _titleLabel.text = title ?? string.Empty;
        }

        if (_statusLabel != null)
        {
            _statusLabel.text = status ?? string.Empty;
        }
    }
}
