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
        public GameObject Root;
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
        public GameObject Root;
        public Text TitleLabel;
        public Text ValueLabel;
        public Text CaptionLabel;
        public Button DepositButton;
        public Button WithdrawButton;
    }

    private sealed class InfoRowWidgets
    {
        public string InfoId;
        public GameObject Root;
        public Text Label;
        public Text Value;
    }

    private sealed class HighlightFrameState
    {
        public Graphic Graphic;
        public Color OriginalGraphicColor;
        public bool OriginalGraphicRaycastTarget;
        public Outline Outline;
        public Color OriginalOutlineColor;
        public Vector2 OriginalOutlineDistance;
        public bool OriginalOutlineEnabled;
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
    private Text _instructionPopupTitleLabel;
    private Text _instructionPopupBodyLabel;
    private Button _backButton;
    private Button _instructionButton;
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
    private RectTransform _shellContent;
    private RectTransform _mainLayout;
    private RectTransform _footerRow;
    private ScrollRect _shellScrollRect;
    private ScrollRect _expenseScrollRect;
    private ScrollRect _instructionPopupScrollRect;
    private Image _expenseTopFade;
    private Image _expenseBottomFade;
    private RectTransform _instructionPopupOverlay;
    private readonly Dictionary<string, ExpenseRowWidgets> _expenseRows = new Dictionary<string, ExpenseRowWidgets>();
    private readonly Dictionary<string, AssetCardWidgets> _assetCards = new Dictionary<string, AssetCardWidgets>();
    private readonly Dictionary<string, InfoRowWidgets> _infoRows = new Dictionary<string, InfoRowWidgets>();
    private readonly Dictionary<GameObject, HighlightFrameState> _featureHighlightFrames = new Dictionary<GameObject, HighlightFrameState>();
    private bool _hasDismissedExpenseScrollHint;
    private bool _isBuilt;
    private string _instructionReferenceTitle = string.Empty;
    private string _instructionReferenceBody = string.Empty;
    private static readonly Color UjePositiveColor = new Color32(84, 156, 110, 255);
    private static readonly Color NewFeatureHighlightColor = new Color32(214, 73, 73, 255);

    public override ScreenController Construct(UIContext context)
    {
        EnsureBuilt();
        return new GameplayPlaceholderScreenController(this, context);
    }

    public void Render(
        PeriodRuntimeState runtimeState,
        string instructionTitle,
        string instructionBody,
        Action onBack,
        Action onComplete,
        Action<string, string> onExpenseAmountChanged,
        Action<string> onApplyRequiredExpenseAmount,
        Action<string, FundsSourceType> onExpenseSourceChanged,
        Action<string, AssetOperationKind> onAssetAction,
        Action onConsumerCreditAction,
        Action onApartmentPurchaseAction,
        Action onMortgageAction,
        Action onPdsAction,
        IReadOnlyCollection<string> newlyEnabledFeatures)
    {
        EnsureBuilt();
        _instructionReferenceTitle = instructionTitle ?? string.Empty;
        _instructionReferenceBody = instructionBody ?? string.Empty;
        BindButton(_backButton, onBack);
        BindButton(_completeButton, onComplete);
        BindButton(_consumerCreditButton, onConsumerCreditAction);
        BindButton(_apartmentButton, onApartmentPurchaseAction);
        BindButton(_mortgageButton, onMortgageAction);
        BindButton(_pdsButton, onPdsAction);
        _instructionButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(_instructionReferenceBody));
        if (string.IsNullOrWhiteSpace(_instructionReferenceBody))
        {
            CloseInstructionReferencePopup();
        }

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
            CloseInstructionReferencePopup();
            return;
        }

        ShowMainState();
        ApplyHeader(runtimeState);
        ApplyMetrics(runtimeState);
        ApplyInfo(runtimeState);
        ApplyExpenses(runtimeState, onExpenseAmountChanged, onApplyRequiredExpenseAmount, onExpenseSourceChanged);
        ApplyActionButtons(runtimeState);
        ApplyAssets(runtimeState, onAssetAction);
        ApplyFeatureHighlights(runtimeState, newlyEnabledFeatures);
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
        _shellContent = CreateShellScrollContent(shell);

        BuildHeader(_shellContent);
        BuildMetrics(_shellContent);
        BuildMain(_shellContent);
        BuildFooter(_shellContent);
        BuildInstructionReferencePopup(background);

        _emptyStateLabel = RuntimeUiFactory.CreateBodyText(_shellContent, string.Empty, TextAnchor.MiddleCenter);
        _emptyStateLabel.color = RuntimeUiFactory.TextSecondaryColor;
        _emptyStateLabel.gameObject.SetActive(false);
    }

    private RectTransform CreateShellScrollContent(Transform parent)
    {
        var scrollArea = CreateRow(parent, "ShellScrollArea", 10f, TextAnchor.UpperLeft);
        Stretch(scrollArea, 20f, 18f, 20f, 18f);
        var scrollAreaLayout = scrollArea.GetComponent<HorizontalLayoutGroup>();
        scrollAreaLayout.childForceExpandWidth = false;
        scrollAreaLayout.childForceExpandHeight = true;
        scrollAreaLayout.childControlHeight = true;

        var viewport = CreateRect("ShellViewport", scrollArea);
        AddLayoutElement(viewport.gameObject, flexibleWidth: 1f, flexibleHeight: 1f);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.white;
        var viewportMask = viewport.gameObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        _shellScrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();
        _shellScrollRect.viewport = viewport;
        _shellScrollRect.horizontal = false;
        _shellScrollRect.vertical = true;
        _shellScrollRect.movementType = ScrollRect.MovementType.Clamped;
        _shellScrollRect.scrollSensitivity = 24f;
        var verticalScrollbar = CreateVerticalScrollbar(scrollArea);
        _shellScrollRect.verticalScrollbar = verticalScrollbar;
        _shellScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        var content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 6, 4, 4);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _shellScrollRect.content = content;
        return content;
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
        AddLayoutElement(controls.gameObject, preferredWidth: 430f);
        _phaseLabel = RuntimeUiFactory.CreateCaption(controls, string.Empty, TextAnchor.MiddleCenter);
        AddLayoutElement(_phaseLabel.gameObject, preferredWidth: 120f);
        _instructionButton = RuntimeUiFactory.CreateSecondaryButton(controls, "Инструкция", 46f);
        AddLayoutElement(_instructionButton.gameObject, preferredWidth: 170f, minimumWidth: 170f, preferredHeight: 46f, flexibleWidth: 0f);
        BindButton(_instructionButton, OpenInstructionReferencePopup);
        _instructionButton.gameObject.SetActive(false);
        _backButton = RuntimeUiFactory.CreateSecondaryButton(controls, "Назад", 46f);
        AddLayoutElement(_backButton.gameObject, preferredWidth: 120f, minimumWidth: 120f, preferredHeight: 46f, flexibleWidth: 0f);
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

        var leftColumn = CreateVerticalGroup(_mainLayout, "LeftColumn", 16f, TextAnchor.UpperLeft);
        AddLayoutElement(leftColumn.gameObject, preferredWidth: 1040f, flexibleWidth: 1.3f);

        var rightColumn = CreateVerticalGroup(_mainLayout, "RightColumn", 16f, TextAnchor.UpperLeft);
        AddLayoutElement(rightColumn.gameObject, preferredWidth: 400f, flexibleWidth: 0.7f);

        _expenseContent = CreateScrollableSection(leftColumn, "Расходы", out _expenseScrollRect);

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

    private void BuildInstructionReferencePopup(Transform parent)
    {
        _instructionPopupOverlay = CreateRect("InstructionReferenceOverlay", parent);
        Stretch(_instructionPopupOverlay, 0f, 0f, 0f, 0f);
        _instructionPopupOverlay.SetAsLastSibling();

        var overlayImage = _instructionPopupOverlay.gameObject.AddComponent<Image>();
        overlayImage.color = new Color(0.12f, 0.15f, 0.22f, 0.6f);

        var overlayButton = _instructionPopupOverlay.gameObject.AddComponent<Button>();
        overlayButton.transition = Selectable.Transition.None;
        overlayButton.onClick.AddListener(CloseInstructionReferencePopup);

        var card = RuntimeUiFactory.CreateCard("InstructionReferenceCard", _instructionPopupOverlay, new Vector2(1120f, 820f));
        var content = RuntimeUiFactory.CreateContentRoot(
            "InstructionReferenceContent",
            card,
            new RectOffset(22, 22, 20, 18),
            12f);

        var headerRow = RuntimeUiFactory.CreateRow("InstructionReferenceHeader", content, 12f, TextAnchor.MiddleCenter);
        var headerLayout = headerRow.GetComponent<HorizontalLayoutGroup>();
        headerLayout.childForceExpandWidth = false;
        headerLayout.childControlWidth = true;

        _instructionPopupTitleLabel = RuntimeUiFactory.CreateTitle(headerRow, "Инструкция", TextAnchor.MiddleLeft);
        AddLayoutElement(_instructionPopupTitleLabel.gameObject, flexibleWidth: 1f);

        var closeButton = RuntimeUiFactory.CreateSecondaryButton(headerRow, "Закрыть", 42f);
        AddLayoutElement(closeButton.gameObject, preferredWidth: 140f, minimumWidth: 140f, preferredHeight: 42f, flexibleWidth: 0f);
        closeButton.onClick.AddListener(CloseInstructionReferencePopup);

        var bodyPanel = RuntimeUiFactory.CreatePanel(
            "InstructionReferenceBodyPanel",
            content,
            new RectOffset(12, 12, 12, 12),
            10f,
            RuntimeUiFactory.SurfaceColor);
        DisableContentSizeFitter(bodyPanel);
        AddLayoutElement(bodyPanel.gameObject, flexibleHeight: 1f, minimumHeight: 600f);

        var scrollArea = RuntimeUiFactory.CreateRow("InstructionReferenceScrollArea", bodyPanel, 8f, TextAnchor.UpperLeft);
        var scrollAreaLayout = scrollArea.GetComponent<HorizontalLayoutGroup>();
        scrollAreaLayout.childForceExpandWidth = false;
        scrollAreaLayout.childForceExpandHeight = true;
        scrollAreaLayout.childControlHeight = true;
        AddLayoutElement(scrollArea.gameObject, flexibleHeight: 1f, minimumHeight: 560f);

        var viewport = CreateRect("InstructionReferenceViewport", scrollArea);
        AddLayoutElement(viewport.gameObject, flexibleWidth: 1f, flexibleHeight: 1f, minimumHeight: 560f);
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.white;
        var viewportMask = viewport.gameObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        _instructionPopupScrollRect = bodyPanel.gameObject.AddComponent<ScrollRect>();
        _instructionPopupScrollRect.viewport = viewport;
        _instructionPopupScrollRect.horizontal = false;
        _instructionPopupScrollRect.vertical = true;
        _instructionPopupScrollRect.movementType = ScrollRect.MovementType.Clamped;
        _instructionPopupScrollRect.scrollSensitivity = 24f;
        var scrollbar = CreateVerticalScrollbar(scrollArea);
        _instructionPopupScrollRect.verticalScrollbar = scrollbar;
        _instructionPopupScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        var bodyContent = RuntimeUiFactory.CreateContentRoot(
            "InstructionReferenceBodyContent",
            viewport,
            new RectOffset(0, 0, 0, 0),
            8f);
        bodyContent.anchorMin = new Vector2(0f, 1f);
        bodyContent.anchorMax = new Vector2(1f, 1f);
        bodyContent.pivot = new Vector2(0.5f, 1f);
        bodyContent.anchoredPosition = Vector2.zero;
        bodyContent.sizeDelta = new Vector2(0f, 0f);
        var contentFitter = bodyContent.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _instructionPopupScrollRect.content = bodyContent;

        _instructionPopupBodyLabel = RuntimeUiFactory.CreateBodyText(bodyContent, string.Empty);
        _instructionPopupBodyLabel.alignment = TextAnchor.UpperLeft;
        _instructionPopupBodyLabel.supportRichText = true;
        _instructionPopupBodyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        _instructionPopupBodyLabel.verticalOverflow = VerticalWrapMode.Overflow;

        _instructionPopupOverlay.gameObject.SetActive(false);
    }

    private void OpenInstructionReferencePopup()
    {
        if (_instructionPopupOverlay == null || string.IsNullOrWhiteSpace(_instructionReferenceBody))
        {
            return;
        }

        _instructionPopupTitleLabel.text = string.IsNullOrWhiteSpace(_instructionReferenceTitle)
            ? "Инструкция"
            : _instructionReferenceTitle;
        _instructionPopupBodyLabel.text = SimpleMarkdownFormatter.Format(_instructionReferenceBody);
        _instructionPopupOverlay.gameObject.SetActive(true);
        _instructionPopupOverlay.SetAsLastSibling();

        if (_instructionPopupScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            _instructionPopupScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void CloseInstructionReferencePopup()
    {
        if (_instructionPopupOverlay != null)
        {
            _instructionPopupOverlay.gameObject.SetActive(false);
        }
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
                var showRequiredBadge = expenseDefinition.IsRequired && expenseDefinition.MinimumAmount > 0d
                    || string.Equals(expenseDefinition.Id, "holiday", StringComparison.Ordinal) && expenseDefinition.MaximumAmount > 0d
                    || string.Equals(expenseDefinition.Id, "leisure", StringComparison.Ordinal);
                widgets.RequiredBadge.SetActive(showRequiredBadge);

                if (showRequiredBadge && widgets.RequiredBadgeLabel != null)
                {
                    widgets.RequiredBadgeLabel.text = string.Equals(expenseDefinition.Id, "housing_rent", StringComparison.Ordinal)
                        ? FormatMoney(expenseDefinition.MinimumAmount)
                        : $"мин. {FormatMoney(expenseDefinition.MinimumAmount)}";
                    var badgeAmount = GetExpenseBadgeAmount(runtimeState, expenseDefinition);
                    widgets.RequiredBadgeLabel.text = string.Equals(expenseDefinition.Id, "goods_services", StringComparison.Ordinal)
                        ? $"мин. {FormatMoney(badgeAmount)}"
                        : FormatMoney(badgeAmount);
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

        if (_expenseContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_expenseContent);
        }

        if (_mainLayout != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_mainLayout);
        }

        if (_shellContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_shellContent);
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

    private void ApplyFeatureHighlights(
        PeriodRuntimeState runtimeState,
        IReadOnlyCollection<string> newlyEnabledFeatures)
    {
        ResetFeatureHighlights();

        if (runtimeState == null
            || !runtimeState.HasDefinition
            || newlyEnabledFeatures == null
            || newlyEnabledFeatures.Count == 0)
        {
            return;
        }

        var featureSet = new HashSet<string>(newlyEnabledFeatures, StringComparer.OrdinalIgnoreCase);

        if (featureSet.Contains("consumer_credit"))
        {
            SetFeatureHighlight(_consumerCreditButton != null ? _consumerCreditButton.gameObject : null, true);
            HighlightInfoRow("credit_rate");
        }

        if (featureSet.Contains("mortgage"))
        {
            SetFeatureHighlight(_apartmentButton != null ? _apartmentButton.gameObject : null, true);
            SetFeatureHighlight(_mortgageButton != null ? _mortgageButton.gameObject : null, true);
            HighlightInfoRow("mortgage_rate");
        }

        if (featureSet.Contains("pds"))
        {
            SetFeatureHighlight(_pdsButton != null ? _pdsButton.gameObject : null, true);
            HighlightAssetCard(ConsumerCreditMath.PdsAssetId);
        }

        if (featureSet.Contains("education"))
        {
            HighlightExpenseRow("education");
        }

        if (featureSet.Contains("child_expense"))
        {
            HighlightExpenseRow("child_expense");
        }

        if (featureSet.Contains("pension_info"))
        {
            HighlightInfoRow("pension_savings");
        }
    }

    private void HighlightExpenseRow(string expenseId)
    {
        if (!string.IsNullOrWhiteSpace(expenseId)
            && _expenseRows.TryGetValue(expenseId, out var widgets))
        {
            SetFeatureHighlight(widgets.Root, true);
        }
    }

    private void HighlightAssetCard(string assetId)
    {
        if (!string.IsNullOrWhiteSpace(assetId)
            && _assetCards.TryGetValue(assetId, out var widgets))
        {
            SetFeatureHighlight(widgets.Root, true);
        }
    }

    private void HighlightInfoRow(string infoId)
    {
        if (!string.IsNullOrWhiteSpace(infoId)
            && _infoRows.TryGetValue(infoId, out var widgets))
        {
            SetFeatureHighlight(widgets.Root, true);
        }
    }

    private void ResetFeatureHighlights()
    {
        foreach (var entry in _featureHighlightFrames)
        {
            SetFeatureHighlight(entry.Key, false);
        }
    }

    private void SetFeatureHighlight(GameObject target, bool isHighlighted)
    {
        if (target == null)
        {
            return;
        }

        if (!_featureHighlightFrames.TryGetValue(target, out var state))
        {
            var graphic = target.GetComponent<Graphic>();

            if (graphic == null)
            {
                var image = target.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0.01f);
                image.raycastTarget = false;
                graphic = image;
            }

            var outline = target.GetComponent<Outline>();

            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
                outline.effectColor = RuntimeUiFactory.BorderColor;
                outline.effectDistance = new Vector2(1f, -1f);
                outline.enabled = false;
            }

            state = new HighlightFrameState
            {
                Graphic = graphic,
                OriginalGraphicColor = graphic.color,
                OriginalGraphicRaycastTarget = graphic.raycastTarget,
                Outline = outline,
                OriginalOutlineColor = outline.effectColor,
                OriginalOutlineDistance = outline.effectDistance,
                OriginalOutlineEnabled = outline.enabled
            };

            _featureHighlightFrames[target] = state;
        }

        if (state.Outline == null)
        {
            return;
        }

        if (isHighlighted && target.activeInHierarchy)
        {
            state.Outline.enabled = true;
            state.Outline.effectColor = NewFeatureHighlightColor;
            state.Outline.effectDistance = new Vector2(3f, -3f);
            return;
        }

        state.Outline.effectColor = state.OriginalOutlineColor;
        state.Outline.effectDistance = state.OriginalOutlineDistance;
        state.Outline.enabled = state.OriginalOutlineEnabled;

        if (state.Graphic != null)
        {
            state.Graphic.color = state.OriginalGraphicColor;
            state.Graphic.raycastTarget = state.OriginalGraphicRaycastTarget;
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
            UpdateExpenseSectionHeight(definitions);
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
            rowLayout.padding = new RectOffset(18, 18, 16, 16);
            rowLayout.spacing = 14f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            AddLayoutElement(row.gameObject, preferredHeight: 92f);

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
            requiredLabel.color = RuntimeUiFactory.TextPrimaryColor;
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
                Root = row.gameObject,
                TitleLabel = title,
                MetaLabel = meta,
                RequiredBadge = requiredBadge.gameObject,
                RequiredBadgeLabel = requiredLabel,
                AmountInput = amountField,
                ApplyRequiredAmountButton = applyRequiredAmountButton,
                SourceDropdown = sourceDropdown
            };
        }

        UpdateExpenseSectionHeight(definitions);
    }

    private void UpdateExpenseSectionHeight(IReadOnlyList<PeriodExpenseDefinition> definitions)
    {
        if (_expenseContent == null)
        {
            return;
        }

        var section = _expenseContent.parent as RectTransform;

        if (section == null)
        {
            return;
        }

        var expenseCount = 0;

        if (definitions != null)
        {
            for (var index = 0; index < definitions.Count; index++)
            {
                if (definitions[index] != null)
                {
                    expenseCount++;
                }
            }
        }

        const float baseSectionHeight = 420f;
        const float headerAndPaddingHeight = 96f;
        const float rowHeight = 92f;
        const float rowSpacing = 10f;

        var preferredHeight = Mathf.Max(
            baseSectionHeight,
            headerAndPaddingHeight + (expenseCount * rowHeight) + (Mathf.Max(0, expenseCount - 1) * rowSpacing));

        var layoutElement = section.GetComponent<LayoutElement>();

        if (layoutElement == null)
        {
            layoutElement = section.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = preferredHeight;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleHeight = 0f;
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
                Root = row.gameObject,
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
            var scrollArea = CreateRow(_assetContent, "AssetScrollArea", 8f, TextAnchor.UpperLeft);
            var scrollAreaLayout = scrollArea.GetComponent<HorizontalLayoutGroup>();
            scrollAreaLayout.childForceExpandWidth = false;
            scrollAreaLayout.childForceExpandHeight = true;
            scrollAreaLayout.childControlHeight = true;
            AddLayoutElement(scrollArea.gameObject, flexibleHeight: 1f, minimumHeight: 356f, preferredHeight: 356f);

            var viewport = CreateRect("AssetViewport", scrollArea);
            AddLayoutElement(viewport.gameObject, flexibleWidth: 1f, flexibleHeight: 1f, minimumHeight: 356f);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.white;
            var viewportMask = viewport.gameObject.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            var scrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;
            var verticalScrollbar = CreateVerticalScrollbar(scrollArea);
            scrollRect.verticalScrollbar = verticalScrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            var content = RuntimeUiFactory.CreateContentRoot(
                "AssetContent",
                viewport,
                new RectOffset(0, 0, 0, 0),
                14f);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = content;

            RectTransform currentRow = null;
            var cardsInRow = 0;
            var rowIndex = 0;

            for (var index = 0; index < assets.Count; index++)
            {
                var asset = assets[index];

                if (asset == null)
                {
                    continue;
                }

                if (currentRow == null || cardsInRow >= 2)
                {
                    if (currentRow != null && cardsInRow == 1)
                    {
                        RuntimeUiFactory.AddFlexibleSpacer(currentRow);
                    }

                    currentRow = CreateAssetsRow(content, $"AssetCardsRow_{rowIndex}");
                    rowIndex++;
                    cardsInRow = 0;
                }

                CreateAssetCard(currentRow, asset);
                cardsInRow++;
            }

            if (currentRow != null && cardsInRow == 1)
            {
                RuntimeUiFactory.AddFlexibleSpacer(currentRow);
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
        AddLayoutElement(card.gameObject, minimumWidth: 300f, preferredWidth: 300f, preferredHeight: 166f, flexibleWidth: 1f);

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

        AddLayoutElement(depositButton.gameObject, minimumWidth: 126f, preferredWidth: 136f, preferredHeight: 42f, flexibleWidth: 1f);
        AddLayoutElement(withdrawButton.gameObject, minimumWidth: 126f, preferredWidth: 136f, preferredHeight: 42f, flexibleWidth: 1f);

        _assetCards[asset.AssetId] = new AssetCardWidgets
        {
            AssetId = asset.AssetId,
            Root = card.gameObject,
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

    private static double GetExpenseBadgeAmount(
        PeriodRuntimeState runtimeState,
        PeriodExpenseDefinition expenseDefinition)
    {
        if (expenseDefinition == null)
        {
            return 0d;
        }

        if (string.Equals(expenseDefinition.Id, "holiday", StringComparison.Ordinal))
        {
            return Math.Max(0d, expenseDefinition.MaximumAmount);
        }

        if (string.Equals(expenseDefinition.Id, "leisure", StringComparison.Ordinal))
        {
            var income = runtimeState != null
                         && runtimeState.HasDefinition
                         && runtimeState.Definition.CalculationSettings != null
                ? Math.Max(0d, runtimeState.Definition.CalculationSettings.CurrentIncomeEcu)
                : 0d;
            return income > 0d
                ? income * 0.05d
                : ConsumerCreditMath.DefaultBaseIncomeEcu * 0.05d;
        }

        return Math.Max(0d, expenseDefinition.MinimumAmount);
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
        scrollRect = null;
        _expenseScrollRect = null;
        _expenseTopFade = null;
        _expenseBottomFade = null;
        _expenseHintLabel = null;
        return CreateSection(parent, title);
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

    private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
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

    private static void DisableContentSizeFitter(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        var fitter = rect.GetComponent<ContentSizeFitter>();

        if (fitter != null)
        {
            fitter.enabled = false;
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
            ? string.Equals(definition.Id, "goods_services", StringComparison.Ordinal)
                ? definition.MinimumAmount * 0.25d
                : definition.MinimumAmount
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
