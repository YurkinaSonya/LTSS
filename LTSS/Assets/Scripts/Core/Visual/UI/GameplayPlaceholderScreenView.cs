using UnityEngine;
using UnityEngine.UI;

public sealed class GameplayPlaceholderScreenView : ScreenView
{
    private Text _detailsLabel;
    private Button _backButton;
    private bool _isBuilt;

    protected override void Awake()
    {
        base.Awake();
        BuildUiIfNeeded();
    }

    public override ScreenController Construct(Game.Core.Application.UI.UIContext context)
    {
        return new GameplayPlaceholderScreenController(this, context);
    }

    public void BindBack(System.Action callback)
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

    private void BuildUiIfNeeded()
    {
        if (_isBuilt)
        {
            return;
        }

        var background = RuntimeUiFactory.CreateFullscreenPanel(
            "Background",
            transform,
            new Color(0.05f, 0.07f, 0.09f, 0.97f));
        var card = RuntimeUiFactory.CreateCard(
            "Card",
            background,
            700f,
            new Color(0.10f, 0.13f, 0.17f, 0.94f));

        RuntimeUiFactory.CreateText(
            "Title",
            card,
            "Gameplay Flow Placeholder",
            30,
            new Color(0.98f, 0.98f, 0.98f, 1f),
            TextAnchor.MiddleCenter,
            FontStyle.Bold);

        RuntimeUiFactory.CreateText(
            "Description",
            card,
            "Session runtime has been loaded successfully. The next iteration can attach actual period logic here.",
            16,
            new Color(1f, 1f, 1f, 0.72f));

        _detailsLabel = RuntimeUiFactory.CreateText(
            "Details",
            card,
            string.Empty,
            16,
            new Color(1f, 1f, 1f, 0.84f));

        _backButton = RuntimeUiFactory.CreateButton(
            "BackButton",
            card,
            "Back to Session",
            new Color(0.88f, 0.72f, 0.31f, 1f),
            new Color(0.11f, 0.10f, 0.07f, 1f));

        _isBuilt = true;
    }
}

public sealed class GameplayPlaceholderScreenController : ScreenController
{
    private readonly GameplayPlaceholderScreenView _view;

    public GameplayPlaceholderScreenController(
        GameplayPlaceholderScreenView view,
        Game.Core.Application.UI.UIContext context)
        : base(view, context)
    {
        _view = view;
    }

    public override void Open()
    {
        base.Open();
        _view.BindBack(OnBack);
        ApplyRuntime(Context.SessionCoordinator.CurrentRuntime);
        Context.SessionCoordinator.RuntimeChanged += ApplyRuntime;
    }

    public override void Dispose()
    {
        Context.SessionCoordinator.RuntimeChanged -= ApplyRuntime;
        _view.BindBack(null);
    }

    private void OnBack()
    {
        Context.Navigation?.ShowSessionReady("gameplay_placeholder_back");
    }

    private void ApplyRuntime(Game.Core.Application.Session.ClientRuntimeState runtimeState)
    {
        if (runtimeState == null || !runtimeState.HasSession)
        {
            _view.SetDetails("Session runtime is not available.");
            return;
        }

        _view.SetDetails(
            $"Run {runtimeState.AuthenticatedRun.RunId} is loaded. " +
            $"Current period: {runtimeState.Bootstrap.Run.CurrentPeriodNumber}. " +
            $"Survey templates available: {runtimeState.Bootstrap.SurveyTemplates.Count}.");
    }
}
