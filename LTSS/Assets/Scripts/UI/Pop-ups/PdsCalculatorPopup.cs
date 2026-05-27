using System;
using System.Globalization;
using Game.Core.Application.Periods;
using Game.Domain.GameFlow;
using UnityEngine;
using UnityEngine.UI;

[PopupDefinition(Enums.PopupType.PdsCalculator)]
public sealed class PdsCalculatorPopup : Popup
{
    private Text _titleLabel;
    private Text _subtitleLabel;
    private Text _availableIncomeLabel;
    private Text _availablePensionLabel;
    private Text _rateLabel;
    private Text _bonusLabel;
    private Text _projectionLabel;
    private Text _messageLabel;
    private InputField _contributionInput;
    private InputField _pensionTransferInput;
    private Button _confirmButton;
    private Button _cancelButton;
    private bool _isBuilt;

    public override Enums.PopupType PopupType => Enums.PopupType.PdsCalculator;

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

        if (_contributionInput != null)
        {
            _contributionInput.onValueChanged.AddListener(_ => RefreshProjection());
        }

        if (_pensionTransferInput != null)
        {
            _pensionTransferInput.onValueChanged.AddListener(_ => RefreshProjection());
        }
    }

    public override void Close(Action callback = null)
    {
        if (Context.PeriodGameplay != null)
        {
            Context.PeriodGameplay.Changed -= ApplyRuntime;
        }

        if (_contributionInput != null)
        {
            _contributionInput.onValueChanged.RemoveAllListeners();
        }

        if (_pensionTransferInput != null)
        {
            _pensionTransferInput.onValueChanged.RemoveAllListeners();
        }

        base.Close(callback);
    }

    private void BindButtons()
    {
        BindButton(_cancelButton, () => Context.Popups?.Pop("pds_calc_cancel"));
        BindButton(_confirmButton, HandleConfirm);
    }

    private void HandleConfirm()
    {
        if (Context.PeriodGameplay == null)
        {
            SetMessage("Сервис периода недоступен.");
            return;
        }

        if (Context.PeriodGameplay.TryActivatePds(
                ReadContributionText(),
                ReadPensionTransferText(),
                out var errorMessage))
        {
            Context.Popups?.Pop("pds_calc_confirm");
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
        var availableContribution = Math.Max(0d, latestRuntime.Summary.RemainingToAllocate);
        var availablePensionTransfer = pensionReserve != null
            ? Math.Max(0d, pensionReserve.Balance)
            : 0d;
        var depositRate = definition.EconomyContext != null && definition.EconomyContext.DepositRate.HasValue
            ? definition.EconomyContext.DepositRate.Value
            : 0d;

        _titleLabel.text = "Калькулятор ПДС";
        _subtitleLabel.text = "Оцените результат на горизонте 15 периодов и при желании сразу подключите программу.";
        _availableIncomeLabel.text = $"Свободно для взноса: {EcuFormatter.FormatAmount(availableContribution)}";
        _availablePensionLabel.text = $"Пенсионные накопления: {EcuFormatter.FormatAmount(availablePensionTransfer)}";
        _rateLabel.text = $"Доходность периода: {depositRate.ToString("0.##", CultureInfo.InvariantCulture)}%";
        _confirmButton.interactable = true;

        if (string.IsNullOrWhiteSpace(_messageLabel.text)
            && !string.IsNullOrWhiteSpace(latestRuntime.StatusMessage))
        {
            SetMessage(latestRuntime.StatusMessage);
        }

        RefreshProjection();
    }

    private void RefreshProjection()
    {
        var runtimeState = Context != null && Context.PeriodGameplay != null
            ? Context.PeriodGameplay.Current
            : PeriodRuntimeState.Empty;

        if (runtimeState == null || !runtimeState.HasDefinition)
        {
            _bonusLabel.text = "Софинансирование за период: -";
            _projectionLabel.text = "Через 15 периодов: -";
            return;
        }

        NumericInputParser.TryParseNonNegativeAmount(ReadContributionText(), out var contributionAmount);
        NumericInputParser.TryParseNonNegativeAmount(ReadPensionTransferText(), out var pensionTransferAmount);

        var definition = runtimeState.Definition;
        var openingBalance = definition.PdsAccount != null
            ? Math.Max(0d, definition.PdsAccount.Balance)
            : 0d;
        var startingParticipationPeriodNumber = definition.PdsAccount != null
                                                && definition.PdsAccount.ActivationPeriodNumber > 0
                                                && runtimeState.PeriodNumber >= definition.PdsAccount.ActivationPeriodNumber
            ? runtimeState.PeriodNumber - definition.PdsAccount.ActivationPeriodNumber + 1
            : 1;
        var firstPeriodBonus = ConsumerCreditMath.CalculatePdsContributionBonus(
            Math.Max(0d, contributionAmount),
            startingParticipationPeriodNumber);
        var projectedBalance = ConsumerCreditMath.CalculatePdsProjectedBalance(
            openingBalance,
            contributionAmount,
            pensionTransferAmount,
            definition.EconomyContext != null ? definition.EconomyContext.DepositRate : null,
            startingParticipationPeriodNumber,
            ConsumerCreditMath.PdsProjectionPeriods);

        _bonusLabel.text = $"Софинансирование за период: {EcuFormatter.FormatAmount(firstPeriodBonus)}";
        _projectionLabel.text =
            $"Через {ConsumerCreditMath.PdsProjectionPeriods} периодов: {EcuFormatter.FormatAmount(projectedBalance)}";
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        var card = RuntimeUiFactory.CreateCard("PdsCalculatorCard", transform, new Vector2(700f, 520f), false);
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(28, 28, 26, 24),
            12f);

        _titleLabel = RuntimeUiFactory.CreateTitle(content, "Калькулятор ПДС", TextAnchor.MiddleLeft);
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

        _availableIncomeLabel = RuntimeUiFactory.CreateBodyText(infoPanel, string.Empty);
        _availablePensionLabel = RuntimeUiFactory.CreateCaption(infoPanel, string.Empty);
        _rateLabel = RuntimeUiFactory.CreateCaption(infoPanel, string.Empty);

        RuntimeUiFactory.CreateCaption(content, "Взнос в ПДС за период");
        _contributionInput = RuntimeUiFactory.CreateInputField(content, "Введите сумму");
        NumericInputParser.Configure(_contributionInput);

        RuntimeUiFactory.CreateCaption(content, "Разовый перевод пенсионных накоплений");
        _pensionTransferInput = RuntimeUiFactory.CreateInputField(content, "Введите сумму");
        NumericInputParser.Configure(_pensionTransferInput);

        _bonusLabel = RuntimeUiFactory.CreateBodyText(content, "Софинансирование за период: -");
        _projectionLabel = RuntimeUiFactory.CreateBodyText(
            content,
            $"Через {ConsumerCreditMath.PdsProjectionPeriods} периодов: -");
        RuntimeUiFactory.CreateCaption(
            content,
            "Расчёт предполагает одинаковый взнос в каждом периоде участия и сохраняет текущую ставку доходности.");
        _messageLabel = RuntimeUiFactory.CreateErrorText(content);

        RuntimeUiFactory.AddFlexibleSpacer(content);
        var actions = RuntimeUiFactory.CreateRow("Actions", content, 12f, TextAnchor.MiddleCenter);
        _cancelButton = RuntimeUiFactory.CreateSecondaryButton(actions, "Отмена", 46f);
        _confirmButton = RuntimeUiFactory.CreatePrimaryButton(actions, "Вступить в ПДС", 46f);
    }

    private string ReadContributionText()
    {
        return _contributionInput != null
            ? _contributionInput.text ?? string.Empty
            : string.Empty;
    }

    private string ReadPensionTransferText()
    {
        return _pensionTransferInput != null
            ? _pensionTransferInput.text ?? string.Empty
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
