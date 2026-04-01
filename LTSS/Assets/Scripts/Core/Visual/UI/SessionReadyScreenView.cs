using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.Session;

public sealed class SessionReadyScreenView : ScreenView
{
    private Text _sessionTitleLabel;
    private Text _sessionCodeLabel;
    private Text _participantLabel;
    private Text _runLabel;
    private Text _surveyLabel;
    private Text _configLabel;
    private Button _continueButton;
    private Button _logoutButton;
    private bool _isBuilt;

    protected override void Awake()
    {
        base.Awake();
        BuildUiIfNeeded();
    }

    public override ScreenController Construct(Game.Core.Application.UI.UIContext context)
    {
        return new SessionReadyScreenController(this, context);
    }

    public void BindContinue(System.Action callback)
    {
        BindButton(_continueButton, callback);
    }

    public void BindLogout(System.Action callback)
    {
        BindButton(_logoutButton, callback);
    }

    public void ApplyRuntime(ClientRuntimeState runtimeState)
    {
        if (runtimeState == null || !runtimeState.HasSession)
        {
            _sessionTitleLabel.text = "No session runtime";
            _sessionCodeLabel.text = "Bootstrap payload is not available.";
            _participantLabel.text = string.Empty;
            _runLabel.text = string.Empty;
            _surveyLabel.text = string.Empty;
            _configLabel.text = string.Empty;
            return;
        }

        var session = runtimeState.Bootstrap.Session;
        var participant = runtimeState.Bootstrap.Participant;
        var run = runtimeState.Bootstrap.Run;
        var templates = runtimeState.Bootstrap.SurveyTemplates;

        _sessionTitleLabel.text = string.IsNullOrWhiteSpace(session.Title)
            ? "Untitled Session"
            : session.Title;
        _sessionCodeLabel.text = $"Code: {session.Code}  |  Status: {session.Status}";

        var participantBuilder = new StringBuilder();
        participantBuilder.Append("Participant: ");
        participantBuilder.Append(string.IsNullOrWhiteSpace(participant.Login) ? "-" : participant.Login);
        participantBuilder.Append("  |  Group: ");
        participantBuilder.Append(string.IsNullOrWhiteSpace(participant.AssignedGroupCode)
            ? "-"
            : participant.AssignedGroupCode);
        _participantLabel.text = participantBuilder.ToString();

        _runLabel.text =
            $"Run: {run.RunId}  |  Status: {run.RunStatus}  |  Current period: {run.CurrentPeriodNumber}";

        _surveyLabel.text =
            $"Survey templates: {templates.Count}  |  Bootstrap version: {runtimeState.Bootstrap.BootstrapVersion}";

        var sessionConfig = session.SessionConfig;
        var assignedConfig = participant.AssignedConfig;
        var periodSummary = sessionConfig.PeriodCount.HasValue
            ? sessionConfig.PeriodCount.Value.ToString()
            : "n/a";

        _configLabel.text =
            $"Config v{session.ConfigVersion}  |  Session config: {sessionConfig.Summary}  |  " +
            $"Assigned config: {assignedConfig.Summary}  |  Periods: {periodSummary}";
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
            760f,
            new Color(0.11f, 0.15f, 0.19f, 0.95f));

        RuntimeUiFactory.CreateText(
            "Header",
            card,
            "Session Runtime Ready",
            30,
            new Color(0.98f, 0.98f, 0.98f, 1f),
            TextAnchor.MiddleCenter,
            FontStyle.Bold);

        RuntimeUiFactory.CreateText(
            "Description",
            card,
            "Technical landing screen with real bootstrap data. This is the handoff point for gameplay flow implementation.",
            16,
            new Color(1f, 1f, 1f, 0.72f));

        _sessionTitleLabel = RuntimeUiFactory.CreateText(
            "SessionTitle",
            card,
            string.Empty,
            24,
            new Color(0.98f, 0.96f, 0.82f, 1f),
            TextAnchor.MiddleCenter,
            FontStyle.Bold);

        _sessionCodeLabel = RuntimeUiFactory.CreateText(
            "SessionCode",
            card,
            string.Empty,
            16,
            new Color(1f, 1f, 1f, 0.82f));

        _participantLabel = RuntimeUiFactory.CreateText(
            "ParticipantInfo",
            card,
            string.Empty,
            17,
            new Color(1f, 1f, 1f, 0.92f));

        _runLabel = RuntimeUiFactory.CreateText(
            "RunInfo",
            card,
            string.Empty,
            17,
            new Color(1f, 1f, 1f, 0.92f));

        _surveyLabel = RuntimeUiFactory.CreateText(
            "SurveyInfo",
            card,
            string.Empty,
            17,
            new Color(1f, 1f, 1f, 0.92f));

        _configLabel = RuntimeUiFactory.CreateText(
            "ConfigInfo",
            card,
            string.Empty,
            15,
            new Color(1f, 1f, 1f, 0.66f));

        var buttonsRow = RuntimeUiFactory.CreateElement("Buttons", card);
        RuntimeUiFactory.AddLayoutElement(buttonsRow, preferredHeight: 56f);
        RuntimeUiFactory.AddHorizontalLayout(buttonsRow, 16f);

        _continueButton = RuntimeUiFactory.CreateButton(
            "ContinueButton",
            buttonsRow,
            "Enter Session",
            new Color(0.88f, 0.72f, 0.31f, 1f),
            new Color(0.11f, 0.10f, 0.07f, 1f));

        _logoutButton = RuntimeUiFactory.CreateButton(
            "LogoutButton",
            buttonsRow,
            "Log Out",
            new Color(1f, 1f, 1f, 0.10f),
            new Color(0.95f, 0.95f, 0.95f, 1f));

        _isBuilt = true;
    }

    private static void BindButton(Button button, System.Action callback)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();

        if (callback != null)
        {
            button.onClick.AddListener(() => callback.Invoke());
        }
    }
}

public sealed class SessionReadyScreenController : ScreenController
{
    private readonly SessionReadyScreenView _view;

    public SessionReadyScreenController(
        SessionReadyScreenView view,
        Game.Core.Application.UI.UIContext context)
        : base(view, context)
    {
        _view = view;
    }

    public override void Open()
    {
        base.Open();

        _view.BindContinue(OnContinue);
        _view.BindLogout(OnLogout);
        Context.SessionCoordinator.RuntimeChanged += ApplyRuntime;
        ApplyRuntime(Context.SessionCoordinator.CurrentRuntime);
    }

    public override void Dispose()
    {
        Context.SessionCoordinator.RuntimeChanged -= ApplyRuntime;
        _view.BindContinue(null);
        _view.BindLogout(null);
    }

    private void OnContinue()
    {
        Context.Navigation?.StartGameplay("session_ready_continue");
    }

    private void OnLogout()
    {
        Context.SessionCoordinator?.ClearSession("session_ready_logout");
    }

    private void ApplyRuntime(ClientRuntimeState runtimeState)
    {
        _view.ApplyRuntime(runtimeState);
    }
}
