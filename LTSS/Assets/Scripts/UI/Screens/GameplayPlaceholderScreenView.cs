using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core.Application.Periods;
using Game.Core.Application.UI;
using Game.Domain.GameFlow;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameplayPlaceholderScreenView : ScreenView
{
    private sealed class ExpenseRowWidgets
    {
        public string ExpenseId;
        public Text TitleLabel;
        public Text MetaLabel;
        public GameObject RequiredBadge;
        public Text RequiredBadgeLabel;
        public InputField AmountInput;
        public Button ApplyRequiredAmountButton;
        public Dropdown SourceDropdown;
    }

    private sealed class AssetCardWidgets
    {
        public string AssetId;
        public Text TitleLabel;
        public Text ValueLabel;
        public Text CaptionLabel;
        public Button DepositButton;
        public Button WithdrawButton;
    }

    private sealed class InfoRowWidgets
    {
        public string InfoId;
        public Text Label;
        public Text Value;
    }

    private Text _periodTitleLabel;
    private Text _periodSubtitleLabel;
    private Text _phaseLabel;
    private Text _ujeValueLabel;
    private Text _ujeDeltaLabel;
    private Text _incomeValueLabel;
    private Text _remainingValueLabel;
    private Text _validationLabel;
    private Text _statusTextLabel;
    private Text _emptyStateLabel;
    private Text _expenseHintLabel;
    private Button _backButton;
    private Button _completeButton;
    private RectTransform _actionButtonsColumn;
    private RectTransform _consumerCreditRow;
    private RectTransform _housingActionRow;
    private RectTransform _pdsActionRow;
    private Button _consumerCreditButton;
    private Button _apartmentButton;
    private Button _mortgageButton;
    private Button _pdsButton;
    private RectTransform _expenseContent;
    private RectTransform _assetContent;
    private RectTransform _infoContent;
    private RectTransform _mainLayout;
    private RectTransform _footerRow;
    private ScrollRect _expenseScrollRect;
    private Image _expenseTopFade;
    private Image _expenseBottomFade;
    private readonly Dictionary<string, ExpenseRowWidgets> _expenseRows = new Dictionary<string, ExpenseRowWidgets>();
    private readonly Dictionary<string, AssetCardWidgets> _assetCards = new Dictionary<string, AssetCardWidgets>();
    private readonly Dictionary<string, InfoRowWidgets> _infoRows = new Dictionary<string, InfoRowWidgets>();
    private bool _hasDismissedExpenseScrollHint;
    private bool _isBuilt;
    private static readonly Color UjePositiveColor = new Color32(84, 156, 110, 255);

    public override ScreenController Construct(UIContext context)
    {
        EnsureBuilt();
        return new GameplayPlaceholderScreenController(this, context);
    }

    public void Render(
        PeriodRuntimeState runtimeState,
        Action onBack,
        Action onComplete,
        Action<string, string> onExpenseAmountChanged,
        Action<string> onApplyRequiredExpenseAmount,
        Action<string, FundsSourceType> onExpenseSourceChanged,
        Action<string, AssetOperationKind> onAssetAction,
        Action onConsumerCreditAction,
        Action onApartmentPurchaseAction,
        Action onMortgageAction,
        Action onPdsAction)
    {
        EnsureBuilt();
        BindButton(_backButton, onBack);
        BindButton(_completeButton, onComplete);
        BindButton(_consumerCreditButton, onConsumerCreditAction);
        BindButton(_apartmentButton, onApartmentPurchaseAction);
        BindButton(_mortgageButton, onMortgageAction);
        BindButton(_pdsButton, onPdsAction);

        if (runtimeState == null || !runtimeState.HasDefinition)
        {
            var fallbackMessage = "Период пока недоступен. Проверьте загрузку данных сессии.";

            if (runtimeState != null)
            {
                if (!string.IsNullOrWhiteSpace(runtimeState.LastError))
                {
                    fallbackMessage = runtimeState.LastError;
                }
                else if (!string.IsNullOrWhiteSpace(runtimeState.StatusMessage))
                {
                    fallbackMessage = runtimeState.StatusMessage;
                }
            }

            SetEmptyState(fallbackMessage);
            return;
        }

        ShowMainState();
        ApplyHeader(runtimeState);
        ApplyMetrics(runtimeState);
        ApplyInfo(runtimeState);
        ApplyExpenses(runtimeState, onExpenseAmountChanged, onApplyRequiredExpenseAmount, onExpenseSourceChanged);
        ApplyActionButtons(runtimeState);
        ApplyAssets(runtimeState, onAssetAction);
        ApplyFooter(runtimeState);
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        var background = RuntimeUiFactory.CreateScreenBackground(transform);
        var shell = RuntimeUiFactory.CreateCard("GameplayShell", background, new Vector2(1500f, 900f));
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            shell,
            new RectOffset(24, 24, 24, 22),
            16f);

        BuildHeader(content);
        BuildMetrics(content);
        BuildMain(content);
        BuildFooter(content);

        _emptyStateLabel = RuntimeUiFactory.CreateBodyText(content, string.Empty, TextAnchor.MiddleCenter);
        _emptyStateLabel.color = RuntimeUiFactory.TextSecondaryColor;
        _emptyStateLabel.gameObject.SetActive(false);
    }

    private void BuildHeader(Transform parent)
    {
        var header = RuntimeUiFactory.CreateSurface("Header", parent, RuntimeUiFactory.SurfaceColor);
        AddLayoutElement(header.gameObject, preferredHeight: 96f);

        var layout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 18, 18);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var titleColumn = CreateVerticalGroup(header, "TitleColumn", 4f, TextAnchor.MiddleLeft);
        AddLayoutElement(titleColumn.gameObject, flexibleWidth: 1f);
        _periodTitleLabel = RuntimeUiFactory.CreateTitle(titleColumn, "Период", TextAnchor.MiddleLeft);
        _periodSubtitleLabel = RuntimeUiFactory.CreateCaption(titleColumn, string.Empty, TextAnchor.MiddleLeft);
        _periodSubtitleLabel.gameObject.SetActive(false);

        var controls = CreateRow(header, "Controls", 10f, TextAnchor.MiddleRight);
        AddLayoutElement(controls.gameObject, preferredWidth: 260f);
        _phaseLabel = RuntimeUiFactory.CreateCaption(controls, string.Empty, TextAnchor.MiddleCenter);
        AddLayoutElement(_phaseLabel.gameObject, preferredWidth: 120f);
        _backButton = RuntimeUiFactory.CreateSecondaryButton(controls, "Назад", 46f);
    }

    private void BuildMetrics(Transform parent)
    {
        var row = CreateRow(parent, "MetricsRow", 16f, TextAnchor.MiddleCenter);
        AddLayoutElement(row.gameObject, preferredHeight: 126f);

        _ujeValueLabel = CreateMetricCard(row, "УЖЭ", "0");
        _incomeValueLabel = CreateMetricCard(row, "Располагаемый доход, ECU", "0");
        _remainingValueLabel = CreateMetricCard(row, "Осталось к распределению, ECU", "0");
    }

    private void BuildMain(Transform parent)
    {
        _mainLayout = CreateRow(parent, "MainLayout", 16f, TextAnchor.UpperLeft);
        AddLayoutElement(_mainLayout.gameObject, flexibleHeight: 1f);

        var leftColumn = CreateVerticalGroup(_mainLayout, "LeftColumn", 16f, TextAnchor.UpperLeft);
        AddLayoutElement(leftColumn.gameObject, preferredWidth: 980f, flexibleWidth: 1.2f, flexibleHeight: 1f);

        var rightColumn = CreateVerticalGroup(_mainLayout, "RightColumn", 16f, TextAnchor.UpperLeft);
        AddLayoutElement(rightColumn.gameObject, preferredWidth: 420f, flexibleWidth: 0.8f, flexibleHeight: 1f);

        _expenseContent = CreateScrollableSection(leftColumn, "Расходы", out _expenseScrollRect);
        AddLayoutElement(_expenseContent.parent.gameObject, flexibleHeight: 1f);

        _actionButtonsColumn = CreateVerticalGroup(leftColumn, "ActionButtonsColumn", 12f, TextAnchor.UpperLeft);
        AddLayoutElement(_actionButtonsColumn.gameObject, preferredHeight: 162f);
        _consumerCreditRow = CreateRow(_actionButtonsColumn, "ConsumerCreditRow", 0f, TextAnchor.MiddleCenter);
        AddLayoutElement(_consumerCreditRow.gameObject, preferredHeight: 46f);
        _consumerCreditButton = RuntimeUiFactory.CreateSecondaryButton(_consumerCreditRow, "Потребительский кредит", 46f);
        AddLayoutElement(_consumerCreditButton.gameObject, flexibleWidth: 1f, preferredHeight: 46f);
        _housingActionRow = CreateRow(_actionButtonsColumn, "HousingActionRow", 12f, TextAnchor.MiddleCenter);
        AddLayoutElement(_housingActionRow.gameObject, preferredHeight: 46f);
        _apartmentButton = RuntimeUiFactory.CreateSecondaryButton(_housingActionRow, "Купить квартиру", 46f);
        AddLayoutElement(_apartmentButton.gameObject, flexibleWidth: 1f, preferredHeight: 46f);
        _mortgageButton = RuntimeUiFactory.CreateSecondaryButton(_housingActionRow, "Оформить ипотеку", 46f);
        AddLayoutElement(_mortgageButton.gameObject, flexibleWidth: 1f, preferredHeight: 46f);

        _assetContent = CreateSection(leftColumn, "Активы");
        _infoContent = CreateSection(rightColumn, "Параметры периода");
        _pdsActionRow = CreateRow(_actionButtonsColumn, "PdsActionRow", 0f, TextAnchor.MiddleCenter);
        AddLayoutElement(_pdsActionRow.gameObject, preferredHeight: 46f);
        _pdsButton = RuntimeUiFactory.CreateSecondaryButton(_pdsActionRow, "ПДС", 46f);
        AddLayoutElement(_pdsButton.gameObject, flexibleWidth: 1f, preferredHeight: 46f);

        BuildStatusSection(rightColumn);
    }

    private void BuildStatusSection(Transform parent)
    {
        var section = RuntimeUiFactory.CreateSurface("StatusSection", parent, RuntimeUiFactory.SurfaceColor);
        AddLayoutElement(section.gameObject, flexibleHeight: 1f);

        var layout = section.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        RuntimeUiFactory.CreateBodyText(section, "Состояние периода");
        _statusTextLabel = RuntimeUiFactory.CreateCaption(section, string.Empty);
    }

    private void BuildFooter(Transform parent)
    {
        _footerRow = CreateRow(parent, "Footer", 16f, TextAnchor.MiddleCenter);
        AddLayoutElement(_footerRow.gameObject, preferredHeight: 64f);

        _validationLabel = RuntimeUiFactory.CreateCaption(_footerRow, string.Empty, TextAnchor.MiddleLeft);
        AddLayoutElement(_validationLabel.gameObject, flexibleWidth: 1f, preferredWidth: 900f);
        _completeButton = RuntimeUiFactory.CreatePrimaryButton(_footerRow, "Завершить период", 52f);
        AddLayoutElement(_completeButton.gameObject, preferredWidth: 280f);
    }

    private void ApplyHeader(PeriodRuntimeState runtimeState)
    {
        var meta = runtimeState.Definition.Meta;
        _periodTitleLabel.text = !string.IsNullOrWhiteSpace(meta.Title)
            ? meta.Title
            : $"Период {meta.PeriodNumber}";

        _periodSubtitleLabel.text = string.Empty;
        _periodSubtitleLabel.gameObject.SetActive(false);
        _phaseLabel.text = !string.IsNullOrWhiteSpace(meta.Phase)
            ? $"{meta.Phase} / {FormatFlow(runtimeState.FlowState)}"
            : FormatFlow(runtimeState.FlowState);
    }

    private void ApplyMetrics(PeriodRuntimeState runtimeState)
    {
        var summary = runtimeState.Summary ?? PeriodCalculationSummary.Empty;

        _ujeValueLabel.text = summary.AccumulatedUje.ToString("0.0", CultureInfo.InvariantCulture);
        ApplyUjeDelta(summary.ProjectedUjeDelta);
        _incomeValueLabel.text = FormatMoney(summary.CurrentIncomeEcu);
        _remainingValueLabel.text = FormatMoney(summary.RemainingToAllocate);
        _remainingValueLabel.color = summary.RemainingToAllocate < -0.01d
            ? RuntimeUiFactory.DangerColor
            : RuntimeUiFactory.TextPrimaryColor;
    }

    private void ApplyInfo(PeriodRuntimeState runtimeState)
    {
        RebuildInfoRows(runtimeState.Definition.InfoBlockValues);

        foreach (var infoValue in runtimeState.Definition.InfoBlockValues)
        {
            if (infoValue == null || !_infoRows.TryGetValue(infoValue.Id, out var widgets))
            {
                continue;
            }

            widgets.Label.text = infoValue.Label;
            widgets.Value.text = FormatInfoValue(infoValue);
            widgets.Value.color = infoValue.HasValue
                ? RuntimeUiFactory.TextPrimaryColor
                : RuntimeUiFactory.TextSecondaryColor;
        }
    }

    private void ApplyExpenses(
        PeriodRuntimeState runtimeState,
        Action<string, string> onExpenseAmountChanged,
        Action<string> onApplyRequiredExpenseAmount,
        Action<string, FundsSourceType> onExpenseSourceChanged)
    {
        var canEdit = runtimeState.FlowState != PeriodFlowState.PeriodCheckpointSubmitting
            && runtimeState.FlowState != PeriodFlowState.PeriodClosed
            && runtimeState.FlowState != PeriodFlowState.LoadingData
            && runtimeState.FlowState != PeriodFlowState.PeriodClosing;
        RebuildExpenseRows(runtimeState.Definition.ExpenseDefinitions);

        foreach (var expenseDefinition in runtimeState.Definition.ExpenseDefinitions)
        {
            if (expenseDefinition == null || !_expenseRows.TryGetValue(expenseDefinition.Id, out var widgets))
            {
                continue;
            }

            var expenseState = FindExpenseState(runtimeState, expenseDefinition.Id);
            widgets.TitleLabel.text = expenseDefinition.Title;
            widgets.MetaLabel.text = expenseDefinition.IsRequired
                ? "обязательная статья"
                : "необязательная статья";
            widgets.MetaLabel.color = expenseDefinition.IsRequired
                ? RuntimeUiFactory.PrimaryColor
                : RuntimeUiFactory.TextSecondaryColor;
            var creditContract = FindConsumerCredit(runtimeState, expenseDefinition.Id);

            if (creditContract != null)
            {
                widgets.MetaLabel.text =
                    $"ост. {creditContract.RemainingPeriods} пер. | долг {FormatMoney(creditContract.RemainingPrincipal)}";
                widgets.MetaLabel.color = RuntimeUiFactory.PrimaryColor;
            }

            if (string.Equals(expenseDefinition.Id, "education", StringComparison.Ordinal))
            {
                widgets.MetaLabel.text = BuildEducationMeta(runtimeState, expenseState);
                widgets.MetaLabel.color = RuntimeUiFactory.PrimaryColor;
            }

            if (widgets.RequiredBadge != null)
            {
                var showRequiredBadge = expenseDefinition.IsRequired && expenseDefinition.MinimumAmount > 0d;
                widgets.RequiredBadge.SetActive(showRequiredBadge);

                if (showRequiredBadge && widgets.RequiredBadgeLabel != null)
                {
                    widgets.RequiredBadgeLabel.text = string.Equals(expenseDefinition.Id, "housing_rent", StringComparison.Ordinal)
                        ? FormatMoney(expenseDefinition.MinimumAmount)
                        : $"мин. {FormatMoney(expenseDefinition.MinimumAmount)}";
                }
            }

            var amountText = expenseState != null && expenseState.Amount > 0d
                ? expenseState.Amount.ToString("0.##", CultureInfo.InvariantCulture)
                : string.Empty;

            widgets.AmountInput.onValueChanged.RemoveAllListeners();
            SyncInputFieldText(widgets.AmountInput, amountText);

            widgets.AmountInput.onValueChanged.AddListener(value => onExpenseAmountChanged?.Invoke(expenseDefinition.Id, value));
            widgets.AmountInput.interactable = canEdit && !IsCompletedEducationExpense(runtimeState, expenseDefinition);

            if (widgets.ApplyRequiredAmountButton != null)
            {
                var showQuickApply = IsFixedAmountExpense(expenseDefinition)
                    || string.Equals(expenseDefinition.Id, "holiday", StringComparison.Ordinal);
                widgets.ApplyRequiredAmountButton.gameObject.SetActive(showQuickApply);
                widgets.ApplyRequiredAmountButton.interactable = canEdit && showQuickApply;
                BindButton(
                    widgets.ApplyRequiredAmountButton,
                    showQuickApply ? (Action)(() => onApplyRequiredExpenseAmount?.Invoke(expenseDefinition.Id)) : null);
            }

            var allowedSources = expenseDefinition.AllowedSources ?? Array.Empty<FundsSourceType>();
            var selectedSource = expenseState != null
                ? expenseState.Source
                : FundsSourceType.CurrentIncome;
            var selectedIndex = FindSourceIndex(allowedSources, selectedSource);
            var optionLabels = BuildSourceOptionLabels(allowedSources);

            widgets.SourceDropdown.onValueChanged.RemoveAllListeners();
            RuntimeUiFactory.SetDropdownOptions(widgets.SourceDropdown, optionLabels, selectedIndex);
            widgets.SourceDropdown.interactable = canEdit
                                                 && allowedSources.Count > 1
                                                 && !IsCompletedEducationExpense(runtimeState, expenseDefinition);

            if (allowedSources.Count > 0)
            {
                widgets.SourceDropdown.onValueChanged.AddListener(index =>
                {
                    if (index >= 0 && index < allowedSources.Count)
                    {
                        onExpenseSourceChanged?.Invoke(expenseDefinition.Id, allowedSources[index]);
                    }
                });
            }
        }

        if (_expenseScrollRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_expenseContent);
            UpdateExpenseScrollUi(runtimeState);
        }
    }

    private void ApplyActionButtons(PeriodRuntimeState runtimeState)
    {
        if (_consumerCreditButton == null || _mortgageButton == null || _apartmentButton == null || _pdsButton == null)
        {
            return;
        }

        var canEdit = runtimeState.FlowState == PeriodFlowState.PeriodIntro
                      || runtimeState.FlowState == PeriodFlowState.PeriodActive
                      || runtimeState.FlowState == PeriodFlowState.PeriodValidation;
        var showConsumerCredit = runtimeState != null
                                 && runtimeState.HasDefinition
                                 && runtimeState.Definition.Meta != null
                                 && runtimeState.Definition.Meta.HasFeature("consumer_credit");
        var showMortgage = runtimeState != null
                           && runtimeState.HasDefinition
                           && runtimeState.Definition.Meta != null
                           && runtimeState.Definition.Meta.HasFeature("mortgage");
        var ownsApartment = runtimeState != null
                            && runtimeState.HasDefinition
                            && runtimeState.Definition.ResidenceOwnership != null;
        var canUsePds = runtimeState != null
                        && runtimeState.HasDefinition
                        && runtimeState.Definition.Meta != null
                        && runtimeState.Definition.Meta.HasFeature("pds");
        var canTakeMortgage = showMortgage && !ownsApartment && !HasActiveMortgage(runtimeState);

        if (_actionButtonsColumn != null)
        {
            _actionButtonsColumn.gameObject.SetActive(showConsumerCredit || showMortgage && !ownsApartment || canUsePds);
        }

        if (_consumerCreditRow != null)
        {
            _consumerCreditRow.gameObject.SetActive(showConsumerCredit);
        }

        if (_housingActionRow != null)
        {
            _housingActionRow.gameObject.SetActive(showMortgage && !ownsApartment);
        }

        if (_pdsActionRow != null)
        {
            _pdsActionRow.gameObject.SetActive(canUsePds);
        }

        _consumerCreditButton.gameObject.SetActive(showConsumerCredit);
        _apartmentButton.gameObject.SetActive(showMortgage && !ownsApartment);
        _mortgageButton.gameObject.SetActive(canTakeMortgage);
        _pdsButton.gameObject.SetActive(canUsePds);
        _consumerCreditButton.interactable = canEdit && showConsumerCredit;
        _apartmentButton.interactable = canEdit && showMortgage && !ownsApartment;
        _mortgageButton.interactable = canEdit && canTakeMortgage;
        _pdsButton.interactable = canEdit && canUsePds;
        RuntimeUiFactory.SetButtonText(
            _pdsButton,
            runtimeState != null
            && runtimeState.HasDefinition
            && runtimeState.Definition.PdsAccount == null
                ? "Вступить в ПДС"
                : "ПДС");
    }

    private void ApplyAssets(
        PeriodRuntimeState runtimeState,
        Action<string, AssetOperationKind> onAssetAction)
    {
        var canEdit = runtimeState.FlowState != PeriodFlowState.PeriodCheckpointSubmitting
            && runtimeState.FlowState != PeriodFlowState.PeriodClosed
            && runtimeState.FlowState != PeriodFlowState.LoadingData
            && runtimeState.FlowState != PeriodFlowState.PeriodClosing;
        RebuildAdaptiveAssetCards(runtimeState.Summary.AssetBalances);

        foreach (var asset in runtimeState.Summary.AssetBalances)
        {
            if (asset == null || !_assetCards.TryGetValue(asset.AssetId, out var widgets))
            {
                continue;
            }

            widgets.TitleLabel.text = asset.Title;
            widgets.ValueLabel.text = FormatMoney(asset.CurrentAmount);
            widgets.CaptionLabel.text = $"На начало периода: {FormatMoney(asset.InitialAmount)}";
            RuntimeUiFactory.SetButtonText(
                widgets.WithdrawButton,
                asset.AssetType == PeriodAssetType.Apartment ? "Продать" : "Снять");

            widgets.DepositButton.gameObject.SetActive(asset.AllowsDeposit);
            widgets.WithdrawButton.gameObject.SetActive(asset.AllowsWithdraw);
            widgets.DepositButton.interactable = canEdit && asset.AllowsDeposit;
            widgets.WithdrawButton.interactable = canEdit && asset.AllowsWithdraw;

            BindButton(widgets.DepositButton, () => onAssetAction?.Invoke(asset.AssetId, AssetOperationKind.Deposit));
            BindButton(widgets.WithdrawButton, () => onAssetAction?.Invoke(asset.AssetId, AssetOperationKind.Withdraw));
        }
    }

    private void ApplyFooter(PeriodRuntimeState runtimeState)
    {
        var summary = runtimeState.Summary ?? PeriodCalculationSummary.Empty;
        var canEdit = runtimeState.FlowState == PeriodFlowState.PeriodIntro
            || runtimeState.FlowState == PeriodFlowState.PeriodActive
            || runtimeState.FlowState == PeriodFlowState.PeriodValidation;

        _completeButton.interactable = canEdit && summary.CanComplete;

        if (runtimeState.FlowState == PeriodFlowState.PeriodCheckpointSubmitting)
        {
            RuntimeUiFactory.SetButtonText(_completeButton, "Отправка...");
        }
        else if (runtimeState.FlowState == PeriodFlowState.PeriodClosed)
        {
            RuntimeUiFactory.SetButtonText(_completeButton, "Период закрыт");
        }
        else
        {
            RuntimeUiFactory.SetButtonText(_completeButton, "Завершить период");
        }

        if (summary.CanComplete && string.IsNullOrWhiteSpace(runtimeState.StatusMessage))
        {
            _validationLabel.text = "Период можно завершить.";
            _validationLabel.color = RuntimeUiFactory.TextSecondaryColor;
        }
        else
        {
            _validationLabel.text = !string.IsNullOrWhiteSpace(runtimeState.StatusMessage)
                ? runtimeState.StatusMessage
                : FirstIssueMessage(summary);
            _validationLabel.color = summary.CanComplete
                ? RuntimeUiFactory.TextSecondaryColor
                : RuntimeUiFactory.DangerColor;
        }

        _statusTextLabel.text = BuildStatusText(runtimeState);
    }

    private void RebuildExpenseRows(IReadOnlyList<PeriodExpenseDefinition> definitions)
    {
        if (!NeedsRebuild(_expenseRows, definitions, definition => definition.Id))
        {
            return;
        }

        ClearChildren(_expenseContent);
        _expenseRows.Clear();

        foreach (var definition in definitions)
        {
            if (definition == null)
            {
                continue;
            }

            var row = RuntimeUiFactory.CreateSurface($"Expense_{definition.Id}", _expenseContent, RuntimeUiFactory.ElevatedSurfaceColor);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 16, 14, 14);
            rowLayout.spacing = 12f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            AddLayoutElement(row.gameObject, preferredHeight: 84f);

            var textColumn = CreateVerticalGroup(row, "TextColumn", 4f, TextAnchor.MiddleLeft);
            AddLayoutElement(textColumn.gameObject, flexibleWidth: 1f, preferredWidth: 360f);
            var title = RuntimeUiFactory.CreateBodyText(textColumn, string.Empty);
            RuntimeUiFactory.ApplyTextStyle(title, FontStyle.Bold);
            var metaRow = CreateRow(textColumn, "MetaRow", 8f, TextAnchor.MiddleLeft);
            var meta = RuntimeUiFactory.CreateCaption(metaRow, string.Empty);
            var requiredBadge = RuntimeUiFactory.CreateSurface("RequiredBadge", metaRow, RuntimeUiFactory.PrimarySoftColor, false);
            var requiredBadgeLayout = requiredBadge.gameObject.AddComponent<HorizontalLayoutGroup>();
            requiredBadgeLayout.padding = new RectOffset(8, 8, 1, 1);
            requiredBadgeLayout.spacing = 0f;
            requiredBadgeLayout.childAlignment = TextAnchor.MiddleCenter;
            requiredBadgeLayout.childControlWidth = false;
            requiredBadgeLayout.childControlHeight = true;
            requiredBadgeLayout.childForceExpandWidth = false;
            requiredBadgeLayout.childForceExpandHeight = false;
            AddLayoutElement(requiredBadge.gameObject, preferredHeight: 20f);
            var requiredBadgeFitter = requiredBadge.gameObject.AddComponent<ContentSizeFitter>();
            requiredBadgeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            requiredBadgeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            var requiredLabel = RuntimeUiFactory.CreateBodyText(requiredBadge, string.Empty, TextAnchor.MiddleCenter);
            requiredLabel.fontSize = 12;
            requiredLabel.color = RuntimeUiFactory.PrimaryColor;
            RuntimeUiFactory.ApplyTextStyle(requiredLabel, FontStyle.Bold);
            requiredBadge.gameObject.SetActive(false);

            var applyRequiredAmountButton = RuntimeUiFactory.CreateSecondaryButton(row, "=", 42f);
            AddLayoutElement(
                applyRequiredAmountButton.gameObject,
                minimumWidth: 40f,
                minimumHeight: 40f,
                preferredWidth: 40f,
                preferredHeight: 40f,
                flexibleWidth: 0f,
                flexibleHeight: 0f);

            var amountField = RuntimeUiFactory.CreateInputField(row, "0");
            NumericInputParser.Configure(amountField);
            AddLayoutElement(amountField.gameObject, minimumHeight: 25f, preferredWidth: 160f);

            var sourceDropdown = RuntimeUiFactory.CreateDropdown(row, 46f);
            AddLayoutElement(sourceDropdown.gameObject, preferredWidth: 180f);

            _expenseRows[definition.Id] = new ExpenseRowWidgets
            {
                ExpenseId = definition.Id,
                TitleLabel = title,
                MetaLabel = meta,
                RequiredBadge = requiredBadge.gameObject,
                RequiredBadgeLabel = requiredLabel,
                AmountInput = amountField,
                ApplyRequiredAmountButton = applyRequiredAmountButton,
                SourceDropdown = sourceDropdown
            };
        }
    }

    private void RebuildAssetCards(IReadOnlyList<PeriodAssetBalance> assets)
    {
        if (!NeedsRebuild(_assetCards, assets, asset => asset.AssetId))
        {
            return;
        }

        ClearChildren(_assetContent);
        _assetCards.Clear();

        var cardsRow = CreateRow(_assetContent, "AssetCards", 14f, TextAnchor.MiddleCenter);
        var cardsLayout = cardsRow.GetComponent<HorizontalLayoutGroup>();

        if (cardsLayout != null)
        {
            cardsLayout.childForceExpandWidth = true;
        }

        foreach (var asset in assets)
        {
            if (asset == null)
            {
                continue;
            }

            var card = RuntimeUiFactory.CreateSurface($"Asset_{asset.AssetId}", cardsRow, RuntimeUiFactory.ElevatedSurfaceColor);
            AddLayoutElement(card.gameObject, preferredWidth: 0f, preferredHeight: 166f, flexibleWidth: 1f);

            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = RuntimeUiFactory.CreateBodyText(card, string.Empty);
            RuntimeUiFactory.ApplyTextStyle(title, FontStyle.Bold);
            var value = RuntimeUiFactory.CreateValueText(card, "0", 28, TextAnchor.MiddleLeft);
            var caption = RuntimeUiFactory.CreateCaption(card, string.Empty);
            RuntimeUiFactory.AddFlexibleSpacer(card);
            var actions = CreateRow(card, "Actions", 10f, TextAnchor.MiddleCenter);
            var depositButton = RuntimeUiFactory.CreatePrimaryButton(actions, "Пополнить", 42f);
            var withdrawButton = RuntimeUiFactory.CreateSecondaryButton(actions, "Снять", 42f);

            _assetCards[asset.AssetId] = new AssetCardWidgets
            {
                AssetId = asset.AssetId,
                TitleLabel = title,
                ValueLabel = value,
                CaptionLabel = caption,
                DepositButton = depositButton,
                WithdrawButton = withdrawButton
            };
        }
    }

    private void RebuildInfoRows(IReadOnlyList<PeriodInfoBlockValue> infoValues)
    {
        ClearChildren(_infoContent);
        _infoRows.Clear();

        foreach (var infoValue in infoValues)
        {
            if (infoValue == null)
            {
                continue;
            }

            var row = CreateRow(_infoContent, $"Info_{infoValue.Id}", 12f, TextAnchor.MiddleLeft);
            var label = RuntimeUiFactory.CreateCaption(row, infoValue.Label);
            AddLayoutElement(label.gameObject, preferredWidth: 180f);
            var value = RuntimeUiFactory.CreateBodyText(row, string.Empty);

            _infoRows[infoValue.Id] = new InfoRowWidgets
            {
                InfoId = infoValue.Id,
                Label = label,
                Value = value
            };
        }
    }

    private void RebuildAdaptiveAssetCards(IReadOnlyList<PeriodAssetBalance> assets)
    {
        if (assets == null)
        {
            assets = Array.Empty<PeriodAssetBalance>();
        }

        ClearChildren(_assetContent);
        _assetCards.Clear();

        if (ShouldUseScrollableAssetsLayout(assets))
        {
            var primaryRow = CreateAssetsRow(_assetContent, "PrimaryAssetCards");

            for (var index = 0; index < assets.Count; index++)
            {
                var asset = assets[index];

                if (asset == null || !IsPrimaryAsset(asset))
                {
                    continue;
                }

                CreateAssetCard(primaryRow, asset);
            }

            var overflowArea = CreateRow(_assetContent, "OverflowAssetScrollArea", 8f, TextAnchor.UpperLeft);
            var overflowLayout = overflowArea.GetComponent<HorizontalLayoutGroup>();
            overflowLayout.childForceExpandWidth = false;
            overflowLayout.childForceExpandHeight = true;
            overflowLayout.childControlHeight = true;
            AddLayoutElement(overflowArea.gameObject, flexibleHeight: 1f, minimumHeight: 190f);

            var viewport = CreateRect("OverflowAssetViewport", overflowArea);
            AddLayoutElement(viewport.gameObject, flexibleWidth: 1f, flexibleHeight: 1f, minimumHeight: 190f);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.white;
            var viewportMask = viewport.gameObject.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            var scrollRect = overflowArea.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;
            var verticalScrollbar = CreateVerticalScrollbar(overflowArea);
            scrollRect.verticalScrollbar = verticalScrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            var overflowContent = RuntimeUiFactory.CreateContentRoot(
                "OverflowAssetContent",
                viewport,
                new RectOffset(0, 0, 0, 0),
                10f);
            overflowContent.anchorMin = new Vector2(0f, 1f);
            overflowContent.anchorMax = new Vector2(1f, 1f);
            overflowContent.pivot = new Vector2(0.5f, 1f);
            overflowContent.anchoredPosition = Vector2.zero;
            overflowContent.sizeDelta = new Vector2(0f, 0f);
            var fitter = overflowContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = overflowContent;

            var overflowRow = CreateAssetsRow(overflowContent, "OverflowAssetCards");

            for (var index = 0; index < assets.Count; index++)
            {
                var asset = assets[index];

                if (asset == null || IsPrimaryAsset(asset))
                {
                    continue;
                }

                CreateAssetCard(overflowRow, asset);
            }
        }
        else
        {
            var cardsRow = CreateAssetsRow(_assetContent, "AssetCards");

            for (var index = 0; index < assets.Count; index++)
            {
                var asset = assets[index];

                if (asset == null)
                {
                    continue;
                }

                CreateAssetCard(cardsRow, asset);
            }
        }
    }

    private RectTransform CreateAssetsRow(Transform parent, string name)
    {
        var row = CreateRow(parent, name, 14f, TextAnchor.MiddleCenter);
        var layout = row.GetComponent<HorizontalLayoutGroup>();

        if (layout != null)
        {
            layout.childForceExpandWidth = true;
        }

        return row;
    }

    private void CreateAssetCard(Transform parent, PeriodAssetBalance asset)
    {
        var card = RuntimeUiFactory.CreateSurface($"Asset_{asset.AssetId}", parent, RuntimeUiFactory.ElevatedSurfaceColor);
        AddLayoutElement(card.gameObject, preferredWidth: 0f, preferredHeight: 166f, flexibleWidth: 1f);

        var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var title = RuntimeUiFactory.CreateBodyText(card, string.Empty);
        RuntimeUiFactory.ApplyTextStyle(title, FontStyle.Bold);
        var value = RuntimeUiFactory.CreateValueText(card, "0", 28, TextAnchor.MiddleLeft);
        var caption = RuntimeUiFactory.CreateCaption(card, string.Empty);
        RuntimeUiFactory.AddFlexibleSpacer(card);
        var actions = CreateRow(card, "Actions", 10f, TextAnchor.MiddleCenter);
        var depositButton = RuntimeUiFactory.CreatePrimaryButton(actions, "Пополнить", 42f);
        var withdrawButton = RuntimeUiFactory.CreateSecondaryButton(actions, "Снять", 42f);

        _assetCards[asset.AssetId] = new AssetCardWidgets
        {
            AssetId = asset.AssetId,
            TitleLabel = title,
            ValueLabel = value,
            CaptionLabel = caption,
            DepositButton = depositButton,
            WithdrawButton = withdrawButton
        };
    }

    private static bool ShouldUseScrollableAssetsLayout(IReadOnlyList<PeriodAssetBalance> assets)
    {
        return assets != null && assets.Count > 3;
    }

    private static bool IsPrimaryAsset(PeriodAssetBalance asset)
    {
        return asset != null
               && (asset.AssetType == PeriodAssetType.Cash
                   || asset.AssetType == PeriodAssetType.Deposit);
    }

    private void SetEmptyState(string message)
    {
        _mainLayout.gameObject.SetActive(false);
        _footerRow.gameObject.SetActive(false);
        _emptyStateLabel.gameObject.SetActive(true);
        _emptyStateLabel.text = message ?? string.Empty;
        _periodTitleLabel.text = "Период";
        _periodSubtitleLabel.text = string.Empty;
        _periodSubtitleLabel.gameObject.SetActive(false);
        _phaseLabel.text = string.Empty;
        _statusTextLabel.text = string.Empty;
        _validationLabel.text = string.Empty;
    }

    private void ShowMainState()
    {
        _mainLayout.gameObject.SetActive(true);
        _footerRow.gameObject.SetActive(true);
        _emptyStateLabel.gameObject.SetActive(false);
    }

    private static PeriodExpenseState FindExpenseState(PeriodRuntimeState runtimeState, string expenseId)
    {
        foreach (var expenseState in runtimeState.Expenses)
        {
            if (expenseState != null && string.Equals(expenseState.ExpenseId, expenseId, StringComparison.Ordinal))
            {
                return expenseState;
            }
        }

        return null;
    }

    private static ConsumerCreditContractRuntime FindConsumerCredit(PeriodRuntimeState runtimeState, string expenseId)
    {
        if (runtimeState == null
            || runtimeState.Definition == null
            || runtimeState.Definition.ConsumerCredits == null
            || !ConsumerCreditMath.IsCreditExpenseId(expenseId))
        {
            return null;
        }

        var creditId = ConsumerCreditMath.ExtractCreditId(expenseId);

        for (var index = 0; index < runtimeState.Definition.ConsumerCredits.Count; index++)
        {
            var credit = runtimeState.Definition.ConsumerCredits[index];

            if (credit != null && string.Equals(credit.CreditId, creditId, StringComparison.Ordinal))
            {
                return credit;
            }
        }

        return null;
    }

    private static bool HasActiveMortgage(PeriodRuntimeState runtimeState)
    {
        if (runtimeState == null
            || runtimeState.Definition == null
            || runtimeState.Definition.ConsumerCredits == null)
        {
            return false;
        }

        for (var index = 0; index < runtimeState.Definition.ConsumerCredits.Count; index++)
        {
            var credit = runtimeState.Definition.ConsumerCredits[index];

            if (credit != null
                && string.Equals(credit.ContractType, ConsumerCreditMath.MortgageKind, StringComparison.Ordinal)
                && credit.RemainingPeriods > 0
                && credit.RemainingPrincipal > 0.01d)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFixedAmountExpense(PeriodExpenseDefinition definition)
    {
        return definition != null
               && definition.MinimumAmount > 0.01d
               && definition.MaximumAmount > 0.01d
               && Math.Abs(definition.MaximumAmount - definition.MinimumAmount) <= 0.01d;
    }

    private static bool NeedsRebuild<TItem, TDefinition>(
        Dictionary<string, TItem> registry,
        IReadOnlyList<TDefinition> definitions,
        Func<TDefinition, string> keySelector)
    {
        if (definitions == null || registry.Count != definitions.Count)
        {
            return true;
        }

        foreach (var definition in definitions)
        {
            var key = keySelector != null ? keySelector(definition) : string.Empty;

            if (string.IsNullOrWhiteSpace(key) || !registry.ContainsKey(key))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildStatusText(PeriodRuntimeState runtimeState)
    {
        var summary = runtimeState.Summary ?? PeriodCalculationSummary.Empty;

        return
            $"Фаза: {FormatFlow(runtimeState.FlowState)}\n" +
            $"Всего расходов: {FormatMoney(summary.TotalExpenses)}\n" +
            $"Наличные: {FormatMoney(summary.EndingCashEcu)}\n" +
            $"Депозит: {FormatMoney(summary.EndingDepositEcu)}";
    }

    private static string FirstIssueMessage(PeriodCalculationSummary summary)
    {
        if (summary == null || summary.ValidationIssues == null)
        {
            return string.Empty;
        }

        foreach (var issue in summary.ValidationIssues)
        {
            if (issue != null && issue.IsBlocking && !string.IsNullOrWhiteSpace(issue.Message))
            {
                return issue.Message;
            }
        }

        return string.Empty;
    }

    private static string FormatMoney(double value)
    {
        return EcuFormatter.FormatAmount(value);
    }

    private static string FormatInfoValue(PeriodInfoBlockValue infoValue)
    {
        if (infoValue == null || !infoValue.HasValue)
        {
            return "-";
        }

        if (string.Equals(infoValue.Id, "income_growth", StringComparison.Ordinal))
        {
            return FormatRoundedGrowthPercent(infoValue.NumericValue, infoValue.RawText);
        }

        if (infoValue.NumericValue.HasValue)
        {
            var formatted = infoValue.NumericValue.Value.ToString("0.##", CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(infoValue.Suffix)
                ? formatted
                : $"{formatted}{infoValue.Suffix}";
        }

        return infoValue.DisplayValue;
    }

    private static string FormatRoundedGrowthPercent(double? multiplier, string fallback = "-")
    {
        if (!multiplier.HasValue)
        {
            return fallback;
        }

        var percent = (multiplier.Value - 1d) * 100d;
        var prefix = percent > 0d
            ? "+"
            : string.Empty;
        return $"{prefix}{percent.ToString("0.##", CultureInfo.InvariantCulture)}%";
    }

    private static bool IsCompletedEducationExpense(
        PeriodRuntimeState runtimeState,
        PeriodExpenseDefinition expenseDefinition)
    {
        return runtimeState != null
               && runtimeState.HasDefinition
               && expenseDefinition != null
               && string.Equals(expenseDefinition.Id, "education", StringComparison.Ordinal)
               && runtimeState.Definition.EducationGoal != null
               && runtimeState.Definition.EducationGoal.IsCompleted;
    }

    private static string BuildEducationMeta(
        PeriodRuntimeState runtimeState,
        PeriodExpenseState expenseState)
    {
        var goal = runtimeState != null && runtimeState.HasDefinition
            ? runtimeState.Definition.EducationGoal
            : null;
        var targetAmount = goal != null && goal.TargetAmount > 0d
            ? goal.TargetAmount
            : runtimeState != null && runtimeState.HasDefinition
                ? ConsumerCreditMath.CalculateEducationTargetAmount(runtimeState.Definition)
                : ConsumerCreditMath.CalculateEducationTargetAmount(ConsumerCreditMath.DefaultBaseIncomeEcu);
        var committedAmount = goal != null
            ? Math.Max(0d, goal.AccumulatedAmount)
            : 0d;
        var currentContribution = expenseState != null
            ? Math.Max(0d, expenseState.Amount)
            : 0d;
        var displayedProgress = Math.Min(targetAmount, committedAmount + currentContribution);

        if (displayedProgress + 0.01d < targetAmount)
        {
            return $"{FormatMoney(displayedProgress)} / {FormatMoney(targetAmount)}";
        }

        if (goal != null && goal.IncomeBoostStartPeriodNumber > 0)
        {
            var currentPeriodNumber = runtimeState != null ? runtimeState.PeriodNumber : 0;

            return currentPeriodNumber >= goal.IncomeBoostStartPeriodNumber
                ? "цель достигнута | доход x1.5"
                : $"цель достигнута | доход x1.5 с {goal.IncomeBoostStartPeriodNumber} пер.";
        }

        return "цель достигнута";
    }

    private static string FormatFlow(PeriodFlowState flowState)
    {
        switch (flowState)
        {
            case PeriodFlowState.LoadingData:
                return "Загрузка";
            case PeriodFlowState.PeriodIntro:
                return "Старт";
            case PeriodFlowState.PeriodActive:
                return "Активно";
            case PeriodFlowState.PeriodValidation:
                return "Проверка";
            case PeriodFlowState.PeriodClosing:
                return "Закрытие";
            case PeriodFlowState.PeriodCheckpointSubmitting:
                return "Отправка";
            case PeriodFlowState.PeriodClosed:
                return "Закрыт";
            default:
                return "Ожидание";
        }
    }

    private Text CreateMetricCard(Transform parent, string label, string value)
    {
        var card = RuntimeUiFactory.CreateSurface($"{label}Card", parent, RuntimeUiFactory.SurfaceColor);
        AddLayoutElement(card.gameObject, preferredWidth: 0f, preferredHeight: 126f, flexibleWidth: 1f);

        var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 16);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        if (_ujeValueLabel == null && _ujeDeltaLabel == null)
        {
            RuntimeUiFactory.CreateCaption(card, "\u0423\u0416\u042D (\u0436\u0438\u0437\u043d\u0435\u043d\u043d\u0430\u044f \u044d\u043d\u0435\u0440\u0433\u0438\u044f)");
            var valueRow = CreateRow(card, "UjeValueRow", 12f, TextAnchor.MiddleLeft);
            var mainValue = RuntimeUiFactory.CreateValueText(valueRow, value, 34, TextAnchor.MiddleLeft);
            _ujeDeltaLabel = RuntimeUiFactory.CreateValueText(valueRow, string.Empty, 20, TextAnchor.MiddleLeft);
            AddLayoutElement(_ujeDeltaLabel.gameObject, preferredWidth: 90f);
            _ujeDeltaLabel.gameObject.SetActive(false);
            return mainValue;
        }

        RuntimeUiFactory.CreateCaption(card, label);
        return RuntimeUiFactory.CreateValueText(card, value, 34, TextAnchor.MiddleLeft);
    }

    private void ApplyUjeDelta(double delta)
    {
        if (_ujeDeltaLabel == null)
        {
            return;
        }

        if (Math.Abs(delta) <= 0.001d)
        {
            _ujeDeltaLabel.text = string.Empty;
            _ujeDeltaLabel.gameObject.SetActive(false);
            return;
        }

        _ujeDeltaLabel.text = delta > 0d
            ? $"+{delta.ToString("0.0", CultureInfo.InvariantCulture)}"
            : delta.ToString("0.0", CultureInfo.InvariantCulture);
        _ujeDeltaLabel.color = delta > 0d
            ? UjePositiveColor
            : RuntimeUiFactory.DangerColor;
        _ujeDeltaLabel.gameObject.SetActive(true);
    }

    private static RectTransform CreateSection(Transform parent, string title)
    {
        var section = RuntimeUiFactory.CreateSurface(title, parent, RuntimeUiFactory.SurfaceColor);
        var layout = section.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        RuntimeUiFactory.CreateBodyText(section, title);
        var content = CreateVerticalGroup(section, "Content", 10f, TextAnchor.UpperLeft);
        AddLayoutElement(content.gameObject, flexibleHeight: 1f);
        return content;
    }

    private RectTransform CreateScrollableSection(Transform parent, string title, out ScrollRect scrollRect)
    {
        var section = RuntimeUiFactory.CreateSurface(title, parent, RuntimeUiFactory.SurfaceColor);
        AddLayoutElement(section.gameObject, flexibleHeight: 1f);
        var layout = section.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        RuntimeUiFactory.CreateBodyText(section, title);

        var hintRow = CreateRow(section, "ExpenseHintRow", 8f, TextAnchor.MiddleLeft);
        AddLayoutElement(hintRow.gameObject, preferredHeight: 22f);
        _expenseHintLabel = RuntimeUiFactory.CreateCaption(hintRow, " ", TextAnchor.MiddleLeft);
        _expenseHintLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        _expenseHintLabel.verticalOverflow = VerticalWrapMode.Overflow;
        _expenseHintLabel.color = RuntimeUiFactory.TextSecondaryColor;

        var scrollArea = CreateRow(section, "ExpenseScrollArea", 8f, TextAnchor.UpperLeft);
        var scrollAreaLayout = scrollArea.GetComponent<HorizontalLayoutGroup>();
        scrollAreaLayout.childForceExpandWidth = false;
        scrollAreaLayout.childForceExpandHeight = true;
        scrollAreaLayout.childControlHeight = true;
        AddLayoutElement(scrollArea.gameObject, flexibleHeight: 1f, minimumHeight: 260f);

        var viewport = CreateRect("ExpenseViewport", scrollArea);
        AddLayoutElement(viewport.gameObject, flexibleWidth: 1f, flexibleHeight: 1f, minimumHeight: 260f);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.white;
        var viewportMask = viewport.gameObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        scrollRect = section.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;
        var verticalScrollbar = CreateVerticalScrollbar(scrollArea);
        scrollRect.verticalScrollbar = verticalScrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scrollRect.onValueChanged.AddListener(_ => OnExpenseScrollChanged());

        var content = RuntimeUiFactory.CreateContentRoot(
            "ExpenseContent",
            viewport,
            new RectOffset(0, 0, 0, 0),
            10f);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = content;

        _expenseTopFade = CreateExpenseFadeOverlay(viewport, "ExpenseTopFade", true);
        _expenseBottomFade = CreateExpenseFadeOverlay(viewport, "ExpenseBottomFade", false);
        return content;
    }

    private static RectTransform CreateVerticalGroup(
        Transform parent,
        string name,
        float spacing,
        TextAnchor alignment)
    {
        var rect = CreateRect(name, parent);
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return rect;
    }

    private static RectTransform CreateRow(
        Transform parent,
        string name,
        float spacing,
        TextAnchor alignment)
    {
        var rect = CreateRect(name, parent);
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return rect;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static void AddLayoutElement(
        GameObject target,
        float minimumWidth = -1f,
        float minimumHeight = -1f,
        float preferredWidth = -1f,
        float preferredHeight = -1f,
        float flexibleWidth = -1f,
        float flexibleHeight = -1f)
    {
        var layoutElement = target.GetComponent<LayoutElement>();

        if (layoutElement == null)
        {
            layoutElement = target.AddComponent<LayoutElement>();
        }

        if (preferredWidth >= 0f)
        {
            layoutElement.preferredWidth = preferredWidth;
        }

        if (preferredHeight >= 0f)
        {
            layoutElement.preferredHeight = preferredHeight;
        }

        if (minimumWidth >= 0f)
        {
            layoutElement.minWidth = minimumWidth;
        }

        if (minimumHeight >= 0f)
        {
            layoutElement.minHeight = minimumHeight;
        }

        if (flexibleWidth >= 0f)
        {
            layoutElement.flexibleWidth = flexibleWidth;
        }

        if (flexibleHeight >= 0f)
        {
            layoutElement.flexibleHeight = flexibleHeight;
        }
    }

    private static void ClearChildren(Transform parent)
    {
        for (var index = parent.childCount - 1; index >= 0; index--)
        {
            Destroy(parent.GetChild(index).gameObject);
        }
    }

    private void OnExpenseScrollChanged()
    {
        if (_expenseScrollRect != null && _expenseScrollRect.verticalNormalizedPosition < 0.995f)
        {
            _hasDismissedExpenseScrollHint = true;
        }

        UpdateExpenseScrollVisuals();
    }

    private void UpdateExpenseScrollUi(PeriodRuntimeState runtimeState)
    {
        UpdateExpenseHint(runtimeState);
        UpdateExpenseScrollVisuals();
    }

    private void UpdateExpenseHint(PeriodRuntimeState runtimeState)
    {
        if (_expenseHintLabel == null || runtimeState == null || !runtimeState.HasDefinition)
        {
            return;
        }

        var remainingRequiredCount = CountRemainingRequiredExpenses(runtimeState);
        var hasOverflow = HasExpenseScrollOverflow();
        var showScrollHint = hasOverflow && !_hasDismissedExpenseScrollHint;

        if (remainingRequiredCount > 0 && showScrollHint)
        {
            _expenseHintLabel.text = $"Осталось заполнить {FormatRequiredExpenseCount(remainingRequiredCount)}. Прокрутите список, чтобы увидеть все расходы.";
        }
        else if (remainingRequiredCount > 0)
        {
            _expenseHintLabel.text = $"Осталось заполнить {FormatRequiredExpenseCount(remainingRequiredCount)}.";
        }
        else if (showScrollHint)
        {
            _expenseHintLabel.text = "Прокрутите список, чтобы увидеть все расходы.";
        }
        else
        {
            _expenseHintLabel.text = " ";
        }
    }

    private void UpdateExpenseScrollVisuals()
    {
        var hasOverflow = HasExpenseScrollOverflow();
        var showTopFade = hasOverflow
                          && _expenseScrollRect != null
                          && _expenseScrollRect.verticalNormalizedPosition < 0.995f;
        var showBottomFade = hasOverflow
                             && _expenseScrollRect != null
                             && _expenseScrollRect.verticalNormalizedPosition > 0.005f;

        if (_expenseTopFade != null)
        {
            _expenseTopFade.gameObject.SetActive(showTopFade);
        }

        if (_expenseBottomFade != null)
        {
            _expenseBottomFade.gameObject.SetActive(showBottomFade);
        }
    }

    private bool HasExpenseScrollOverflow()
    {
        if (_expenseScrollRect == null || _expenseScrollRect.content == null || _expenseScrollRect.viewport == null)
        {
            return false;
        }

        var contentHeight = LayoutUtility.GetPreferredHeight(_expenseScrollRect.content);
        var viewportHeight = _expenseScrollRect.viewport.rect.height;
        return contentHeight > viewportHeight + 4f;
    }

    private static int CountRemainingRequiredExpenses(PeriodRuntimeState runtimeState)
    {
        if (runtimeState == null || !runtimeState.HasDefinition || runtimeState.Definition.ExpenseDefinitions == null)
        {
            return 0;
        }

        var count = 0;

        for (var index = 0; index < runtimeState.Definition.ExpenseDefinitions.Count; index++)
        {
            var definition = runtimeState.Definition.ExpenseDefinitions[index];

            if (definition == null || !definition.IsRequired || IsExpenseSatisfied(runtimeState, definition))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static bool IsExpenseSatisfied(PeriodRuntimeState runtimeState, PeriodExpenseDefinition definition)
    {
        if (runtimeState == null || definition == null)
        {
            return true;
        }

        var state = FindExpenseState(runtimeState, definition.Id);
        var amount = state != null ? Math.Max(0d, state.Amount) : 0d;

        if (IsFixedAmountExpense(definition))
        {
            return Math.Abs(amount - definition.MinimumAmount) <= 0.01d;
        }

        var minimumAmount = definition.MinimumAmount > 0d
            ? definition.MinimumAmount
            : 0.01d;
        return amount + 0.01d >= minimumAmount;
    }

    private static string FormatRequiredExpenseCount(int count)
    {
        if (count <= 1)
        {
            return "1 обязательный расход";
        }

        var remainder10 = count % 10;
        var remainder100 = count % 100;

        if (remainder10 >= 2 && remainder10 <= 4 && (remainder100 < 12 || remainder100 > 14))
        {
            return $"{count} обязательных расхода";
        }

        return $"{count} обязательных расходов";
    }

    private static Image CreateExpenseFadeOverlay(Transform parent, string name, bool isTop)
    {
        var root = CreateRect(name, parent);
        root.SetAsLastSibling();
        root.anchorMin = isTop ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
        root.anchorMax = isTop ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
        root.pivot = isTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        root.sizeDelta = new Vector2(0f, 24f);
        root.anchoredPosition = Vector2.zero;

        var image = root.gameObject.AddComponent<Image>();
        image.color = new Color(
            RuntimeUiFactory.SurfaceColor.r,
            RuntimeUiFactory.SurfaceColor.g,
            RuntimeUiFactory.SurfaceColor.b,
            0.68f);
        image.raycastTarget = false;

        CreateFadeBand(root, "BandStrong", isTop ? 0f : 14f, 12f, 0.34f);
        CreateFadeBand(root, "BandMedium", isTop ? 8f : 7f, 10f, 0.22f);
        CreateFadeBand(root, "BandSoft", isTop ? 16f : 1f, 8f, 0.12f);
        root.gameObject.SetActive(false);
        return image;
    }

    private static void CreateFadeBand(Transform parent, string name, float topOffset, float height, float alpha)
    {
        var band = CreateRect(name, parent);
        band.anchorMin = new Vector2(0f, 1f);
        band.anchorMax = new Vector2(1f, 1f);
        band.pivot = new Vector2(0.5f, 1f);
        band.anchoredPosition = new Vector2(0f, -topOffset);
        band.sizeDelta = new Vector2(0f, height);

        var image = band.gameObject.AddComponent<Image>();
        image.color = new Color(
            RuntimeUiFactory.SurfaceColor.r,
            RuntimeUiFactory.SurfaceColor.g,
            RuntimeUiFactory.SurfaceColor.b,
            alpha);
        image.raycastTarget = false;
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

    private static Scrollbar CreateVerticalScrollbar(Transform parent)
    {
        var root = CreateRect("VerticalScrollbar", parent);
        AddLayoutElement(root.gameObject, preferredWidth: 8f, minimumWidth: 8f, flexibleHeight: 1f);

        var trackImage = root.gameObject.AddComponent<Image>();
        trackImage.color = new Color(
            RuntimeUiFactory.BorderColor.r,
            RuntimeUiFactory.BorderColor.g,
            RuntimeUiFactory.BorderColor.b,
            0.55f);

        var scrollbar = root.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.size = 0.25f;
        scrollbar.targetGraphic = trackImage;

        var slidingArea = CreateRect("SlidingArea", root);
        slidingArea.anchorMin = Vector2.zero;
        slidingArea.anchorMax = Vector2.one;
        slidingArea.offsetMin = Vector2.zero;
        slidingArea.offsetMax = Vector2.zero;

        var handle = CreateRect("Handle", slidingArea);
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(1f, 0.2f);
        handle.offsetMin = Vector2.zero;
        handle.offsetMax = Vector2.zero;

        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = RuntimeUiFactory.PrimaryColor;
        scrollbar.handleRect = handle;
        return scrollbar;
    }

    private static void SyncInputFieldText(InputField inputField, string value)
    {
        if (inputField == null)
        {
            return;
        }

        var safeValue = value ?? string.Empty;
        var preserveFocusedInput = inputField.isFocused
                                   && NumericInputParser.ShouldPreserveFocusedInput(inputField.text, safeValue);

        if (!preserveFocusedInput && inputField.text != safeValue)
        {
            inputField.text = safeValue;
        }

        if (inputField.textComponent != null)
        {
            inputField.textComponent.gameObject.SetActive(true);
            inputField.textComponent.text = preserveFocusedInput
                ? inputField.text
                : safeValue;
            inputField.textComponent.color = RuntimeUiFactory.TextPrimaryColor;
            inputField.textComponent.enabled = true;
        }

        if (inputField.placeholder is Graphic placeholderGraphic)
        {
            placeholderGraphic.gameObject.SetActive(true);
            placeholderGraphic.enabled = string.IsNullOrWhiteSpace(preserveFocusedInput
                ? inputField.text
                : safeValue);
        }

        inputField.ForceLabelUpdate();

        var rectTransform = inputField.transform as RectTransform;

        if (rectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }
}
