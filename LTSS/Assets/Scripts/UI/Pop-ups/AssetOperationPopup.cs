using System;
using Game.Core.Application.Periods;
using Game.Domain.GameFlow;
using UnityEngine;
using UnityEngine.UI;

[PopupDefinition(Enums.PopupType.AssetOperation)]
public sealed class AssetOperationPopup : Popup
{
    private Text _titleLabel;
    private Text _subtitleLabel;
    private Text _sourceLabel;
    private Text _limitLabel;
    private Text _messageLabel;
    private InputField _amountInput;
    private Button _sourceButton;
    private Button _confirmButton;
    private Button _cancelButton;
    private bool _isBuilt;

    public override Enums.PopupType PopupType => Enums.PopupType.AssetOperation;

    protected override void OnInitialize()
    {
        EnsureBuilt();
        BindButtons();

        if (Context.PeriodGameplay != null)
        {
            Context.PeriodGameplay.Changed += ApplyRuntime;
            ApplyRuntime(Context.PeriodGameplay.Current);
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
        BindButton(_cancelButton, () => Context.Popups?.Pop("asset_popup_cancel"));
        BindButton(_confirmButton, () => Context.PeriodGameplay?.SubmitAssetDialog(_amountInput != null ? _amountInput.text : string.Empty));
        BindButton(_sourceButton, () => Context.PeriodGameplay?.CycleAssetDialogSource());
    }

    private void ApplyRuntime(PeriodRuntimeState runtimeState)
    {
        if (!_isBuilt || runtimeState == null)
        {
            return;
        }

        var dialog = runtimeState.AssetDialog;

        if (dialog == null || !dialog.IsOpen)
        {
            return;
        }

        _titleLabel.text = dialog.Title;
        _subtitleLabel.text = dialog.Subtitle;
        _sourceLabel.text = PeriodContractMapper.ToFundsSourceLabel(dialog.SelectedSource);
        _sourceButton.gameObject.SetActive(dialog.AllowedSources != null && dialog.AllowedSources.Count > 1);
        _limitLabel.text = $"Доступно: {dialog.MaxAmount.ToString("0.##")} ₽";
        _messageLabel.text = runtimeState.StatusMessage;
        _messageLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(_messageLabel.text));

        if (_amountInput != null && string.IsNullOrWhiteSpace(_amountInput.text) && dialog.SuggestedAmount > 0d)
        {
            _amountInput.text = dialog.SuggestedAmount.ToString("0.##");
        }

        if (_confirmButton != null)
        {
            _confirmButton.interactable = dialog.MaxAmount > 0d;
        }
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        var card = RuntimeUiFactory.CreateCard("AssetOperationCard", transform, new Vector2(560f, 360f), false);
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(28, 28, 26, 24),
            12f);

        _titleLabel = RuntimeUiFactory.CreateTitle(content, "Операция с активом", TextAnchor.MiddleLeft);
        _subtitleLabel = RuntimeUiFactory.CreateCaption(content, string.Empty);
        RuntimeUiFactory.AddSpacer(content, 4f);

        var sourcePanel = RuntimeUiFactory.CreateSurface("SourcePanel", content, RuntimeUiFactory.PrimarySoftColor);
        var sourceLayout = sourcePanel.gameObject.AddComponent<HorizontalLayoutGroup>();
        sourceLayout.padding = new RectOffset(14, 14, 12, 12);
        sourceLayout.spacing = 12f;
        sourceLayout.childAlignment = TextAnchor.MiddleLeft;
        sourceLayout.childControlWidth = true;
        sourceLayout.childControlHeight = true;
        sourceLayout.childForceExpandWidth = false;
        sourceLayout.childForceExpandHeight = false;
        _sourceLabel = RuntimeUiFactory.CreateBodyText(sourcePanel, string.Empty);
        RuntimeUiFactory.AddFlexibleSpacer(sourcePanel);
        _sourceButton = RuntimeUiFactory.CreateSecondaryButton(sourcePanel, "Сменить", 42f);

        _amountInput = RuntimeUiFactory.CreateInputField(content, "Сумма");
        _amountInput.contentType = InputField.ContentType.DecimalNumber;
        _limitLabel = RuntimeUiFactory.CreateCaption(content, string.Empty);
        _messageLabel = RuntimeUiFactory.CreateErrorText(content);

        RuntimeUiFactory.AddFlexibleSpacer(content);
        var actions = RuntimeUiFactory.CreateRow("Actions", content, 12f, TextAnchor.MiddleCenter);
        _cancelButton = RuntimeUiFactory.CreateSecondaryButton(actions, "Отмена", 46f);
        _confirmButton = RuntimeUiFactory.CreatePrimaryButton(actions, "Подтвердить", 46f);
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
