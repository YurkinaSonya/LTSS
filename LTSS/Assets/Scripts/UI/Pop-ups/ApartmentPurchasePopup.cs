using System;
using Game.Core.Application.Periods;
using Game.Domain.GameFlow;
using UnityEngine;
using UnityEngine.UI;

[PopupDefinition(Enums.PopupType.ApartmentPurchase)]
public sealed class ApartmentPurchasePopup : Popup
{
    private Text _titleLabel;
    private Text _subtitleLabel;
    private Text _descriptionLabel;
    private Text _priceLabel;
    private Text _messageLabel;
    private Button _closeButton;
    private Button _buyButton;
    private Button _mortgageButton;
    private bool _isBuilt;

    public override Enums.PopupType PopupType => Enums.PopupType.ApartmentPurchase;

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
        BindButton(_closeButton, () => Context.Popups?.Pop("apartment_purchase_close"));
        BindButton(_buyButton, HandleBuy);
        BindButton(_mortgageButton, HandleMortgage);
    }

    private void HandleBuy()
    {
        if (Context.PeriodGameplay == null)
        {
            SetMessage("Сервис периода недоступен.");
            return;
        }

        if (Context.PeriodGameplay.TryBuyApartment(out var errorMessage))
        {
            Context.Popups?.Pop("apartment_purchase_confirm");
            return;
        }

        SetMessage(errorMessage);
    }

    private void HandleMortgage()
    {
        Context.Popups?.Pop("apartment_purchase_to_mortgage");
        Context.PeriodGameplay?.OpenMortgageDialog();
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

        var apartmentCost = ConsumerCreditMath.CalculateApartmentCost(latestRuntime.Definition);

        _titleLabel.text = "Купить квартиру";
        _subtitleLabel.text = "Собственное жильё убирает аренду и стабильно добавляет 20 УЖЭ.";
        _descriptionLabel.text =
            "Квартира становится вашим активом, может быть продана позднее и переоценивается вместе с инфляцией периода.";
        _priceLabel.text = $"Полная стоимость покупки: {EcuFormatter.FormatAmount(apartmentCost)}";

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

        var card = RuntimeUiFactory.CreateCard("ApartmentPurchaseCard", transform, new Vector2(700f, 420f), false);
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(28, 28, 26, 24),
            12f);

        _titleLabel = RuntimeUiFactory.CreateTitle(content, "Купить квартиру", TextAnchor.MiddleLeft);
        _subtitleLabel = RuntimeUiFactory.CreateCaption(content, string.Empty);
        _descriptionLabel = RuntimeUiFactory.CreateBodyText(content, string.Empty);

        var infoPanel = RuntimeUiFactory.CreateSurface("InfoPanel", content, RuntimeUiFactory.PrimarySoftColor);
        var infoLayout = infoPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        infoLayout.padding = new RectOffset(14, 14, 12, 12);
        infoLayout.spacing = 6f;
        infoLayout.childAlignment = TextAnchor.UpperLeft;
        infoLayout.childControlWidth = true;
        infoLayout.childControlHeight = true;
        infoLayout.childForceExpandWidth = true;
        infoLayout.childForceExpandHeight = false;

        _priceLabel = RuntimeUiFactory.CreateBodyText(infoPanel, string.Empty);
        _messageLabel = RuntimeUiFactory.CreateErrorText(content);

        RuntimeUiFactory.AddFlexibleSpacer(content);
        var actions = RuntimeUiFactory.CreateRow("Actions", content, 12f, TextAnchor.MiddleCenter);
        _closeButton = RuntimeUiFactory.CreateSecondaryButton(actions, "Закрыть", 46f);
        _buyButton = RuntimeUiFactory.CreatePrimaryButton(actions, "Купить", 46f);
        _mortgageButton = RuntimeUiFactory.CreateSecondaryButton(actions, "К ипотеке", 46f);
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
