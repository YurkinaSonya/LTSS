using System;
using System.Globalization;
using Game.Core.Application.Periods;
using Game.Domain.GameFlow;
using UnityEngine;
using UnityEngine.UI;

[PopupDefinition(Enums.PopupType.Mortgage)]
public sealed class MortgagePopup : Popup
{
    private Text _titleLabel;
    private Text _subtitleLabel;
    private Text _costLabel;
    private Text _downPaymentLabel;
    private Text _principalLabel;
    private Text _rateLabel;
    private Text _termLabel;
    private Text _paymentLabel;
    private Text _messageLabel;
    private Button _confirmButton;
    private Button _cancelButton;
    private bool _isBuilt;

    public override Enums.PopupType PopupType => Enums.PopupType.Mortgage;

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
        BindButton(_cancelButton, () => Context.Popups?.Pop("mortgage_cancel"));
        BindButton(_confirmButton, HandleConfirm);
    }

    private void HandleConfirm()
    {
        if (Context.PeriodGameplay == null)
        {
            SetMessage("Сервис периода недоступен.");
            return;
        }

        if (Context.PeriodGameplay.TrySubmitMortgage(out var errorMessage))
        {
            Context.Popups?.Pop("mortgage_confirm");
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
        var ratePercent = definition.EconomyContext != null && definition.EconomyContext.MortgageRate.HasValue
            ? definition.EconomyContext.MortgageRate.Value
            : 0d;
        var apartmentCost = ConsumerCreditMath.CalculateApartmentCost(definition);
        var downPayment = ConsumerCreditMath.CalculateMortgageDownPayment(definition);
        var principal = ConsumerCreditMath.CalculateMortgagePrincipal(definition);
        var payment = ConsumerCreditMath.CalculateAnnuityPayment(
            principal,
            ratePercent,
            ConsumerCreditMath.MortgageTermPeriods);

        _titleLabel.text = "Ипотека";
        _subtitleLabel.text = "Фиксированная сделка покупки жилья.";
        _costLabel.text = $"Стоимость жилья: {EcuFormatter.FormatAmount(apartmentCost)}";
        _downPaymentLabel.text = $"Первоначальный взнос: {EcuFormatter.FormatAmount(downPayment)}";
        _principalLabel.text = $"Сумма ипотеки: {EcuFormatter.FormatAmount(principal)}";
        _rateLabel.text = $"Ставка периода: {ratePercent.ToString("0.##", CultureInfo.InvariantCulture)}%";
        _termLabel.text = $"Срок: {ConsumerCreditMath.MortgageTermPeriods} периодов";
        _paymentLabel.text = $"Платёж за период: {EcuFormatter.FormatAmount(payment)}";

        if (string.IsNullOrWhiteSpace(_messageLabel.text) && !string.IsNullOrWhiteSpace(latestRuntime.StatusMessage))
        {
            SetMessage(latestRuntime.StatusMessage);
        }
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        var card = RuntimeUiFactory.CreateCard("MortgageCard", transform, new Vector2(640f, 430f), false);
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(28, 28, 26, 24),
            12f);

        _titleLabel = RuntimeUiFactory.CreateTitle(content, "Ипотека", TextAnchor.MiddleLeft);
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

        _costLabel = RuntimeUiFactory.CreateBodyText(infoPanel, string.Empty);
        _downPaymentLabel = RuntimeUiFactory.CreateBodyText(infoPanel, string.Empty);
        _principalLabel = RuntimeUiFactory.CreateBodyText(infoPanel, string.Empty);
        _rateLabel = RuntimeUiFactory.CreateCaption(infoPanel, string.Empty);
        _termLabel = RuntimeUiFactory.CreateCaption(infoPanel, string.Empty);
        _paymentLabel = RuntimeUiFactory.CreateBodyText(infoPanel, string.Empty);
        _messageLabel = RuntimeUiFactory.CreateErrorText(content);

        RuntimeUiFactory.AddFlexibleSpacer(content);
        var actions = RuntimeUiFactory.CreateRow("Actions", content, 12f, TextAnchor.MiddleCenter);
        _cancelButton = RuntimeUiFactory.CreateSecondaryButton(actions, "Отмена", 46f);
        _confirmButton = RuntimeUiFactory.CreatePrimaryButton(actions, "Оформить", 46f);
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
