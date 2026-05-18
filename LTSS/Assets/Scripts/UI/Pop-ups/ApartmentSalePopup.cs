using System;
using Game.Core.Application.Periods;
using Game.Domain.GameFlow;
using UnityEngine;
using UnityEngine.UI;

[PopupDefinition(Enums.PopupType.ApartmentSale)]
public sealed class ApartmentSalePopup : Popup
{
    private Text _titleLabel;
    private Text _subtitleLabel;
    private Text _salePriceLabel;
    private Text _rentLabel;
    private Text _messageLabel;
    private Button _cancelButton;
    private Button _confirmButton;
    private bool _isBuilt;

    public override Enums.PopupType PopupType => Enums.PopupType.ApartmentSale;

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
        BindButton(_cancelButton, () => Context.Popups?.Pop("apartment_sale_cancel"));
        BindButton(_confirmButton, HandleConfirm);
    }

    private void HandleConfirm()
    {
        if (Context.PeriodGameplay == null)
        {
            SetMessage("Сервис периода недоступен.");
            return;
        }

        if (Context.PeriodGameplay.TrySellApartment(out var errorMessage))
        {
            Context.Popups?.Pop("apartment_sale_confirm");
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
        var rentAmount = 20d * Math.Max(
            0.0001d,
            definition.EconomyContext != null
                ? definition.EconomyContext.ExpenseInflationMultiplier
                : 1d);
        var salePrice = ConsumerCreditMath.CalculateResidenceCurrentValue(
            definition.ResidenceOwnership,
            definition.EconomyContext);

        _titleLabel.text = "Продать квартиру?";
        _subtitleLabel.text =
            "После продажи исчезнет бонус владения жильём: регулярные +20 УЖЭ за период будут потеряны.";
        _salePriceLabel.text = $"Вы получите в наличные: {EcuFormatter.FormatAmount(salePrice)}";
        _rentLabel.text =
            $"Обязательная аренда вернётся в расходы: {EcuFormatter.FormatAmount(rentAmount)} за период.";

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

        var card = RuntimeUiFactory.CreateCard("ApartmentSaleCard", transform, new Vector2(700f, 420f), false);
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(28, 28, 26, 24),
            12f);

        _titleLabel = RuntimeUiFactory.CreateTitle(content, "Продать квартиру?", TextAnchor.MiddleLeft);
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

        _salePriceLabel = RuntimeUiFactory.CreateBodyText(infoPanel, string.Empty);
        _rentLabel = RuntimeUiFactory.CreateBodyText(infoPanel, string.Empty);
        RuntimeUiFactory.CreateCaption(
            content,
            "Позже вы сможете снова купить квартиру напрямую или оформить ипотеку.");
        _messageLabel = RuntimeUiFactory.CreateErrorText(content);

        RuntimeUiFactory.AddFlexibleSpacer(content);
        var actions = RuntimeUiFactory.CreateRow("Actions", content, 12f, TextAnchor.MiddleCenter);
        _cancelButton = RuntimeUiFactory.CreateSecondaryButton(actions, "Нет", 46f);
        _confirmButton = RuntimeUiFactory.CreatePrimaryButton(actions, "Да", 46f);
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
