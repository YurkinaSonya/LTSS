using System;
using System.Globalization;
using Game.Core.Application.Periods;
using Game.Domain.GameFlow;
using UnityEngine;
using UnityEngine.UI;

[PopupDefinition(Enums.PopupType.ConsumerCredit)]
public sealed class ConsumerCreditPopup : Popup
{
    private Text _titleLabel;
    private Text _subtitleLabel;
    private Text _rateLabel;
    private Text _termLabel;
    private Text _paymentLabel;
    private Text _potentialLabel;
    private Text _messageLabel;
    private InputField _amountInput;
    private Button _confirmButton;
    private Button _cancelButton;
    private bool _isBuilt;

    public override Enums.PopupType PopupType => Enums.PopupType.ConsumerCredit;

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
            SetMessage("\u0421\u0435\u0440\u0432\u0438\u0441 \u043F\u0435\u0440\u0438\u043E\u0434\u0430 \u043D\u0435\u0434\u043E\u0441\u0442\u0443\u043F\u0435\u043D.");
        }

        if (_amountInput != null)
        {
            _amountInput.onValueChanged.AddListener(_ => RefreshQuote());
        }
    }

    public override void Close(Action callback = null)
    {
        if (Context.PeriodGameplay != null)
        {
            Context.PeriodGameplay.Changed -= ApplyRuntime;
        }

        if (_amountInput != null)
        {
            _amountInput.onValueChanged.RemoveAllListeners();
        }

        base.Close(callback);
    }

    private void BindButtons()
    {
        BindButton(_cancelButton, () => Context.Popups?.Pop("consumer_credit_cancel"));
        BindButton(_confirmButton, HandleConfirm);
    }

    private void HandleConfirm()
    {
        if (Context.PeriodGameplay == null)
        {
            SetMessage("\u0421\u0435\u0440\u0432\u0438\u0441 \u043F\u0435\u0440\u0438\u043E\u0434\u0430 \u043D\u0435\u0434\u043E\u0441\u0442\u0443\u043F\u0435\u043D.");
            return;
        }

        if (Context.PeriodGameplay.TrySubmitConsumerCredit(ReadAmountText(), out var errorMessage))
        {
            Context.Popups?.Pop("consumer_credit_confirm");
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
        var ratePercent = definition.EconomyContext != null && definition.EconomyContext.CreditRate.HasValue
            ? definition.EconomyContext.CreditRate.Value
            : 0d;
        var potential = ConsumerCreditMath.CalculateCreditPotential(definition);

        _titleLabel.text = "\u041F\u043E\u0442\u0440\u0435\u0431\u0438\u0442\u0435\u043B\u044C\u0441\u043A\u0438\u0439 \u043A\u0440\u0435\u0434\u0438\u0442";
        _subtitleLabel.text = "\u0421\u0440\u043E\u043A \u043A\u0440\u0435\u0434\u0438\u0442\u0430 \u0444\u0438\u043A\u0441\u0438\u0440\u043E\u0432\u0430\u043D: 5 \u043F\u0435\u0440\u0438\u043E\u0434\u043E\u0432.";
        _rateLabel.text = $"\u0421\u0442\u0430\u0432\u043A\u0430 \u043F\u0435\u0440\u0438\u043E\u0434\u0430: {ratePercent.ToString("0.##", CultureInfo.InvariantCulture)}%";
        _termLabel.text = $"\u0421\u0440\u043E\u043A: {ConsumerCreditMath.DefaultTermPeriods} \u043F\u0435\u0440\u0438\u043E\u0434\u043E\u0432";
        _potentialLabel.text = $"\u0414\u043E\u0441\u0442\u0443\u043F\u043D\u044B\u0439 \u043B\u0438\u043C\u0438\u0442: {EcuFormatter.FormatAmount(potential)}";

        if (string.IsNullOrWhiteSpace(_messageLabel.text) && !string.IsNullOrWhiteSpace(latestRuntime.StatusMessage))
        {
            SetMessage(latestRuntime.StatusMessage);
        }

        RefreshQuote();
    }

    private void RefreshQuote()
    {
        var runtimeState = Context != null && Context.PeriodGameplay != null
            ? Context.PeriodGameplay.Current
            : PeriodRuntimeState.Empty;

        if (runtimeState == null || !runtimeState.HasDefinition)
        {
            _paymentLabel.text = "\u041F\u043B\u0430\u0442\u0451\u0436 \u0437\u0430 \u043F\u0435\u0440\u0438\u043E\u0434: -";
            return;
        }

        var definition = runtimeState.Definition;
        var ratePercent = definition.EconomyContext != null
            ? definition.EconomyContext.CreditRate
            : null;
        var potential = ConsumerCreditMath.CalculateCreditPotential(definition);

        if (!NumericInputParser.TryParseNonNegativeAmount(ReadAmountText(), out var principal) || principal <= 0d)
        {
            _paymentLabel.text = "\u041F\u043B\u0430\u0442\u0451\u0436 \u0437\u0430 \u043F\u0435\u0440\u0438\u043E\u0434: -";
            _confirmButton.interactable = true;
            return;
        }

        var payment = ConsumerCreditMath.CalculateAnnuityPayment(principal, ratePercent);
        _paymentLabel.text = $"\u041F\u043B\u0430\u0442\u0451\u0436 \u0437\u0430 \u043F\u0435\u0440\u0438\u043E\u0434: {EcuFormatter.FormatAmount(payment)}";

        if (principal > potential + 0.01d)
        {
            SetMessage($"\u0414\u043E\u0441\u0442\u0443\u043F\u043D\u043E \u0442\u043E\u043B\u044C\u043A\u043E {EcuFormatter.FormatAmount(potential)} \u043A\u0440\u0435\u0434\u0438\u0442\u043D\u043E\u0433\u043E \u043B\u0438\u043C\u0438\u0442\u0430.");
        }
        else if (!string.IsNullOrWhiteSpace(_messageLabel.text)
                 && (_messageLabel.text.Contains("\u043F\u043E\u0442\u0435\u043D\u0446\u0438\u0430\u043B")
                     || _messageLabel.text.Contains("\u043B\u0438\u043C\u0438\u0442")))
        {
            SetMessage(string.Empty);
        }

        _confirmButton.interactable = true;
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        var card = RuntimeUiFactory.CreateCard("ConsumerCreditCard", transform, new Vector2(620f, 420f), false);
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(28, 28, 26, 24),
            12f);

        _titleLabel = RuntimeUiFactory.CreateTitle(
            content,
            "\u041F\u043E\u0442\u0440\u0435\u0431\u0438\u0442\u0435\u043B\u044C\u0441\u043A\u0438\u0439 \u043A\u0440\u0435\u0434\u0438\u0442",
            TextAnchor.MiddleLeft);
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

        _rateLabel = RuntimeUiFactory.CreateBodyText(infoPanel, string.Empty);
        _termLabel = RuntimeUiFactory.CreateCaption(infoPanel, string.Empty);
        _potentialLabel = RuntimeUiFactory.CreateCaption(infoPanel, string.Empty);

        RuntimeUiFactory.CreateCaption(content, "\u0421\u0443\u043C\u043C\u0430 \u043A\u0440\u0435\u0434\u0438\u0442\u0430");
        _amountInput = RuntimeUiFactory.CreateInputField(content, "\u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u0441\u0443\u043C\u043C\u0443");
        NumericInputParser.Configure(_amountInput);
        _paymentLabel = RuntimeUiFactory.CreateBodyText(content, "\u041F\u043B\u0430\u0442\u0451\u0436 \u0437\u0430 \u043F\u0435\u0440\u0438\u043E\u0434: -");
        _messageLabel = RuntimeUiFactory.CreateErrorText(content);

        RuntimeUiFactory.AddFlexibleSpacer(content);
        var actions = RuntimeUiFactory.CreateRow("Actions", content, 12f, TextAnchor.MiddleCenter);
        _cancelButton = RuntimeUiFactory.CreateSecondaryButton(actions, "\u041E\u0442\u043C\u0435\u043D\u0430", 46f);
        _confirmButton = RuntimeUiFactory.CreatePrimaryButton(actions, "\u041E\u0444\u043E\u0440\u043C\u0438\u0442\u044C", 46f);
    }

    private string ReadAmountText()
    {
        if (_amountInput == null)
        {
            return string.Empty;
        }

        return _amountInput.text ?? string.Empty;
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
