using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.UI;

public sealed class LoadingScreenView : ScreenView
{
    private Text _titleLabel;
    private Text _statusLabel;
    private bool _isBuilt;

    public override ScreenController Construct(UIContext context)
    {
        EnsureBuilt();
        return new LoadingScreenController(this, context);
    }

    public void SetContent(string title, string status)
    {
        EnsureBuilt();

        if (_titleLabel != null)
        {
            _titleLabel.text = title ?? string.Empty;
        }

        if (_statusLabel != null)
        {
            _statusLabel.text = status ?? string.Empty;
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
        var card = RuntimeUiFactory.CreateCard("LoadingCard", background, new Vector2(420f, 220f));
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(32, 32, 34, 28),
            12f,
            TextAnchor.MiddleCenter);

        RuntimeUiFactory.AddSpacer(content, 8f);
        _titleLabel = RuntimeUiFactory.CreateTitle(content, "Загрузка");
        _statusLabel = RuntimeUiFactory.CreateCaption(content, "Подождите...", TextAnchor.MiddleCenter);
    }
}
