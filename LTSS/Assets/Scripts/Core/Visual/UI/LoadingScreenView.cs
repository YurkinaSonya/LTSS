using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.State;

public sealed class LoadingScreenView : ScreenView
{
    private Text _titleLabel;
    private Text _statusLabel;
    private bool _isBuilt;

    protected override void Awake()
    {
        base.Awake();
        BuildUiIfNeeded();
    }

    public override ScreenController Construct(Game.Core.Application.UI.UIContext context)
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

    private void BuildUiIfNeeded()
    {
        if (_isBuilt)
        {
            return;
        }

        var background = RuntimeUiFactory.CreateFullscreenPanel(
            "Background",
            transform,
            new Color(0.04f, 0.06f, 0.08f, 0.98f));
        var card = RuntimeUiFactory.CreateCard(
            "Card",
            background,
            600f,
            new Color(0.10f, 0.13f, 0.17f, 0.92f));

        _titleLabel = RuntimeUiFactory.CreateText(
            "Title",
            card,
            "Preparing Session",
            30,
            new Color(0.97f, 0.97f, 0.97f, 1f),
            TextAnchor.MiddleCenter,
            FontStyle.Bold);

        _statusLabel = RuntimeUiFactory.CreateText(
            "Status",
            card,
            "Please wait...",
            18,
            new Color(1f, 1f, 1f, 0.76f));

        RuntimeUiFactory.CreateText(
            "Hint",
            card,
            "The client is resolving authentication, bootstrap and local recovery state.",
            15,
            new Color(1f, 1f, 1f, 0.46f));

        _isBuilt = true;
    }
}

public sealed class LoadingScreenController : ScreenController
{
    private readonly LoadingScreenView _view;

    public LoadingScreenController(LoadingScreenView view, Game.Core.Application.UI.UIContext context)
        : base(view, context)
    {
        _view = view;
    }

    public override void Open()
    {
        base.Open();
        Context.StateStore.StateChanged += ApplyState;
        ApplyState(Context.StateStore.Current);
    }

    public override void Dispose()
    {
        Context.StateStore.StateChanged -= ApplyState;
    }

    private void ApplyState(ApplicationStateSnapshot state)
    {
        var title = state.AppStateId == Game.Core.Application.AppStateId.Authenticating
            ? "Authenticating"
            : state.AppStateId == Game.Core.Application.AppStateId.LoadingSession
                ? "Loading Session"
                : "Preparing Session";

        var status = string.IsNullOrWhiteSpace(state.StatusMessage)
            ? "Please wait..."
            : state.StatusMessage;

        _view.SetContent(title, status);
    }
}
