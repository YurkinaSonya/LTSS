using System;
using System.Collections.Generic;
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
    private Dropdown _sourceDropdown;
    private Button _confirmButton;
    private Button _cancelButton;
    private bool _isBuilt;

    public override Enums.PopupType PopupType => Enums.PopupType.AssetOperation;

    protected override void OnInitialize()
    {
        EnsureBuilt();
        BindButtons();
        Debug.Log("[AssetPopupDebug] OnInitialize completed. Buttons bound.");

        if (Context.PeriodGameplay != null)
        {
            Context.PeriodGameplay.Changed += ApplyRuntime;
            ApplyRuntime(Context.PeriodGameplay.Current);
        }
        else
        {
            SetMessage("Диалог актива временно недоступен.");
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
        BindButton(_cancelButton, HandleCancel);
        BindButton(_confirmButton, HandleConfirm);
    }

    private void HandleCancel()
    {
        Context.Popups?.Pop("asset_popup_cancel");
    }

    private void HandleConfirm()
    {
        Debug.Log($"[AssetPopupDebug] HandleConfirm invoked. RawAmount='{ReadAmountText()}'.");

        if (Context.PeriodGameplay == null)
        {
            Debug.Log("[AssetPopupDebug] HandleConfirm aborted. PeriodGameplay is null.");
            SetMessage("Сервис периода недоступен.");
            return;
        }

        var beforeRuntime = Context.PeriodGameplay.Current;
        var beforeOperationCount = beforeRuntime != null && beforeRuntime.AssetOperations != null
            ? beforeRuntime.AssetOperations.Count
            : 0;

        Debug.Log($"[AssetPopupDebug] Before submit. OperationCount={beforeOperationCount}, DialogOpen={(beforeRuntime != null && beforeRuntime.AssetDialog != null && beforeRuntime.AssetDialog.IsOpen)}.");
        Context.PeriodGameplay.SubmitAssetDialog(ReadAmountText());

        var afterRuntime = Context.PeriodGameplay.Current;
        Debug.Log($"[AssetPopupDebug] After submit. OperationCount={(afterRuntime != null && afterRuntime.AssetOperations != null ? afterRuntime.AssetOperations.Count : 0)}, DialogOpen={(afterRuntime != null && afterRuntime.AssetDialog != null && afterRuntime.AssetDialog.IsOpen)}, Status='{(afterRuntime != null ? afterRuntime.StatusMessage : string.Empty)}'.");
        ApplyRuntime(afterRuntime);

        var wasApplied = afterRuntime != null
                         && ((afterRuntime.AssetOperations != null && afterRuntime.AssetOperations.Count > beforeOperationCount)
                             || !afterRuntime.AssetDialog.IsOpen);

        if (wasApplied)
        {
            Debug.Log("[AssetPopupDebug] Operation applied. Closing popup.");
            ClosePopupIfStillOpen();
        }
        else
        {
            Debug.Log("[AssetPopupDebug] Operation was not applied.");
        }
    }

    private void HandleChangeSource(FundsSourceType source)
    {
        if (Context.PeriodGameplay == null)
        {
            SetMessage("Невозможно переключить источник.");
            return;
        }

        Context.PeriodGameplay.SetAssetDialogSource(source);
        ApplyRuntime(Context.PeriodGameplay.Current);
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
        _sourceLabel.text = "Источник средств";

        if (_sourceDropdown != null)
        {
            var allowedSources = dialog.AllowedSources ?? Array.Empty<FundsSourceType>();
            var selectedIndex = FindSourceIndex(allowedSources, dialog.SelectedSource);
            var optionLabels = BuildSourceOptionLabels(allowedSources);

            _sourceDropdown.onValueChanged.RemoveAllListeners();
            RuntimeUiFactory.SetDropdownOptions(_sourceDropdown, optionLabels, selectedIndex);
            _sourceDropdown.interactable = allowedSources.Count > 1;

            if (allowedSources.Count > 0)
            {
                _sourceDropdown.onValueChanged.AddListener(index =>
                {
                    if (index >= 0 && index < allowedSources.Count)
                    {
                        HandleChangeSource(allowedSources[index]);
                    }
                });
            }
        }

        _limitLabel.text = $"Доступно: {EcuFormatter.FormatAmount(dialog.MaxAmount)}";
        SetMessage(runtimeState.StatusMessage);

        if (_amountInput != null && string.IsNullOrWhiteSpace(_amountInput.text) && dialog.SuggestedAmount > 0d)
        {
            _amountInput.text = dialog.SuggestedAmount.ToString("0.##");
            _amountInput.ForceLabelUpdate();
        }

        if (_confirmButton != null)
        {
            _confirmButton.interactable = true;
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
        _sourceDropdown = RuntimeUiFactory.CreateDropdown(sourcePanel, 42f);

        var sourceDropdownLayout = _sourceDropdown.gameObject.GetComponent<LayoutElement>();

        if (sourceDropdownLayout != null)
        {
            sourceDropdownLayout.preferredWidth = 210f;
        }

        _amountInput = RuntimeUiFactory.CreateInputField(content, "Сумма");
        NumericInputParser.Configure(_amountInput);
        _limitLabel = RuntimeUiFactory.CreateCaption(content, string.Empty);
        _messageLabel = RuntimeUiFactory.CreateErrorText(content);

        RuntimeUiFactory.AddFlexibleSpacer(content);
        var actions = RuntimeUiFactory.CreateRow("Actions", content, 12f, TextAnchor.MiddleCenter);
        _cancelButton = RuntimeUiFactory.CreateSecondaryButton(actions, "Отмена", 46f);
        _confirmButton = RuntimeUiFactory.CreatePrimaryButton(actions, "Подтвердить", 46f);
    }

    private string ReadAmountText()
    {
        if (_amountInput == null)
        {
            return string.Empty;
        }

        var text = _amountInput.text;

        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        if (_amountInput.textComponent != null && !string.IsNullOrWhiteSpace(_amountInput.textComponent.text))
        {
            return _amountInput.textComponent.text;
        }

        return string.Empty;
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

    private void ClosePopupIfStillOpen()
    {
        if (Context == null || Context.StateStore == null || Context.Popups == null)
        {
            return;
        }

        var popupStack = Context.StateStore.Current != null
            ? Context.StateStore.Current.PopupStack
            : null;

        if (popupStack == null || popupStack.Count == 0)
        {
            return;
        }

        var topPopup = popupStack[popupStack.Count - 1];

        if (topPopup != null && topPopup.Type == PopupType)
        {
            Context.Popups.Pop("asset_popup_confirm");
        }
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

    private static int FindSourceIndex(IReadOnlyList<FundsSourceType> sources, FundsSourceType selectedSource)
    {
        if (sources == null || sources.Count == 0)
        {
            return 0;
        }

        for (var index = 0; index < sources.Count; index++)
        {
            if (sources[index] == selectedSource)
            {
                return index;
            }
        }

        return 0;
    }

    private static List<string> BuildSourceOptionLabels(IReadOnlyList<FundsSourceType> sources)
    {
        var result = new List<string>();

        if (sources == null)
        {
            return result;
        }

        for (var index = 0; index < sources.Count; index++)
        {
            result.Add(PeriodContractMapper.ToFundsSourceSelectionLabel(sources[index]));
        }

        return result;
    }
}
