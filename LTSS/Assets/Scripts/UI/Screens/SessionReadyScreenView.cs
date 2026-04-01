using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.Session;
using Game.Core.Application.UI;

public sealed class SessionReadyScreenView : ScreenView
{
    [Header("Summary References")]
    [SerializeField] private Text _sessionTitleLabel;
    [SerializeField] private Text _sessionCodeLabel;
    [SerializeField] private Text _participantLabel;
    [SerializeField] private Text _runLabel;
    [SerializeField] private Text _surveyLabel;
    [SerializeField] private Text _configLabel;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Button _logoutButton;

    public override ScreenController Construct(UIContext context)
    {
        return new SessionReadyScreenController(this, context);
    }

    public void BindContinue(Action callback)
    {
        BindButton(_continueButton, callback);
    }

    public void BindLogout(Action callback)
    {
        BindButton(_logoutButton, callback);
    }

    public void ApplyRuntime(ClientRuntimeState runtimeState)
    {
        if (runtimeState == null || !runtimeState.HasSession)
        {
            SetText(_sessionTitleLabel, "No session runtime");
            SetText(_sessionCodeLabel, "Bootstrap payload is not available.");
            SetText(_participantLabel, string.Empty);
            SetText(_runLabel, string.Empty);
            SetText(_surveyLabel, string.Empty);
            SetText(_configLabel, string.Empty);
            return;
        }

        var session = runtimeState.Bootstrap.Session;
        var participant = runtimeState.Bootstrap.Participant;
        var run = runtimeState.Bootstrap.Run;
        var templates = runtimeState.Bootstrap.SurveyTemplates;

        SetText(
            _sessionTitleLabel,
            string.IsNullOrWhiteSpace(session.Title) ? "Untitled Session" : session.Title);
        SetText(_sessionCodeLabel, $"Code: {session.Code}  |  Status: {session.Status}");

        var participantBuilder = new StringBuilder();
        participantBuilder.Append("Participant: ");
        participantBuilder.Append(string.IsNullOrWhiteSpace(participant.Login) ? "-" : participant.Login);
        participantBuilder.Append("  |  Group: ");
        participantBuilder.Append(string.IsNullOrWhiteSpace(participant.AssignedGroupCode)
            ? "-"
            : participant.AssignedGroupCode);
        SetText(_participantLabel, participantBuilder.ToString());

        SetText(
            _runLabel,
            $"Run: {run.RunId}  |  Status: {run.RunStatus}  |  Current period: {run.CurrentPeriodNumber}");

        SetText(
            _surveyLabel,
            $"Survey templates: {templates.Count}  |  Bootstrap version: {runtimeState.Bootstrap.BootstrapVersion}");

        var periodSummary = session.SessionConfig.PeriodCount.HasValue
            ? session.SessionConfig.PeriodCount.Value.ToString()
            : "n/a";

        SetText(
            _configLabel,
            $"Config v{session.ConfigVersion}  |  Session config: {session.SessionConfig.Summary}  |  " +
            $"Assigned config: {participant.AssignedConfig.Summary}  |  Periods: {periodSummary}");
    }

    private static void BindButton(Button button, Action callback)
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

    private static void SetText(Text label, string value)
    {
        if (label != null)
        {
            label.text = value ?? string.Empty;
        }
    }
}
