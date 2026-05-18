using System;
using Game.Core.Application.Periods;
using Game.Domain.GameFlow;
using UnityEngine;
using UnityEngine.UI;

[PopupDefinition(Enums.PopupType.Pds)]
public sealed class PdsPopup : Popup
{
    private Text _titleLabel;
    private Text _subtitleLabel;
    private Text _availableLabel;
    private Text _currentPdsLabel;
    private Text _messageLabel;
    private InputField _amountInput;
    private Button _confirmButton;
    private Button _cancelButton;
    private bool _isBuilt;

    public override Enums.PopupType PopupType => Enums.PopupType.Pds;

    protected override void OnInitialize()
    {
        EnsureBuilt();
        BindButtons();

        if (Context.PeriodGameplay != null)
        {
            Context.PeriodGameplay.Changed += ApplyRuntime;
            ApplyRuntime(Context.PeriodGameplay.Current);
        }
        else
        {
            SetMessage("Сервис периода недоступен.");
        }
    }

    public override void Close(Action callback = null)
    {
        if (Context.PeriodGameplay != null)
        {
            Context.PeriodGameplay.Changed -= ApplyRuntime;
        }

        base.Close(callback);
    }

    private void BindButtons()
    {
        BindButton(_cancelButton, () => Context.Popups?.Pop("pds_cancel"));
        BindButton(_confirmButton, HandleConfirm);
    }

    private void HandleConfirm()
    {
        if (Context.PeriodGameplay == null)
        {
            SetMessage("Сервис периода недоступен.");
            return;
        }

        if (Context.PeriodGameplay.TrySubmitPds(ReadAmountText(), out var errorMessage))
        {
            Context.Popups?.Pop("pds_confirm");
            return;
        }

        SetMessage(errorMessage);
    }

    private void ApplyRuntime(PeriodRuntimeState runtimeState)
    {
        var latestRuntime = Context != null && Context.PeriodGameplay != null
            ? Context.PeriodGameplay.Current
            : runtimeState;

        if (!_isBuilt || latestRuntime == null || !latestRuntime.HasDefinition)
        {
            return;
        }

        var definition = latestRuntime.Definition;
        var pensionReserve = definition.PensionReserve;
        var pdsAccount = definition.PdsAccount;
        var availableAmount = pensionReserve != null
            ? Math.Max(0d, pensionReserve.Balance)
            : 0d;
        var currentPdsAmount = pdsAccount != null
            ? Math.Max(0d, pdsAccount.Balance)
            : 0d;

        _titleLabel.text = "Программа долгосрочных сбережений";
        _subtitleLabel.text = "Переведите часть накоплений на пенсию в отдельный долгосрочный инструмент.";
        _availableLabel.text = $"Доступно пенсионных накоплений: {EcuFormatter.FormatAmount(availableAmount)}";
        _currentPdsLabel.text = pdsAccount != null
            ? $"Текущий баланс ПДС: {EcuFormatter.FormatAmount(currentPdsAmount)}"
            : "ПДС ещё не оформлена.";
        RuntimeUiFactory.SetButtonText(_confirmButton, pdsAccount != null ? "Пополнить" : "Активировать");
        _confirmButton.interactable = availableAmount > 0.01d;

        if (availableAmount <= 0.01d)
        {
            SetMessage("Нет доступных пенсионных накоплений для перевода в ПДС.");
        }
        else if (!string.IsNullOrWhiteSpace(_messageLabel.text)
                 && _messageLabel.text.Contains("Нет доступных пенсионных накоплений"))
        {
            SetMessage(string.Empty);
        }
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        var card = RuntimeUiFactory.CreateCard("PdsCard", transform, new Vector2(640f, 400f), false);
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(28, 28, 26, 24),
            12f);

        _titleLabel = RuntimeUiFactory.CreateTitle(content, "ПДС", TextAnchor.MiddleLeft);
        _subtitleLabel = RuntimeUiFactory.CreateCaption(content, string.Empty);

        var infoPanel = RuntimeUiFactory.CreateSurface("InfoPanel", content, RuntimeUiFactory.PrimarySoftColor);
        var infoLayout = infoPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        infoLayout.padding = new RectOffset(14, 14, 12, 12);
        infoLayout.spacing = 6f;
        infoLayout.childAlignment = TextAnchor.UpperLeft;
        infoLayout.childControlWidth = true;
        infoLayout.childControlHeight = true;
        infoLayout.childForceExpandWidth = true;
        infoLayout.childForceExpandHeight = false;

        _availableLabel = RuntimeUiFactory.CreateBodyText(infoPanel, string.Empty);
        _currentPdsLabel = RuntimeUiFactory.CreateCaption(infoPanel, string.Empty);

        RuntimeUiFactory.CreateCaption(content, "Сумма перевода в ПДС");
        _amountInput = RuntimeUiFactory.CreateInputField(content, "Введите сумму");
        NumericInputParser.Configure(_amountInput);
        _messageLabel = RuntimeUiFactory.CreateErrorText(content);

        RuntimeUiFactory.AddFlexibleSpacer(content);
        var actions = RuntimeUiFactory.CreateRow("Actions", content, 12f, TextAnchor.MiddleCenter);
        _cancelButton = RuntimeUiFactory.CreateSecondaryButton(actions, "Отмена", 46f);
        _confirmButton = RuntimeUiFactory.CreatePrimaryButton(actions, "Активировать", 46f);
    }

    private string ReadAmountText()
    {
        return _amountInput != null
            ? _amountInput.text ?? string.Empty
            : string.Empty;
    }

    private void SetMessage(string message)
    {
        if (_messageLabel == null)
        {
            return;
        }

        _messageLabel.text = message ?? string.Empty;
        _messageLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(_messageLabel.text));
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
}
