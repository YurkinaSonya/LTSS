using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Game.Core.Application.Session;
using Game.Core.Application.UI;

public sealed class SessionReadyScreenView : ScreenView
{
    private Text _sessionTitleLabel;
    private Text _sessionCodeLabel;
    private Text _participantLabel;
    private Text _runLabel;
    private Text _surveyLabel;
    private Text _configLabel;
    private Text _statusLabel;
    private Button _continueButton;
    private Button _logoutButton;
    private bool _isBuilt;

    public override ScreenController Construct(UIContext context)
    {
        EnsureBuilt();
        return new SessionReadyScreenController(this, context);
    }

    public void BindContinue(Action callback)
    {
        EnsureBuilt();
        BindButton(_continueButton, callback);
    }

    public void BindLogout(Action callback)
    {
        EnsureBuilt();
        BindButton(_logoutButton, callback);
    }

    public void ApplyRuntime(
        ClientRuntimeState runtimeState,
        string statusMessage,
        bool canContinue,
        string continueButtonText)
    {
        EnsureBuilt();

        if (runtimeState == null || !runtimeState.HasSession)
        {
            SetText(_sessionTitleLabel, "Сессия не загружена");
            SetText(_sessionCodeLabel, "Данные пока недоступны.");
            SetText(_participantLabel, string.Empty);
            SetText(_runLabel, string.Empty);
            SetText(_surveyLabel, string.Empty);
            SetText(_configLabel, string.Empty);
            SetText(_statusLabel, statusMessage);
            _statusLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(statusMessage));
            _continueButton.interactable = false;
            RuntimeUiFactory.SetButtonText(_continueButton, continueButtonText);
            return;
        }

        var session = runtimeState.Bootstrap.Session;
        var participant = runtimeState.Bootstrap.Participant;
        var run = runtimeState.Bootstrap.Run;
        var templates = runtimeState.Bootstrap.SurveyTemplates;

        SetText(
            _sessionTitleLabel,
            string.IsNullOrWhiteSpace(session.Title) ? "Без названия" : session.Title);
        SetText(_sessionCodeLabel, $"Код: {session.Code}  |  Статус: {session.Status}");

        var participantBuilder = new StringBuilder();
        participantBuilder.Append("Участник: ");
        participantBuilder.Append(string.IsNullOrWhiteSpace(participant.Login) ? "-" : participant.Login);
        participantBuilder.Append("  |  Группа: ");
        participantBuilder.Append(string.IsNullOrWhiteSpace(participant.AssignedGroupCode)
            ? "-"
            : participant.AssignedGroupCode);
        SetText(_participantLabel, participantBuilder.ToString());

        SetText(
            _runLabel,
            $"Запуск: {run.RunId}  |  Статус: {run.RunStatus}  |  Период: {run.CurrentPeriodNumber}");

        SetText(
            _surveyLabel,
            $"Анкеты: {templates.Count}  |  Версия bootstrap: {runtimeState.Bootstrap.BootstrapVersion}");

        var periodSummary = session.SessionConfig.PeriodCount.HasValue
            ? session.SessionConfig.PeriodCount.Value.ToString()
            : "нет";

        SetText(
            _configLabel,
            $"Конфиг v{session.ConfigVersion}  |  Сессия: {session.SessionConfig.Summary}  |  " +
            $"Группа: {participant.AssignedConfig.Summary}  |  Этапов: {periodSummary}");
        SetText(_statusLabel, statusMessage);
        _statusLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(statusMessage));
        _continueButton.interactable = canContinue;
        RuntimeUiFactory.SetButtonText(_continueButton, continueButtonText);
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        var background = RuntimeUiFactory.CreateScreenBackground(transform);
        var card = RuntimeUiFactory.CreateCard("SessionCard", background, new Vector2(720f, 520f));
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(34, 34, 34, 30),
            12f);

        _sessionTitleLabel = RuntimeUiFactory.CreateTitle(content, "Сессия", TextAnchor.MiddleLeft);
        _sessionCodeLabel = RuntimeUiFactory.CreateCaption(content, string.Empty);
        RuntimeUiFactory.AddSpacer(content, 8f);

        _participantLabel = RuntimeUiFactory.CreateBodyText(content, string.Empty);
        _runLabel = RuntimeUiFactory.CreateBodyText(content, string.Empty);
        _surveyLabel = RuntimeUiFactory.CreateBodyText(content, string.Empty);
        _configLabel = RuntimeUiFactory.CreateCaption(content, string.Empty);
        _statusLabel = RuntimeUiFactory.CreateBodyText(content, string.Empty);
        _statusLabel.color = RuntimeUiFactory.PrimaryColor;
        _statusLabel.gameObject.SetActive(false);
        RuntimeUiFactory.AddSpacer(content, 14f);

        var buttonRow = RuntimeUiFactory.CreateRow("Buttons", content, 12f, TextAnchor.MiddleCenter);
        _continueButton = RuntimeUiFactory.CreatePrimaryButton(buttonRow, "Дальше");
        _logoutButton = RuntimeUiFactory.CreateSecondaryButton(buttonRow, "Выйти");
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
