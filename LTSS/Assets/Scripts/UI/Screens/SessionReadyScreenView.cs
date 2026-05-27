using System;
using Game.Core.Application.Session;
using Game.Core.Application.UI;
using UnityEngine;
using UnityEngine.UI;

public sealed class SessionReadyScreenView : ScreenView
{
    private Text _sessionTitleLabel;
    private Text _sessionCodeLabel;
    private Text _participantLabel;
    private Text _runLabel;
    private Text _surveyLabel;
    private Text _configLabel;
    private Text _statusLabel;
    private Button _testerSkipButton;
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

    public void BindTesterSkip(Action callback)
    {
        EnsureBuilt();
        BindButton(_testerSkipButton, callback);
    }

    public void ApplyRuntime(
        ClientRuntimeState runtimeState,
        string statusMessage,
        bool canContinue,
        string continueButtonText,
        bool showTesterSkipButton)
    {
        EnsureBuilt();

        if (runtimeState == null || !runtimeState.HasSession)
        {
            SetText(_sessionTitleLabel, string.Empty);
            SetText(_sessionCodeLabel, string.Empty);
            SetText(_participantLabel, string.Empty);
            SetText(_runLabel, string.Empty);
            SetText(_surveyLabel, string.Empty);
            SetText(_configLabel, string.Empty);
            SetText(_statusLabel, statusMessage);
            _statusLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(statusMessage));
            _continueButton.interactable = false;
            RuntimeUiFactory.SetButtonText(_continueButton, continueButtonText);
            _testerSkipButton.gameObject.SetActive(false);
            return;
        }

        var participant = runtimeState.Bootstrap.Participant;
        SetText(_sessionTitleLabel, string.Empty);
        SetText(_sessionCodeLabel, string.Empty);
        SetText(
            _participantLabel,
            $"Вы вошли как {(!string.IsNullOrWhiteSpace(participant.Login) ? participant.Login : "пользователь")}");
        SetText(_runLabel, string.Empty);
        SetText(_surveyLabel, string.Empty);
        SetText(_configLabel, string.Empty);
        SetText(_statusLabel, statusMessage);
        _statusLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(statusMessage));
        _continueButton.interactable = canContinue;
        RuntimeUiFactory.SetButtonText(_continueButton, continueButtonText);
        _testerSkipButton.gameObject.SetActive(showTesterSkipButton);
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        var background = RuntimeUiFactory.CreateScreenBackground(transform);
        var card = RuntimeUiFactory.CreateCard("SessionCard", background, new Vector2(720f, 460f));
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(34, 34, 34, 30),
            14f);

        var headerRow = RuntimeUiFactory.CreateRow("Header", content, 12f, TextAnchor.MiddleCenter);
        var headerElement = headerRow.gameObject.AddComponent<LayoutElement>();
        headerElement.preferredHeight = 46f;
        headerElement.flexibleWidth = 1f;

        _sessionTitleLabel = RuntimeUiFactory.CreateTitle(headerRow, string.Empty, TextAnchor.MiddleLeft);
        _testerSkipButton = RuntimeUiFactory.CreateSecondaryButton(headerRow, "Тестировщик", 44f);
        var testerElement = _testerSkipButton.gameObject.GetComponent<LayoutElement>();

        if (testerElement != null)
        {
            testerElement.preferredWidth = 170f;
            testerElement.flexibleWidth = 0f;
        }

        _testerSkipButton.gameObject.SetActive(false);

        RuntimeUiFactory.AddFlexibleSpacer(content);
        _sessionCodeLabel = RuntimeUiFactory.CreateCaption(content, string.Empty, TextAnchor.MiddleCenter);
        _sessionCodeLabel.gameObject.SetActive(false);
        _participantLabel = RuntimeUiFactory.CreateValueText(content, string.Empty, 30, TextAnchor.MiddleCenter);
        _runLabel = RuntimeUiFactory.CreateBodyText(content, string.Empty, TextAnchor.MiddleCenter);
        _runLabel.gameObject.SetActive(false);
        _surveyLabel = RuntimeUiFactory.CreateCaption(content, string.Empty, TextAnchor.MiddleCenter);
        _surveyLabel.gameObject.SetActive(false);
        _configLabel = RuntimeUiFactory.CreateCaption(content, string.Empty, TextAnchor.MiddleCenter);
        _configLabel.gameObject.SetActive(false);
        _statusLabel = RuntimeUiFactory.CreateBodyText(content, string.Empty);
        _statusLabel.color = RuntimeUiFactory.PrimaryColor;
        _statusLabel.gameObject.SetActive(false);
        RuntimeUiFactory.AddFlexibleSpacer(content);

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
