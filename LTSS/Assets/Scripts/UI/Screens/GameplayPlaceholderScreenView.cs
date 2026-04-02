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
        public Button SourceButton;
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
    private Text _incomeValueLabel;
    private Text _remainingValueLabel;
    private Text _validationLabel;
    private Text _statusTextLabel;
    private Text _emptyStateLabel;
    private Button _backButton;
    private Button _completeButton;
    private RectTransform _expenseContent;
    private RectTransform _assetContent;
    private RectTransform _infoContent;
    private RectTransform _mainLayout;
    private RectTransform _footerRow;
    private readonly Dictionary<string, ExpenseRowWidgets> _expenseRows = new Dictionary<string, ExpenseRowWidgets>();
    private readonly Dictionary<string, AssetCardWidgets> _assetCards = new Dictionary<string, AssetCardWidgets>();
    private readonly Dictionary<string, InfoRowWidgets> _infoRows = new Dictionary<string, InfoRowWidgets>();
    private bool _isBuilt;

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
        Action<string> onExpenseSourceToggle,
        Action<string, AssetOperationKind> onAssetAction)
    {
        EnsureBuilt();
        BindButton(_backButton, onBack);
        BindButton(_completeButton, onComplete);

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
        ApplyExpenses(runtimeState, onExpenseAmountChanged, onExpenseSourceToggle);
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

        _expenseContent = CreateSection(leftColumn, "Расходы");
        AddLayoutElement(_expenseContent.parent.gameObject, flexibleHeight: 1f);

        _assetContent = CreateSection(leftColumn, "Активы");
        _infoContent = CreateSection(rightColumn, "Параметры периода");
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

        var subtitle = $"Период {meta.PeriodNumber}";

        if (!string.IsNullOrWhiteSpace(meta.HistoricalLabel))
        {
            subtitle += $"  |  {meta.HistoricalLabel}";
        }

        _periodSubtitleLabel.text = subtitle;
        _phaseLabel.text = FormatFlow(runtimeState.FlowState);
    }

    private void ApplyMetrics(PeriodRuntimeState runtimeState)
    {
        var summary = runtimeState.Summary ?? PeriodCalculationSummary.Empty;

        _ujeValueLabel.text = summary.Uje.ToString("0.0", CultureInfo.InvariantCulture);
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
            widgets.Value.text = infoValue.HasValue ? infoValue.DisplayValue : "—";
            widgets.Value.color = infoValue.HasValue
                ? RuntimeUiFactory.TextPrimaryColor
                : RuntimeUiFactory.TextSecondaryColor;
        }
    }

    private void ApplyExpenses(
        PeriodRuntimeState runtimeState,
        Action<string, string> onExpenseAmountChanged,
        Action<string> onExpenseSourceToggle)
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

            if (widgets.RequiredBadge != null)
            {
                var showRequiredBadge = expenseDefinition.IsRequired && expenseDefinition.MinimumAmount > 0d;
                widgets.RequiredBadge.SetActive(showRequiredBadge);

                if (showRequiredBadge && widgets.RequiredBadgeLabel != null)
                {
                    widgets.RequiredBadgeLabel.text = $"мин. {FormatMoney(expenseDefinition.MinimumAmount)}";
                }
            }

            var amountText = expenseState != null && expenseState.Amount > 0d
                ? expenseState.Amount.ToString("0.##", CultureInfo.InvariantCulture)
                : string.Empty;

            widgets.AmountInput.onValueChanged.RemoveAllListeners();
            SyncInputFieldText(widgets.AmountInput, amountText);

            widgets.AmountInput.onValueChanged.AddListener(value => onExpenseAmountChanged?.Invoke(expenseDefinition.Id, value));
            widgets.AmountInput.interactable = canEdit;

            RuntimeUiFactory.SetButtonText(
                widgets.SourceButton,
                PeriodContractMapper.ToFundsSourceLabel(expenseState != null
                    ? expenseState.Source
                    : FundsSourceType.CurrentIncome));
            BindButton(widgets.SourceButton, () => onExpenseSourceToggle?.Invoke(expenseDefinition.Id));
            widgets.SourceButton.interactable = canEdit && expenseDefinition.AllowedSources != null && expenseDefinition.AllowedSources.Count > 1;
        }
    }

    private void ApplyAssets(
        PeriodRuntimeState runtimeState,
        Action<string, AssetOperationKind> onAssetAction)
    {
        var canEdit = runtimeState.FlowState != PeriodFlowState.PeriodCheckpointSubmitting
            && runtimeState.FlowState != PeriodFlowState.PeriodClosed
            && runtimeState.FlowState != PeriodFlowState.LoadingData
            && runtimeState.FlowState != PeriodFlowState.PeriodClosing;
        RebuildAssetCards(runtimeState.Summary.AssetBalances);

        foreach (var asset in runtimeState.Summary.AssetBalances)
        {
            if (asset == null || !_assetCards.TryGetValue(asset.AssetId, out var widgets))
            {
                continue;
            }

            widgets.TitleLabel.text = asset.Title;
            widgets.ValueLabel.text = FormatMoney(asset.CurrentAmount);
            widgets.CaptionLabel.text = $"На начало периода: {FormatMoney(asset.InitialAmount)}";

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
            var requiredLabel = RuntimeUiFactory.CreateCaption(requiredBadge, string.Empty, TextAnchor.MiddleCenter);
            requiredLabel.fontSize = 12;
            requiredLabel.color = RuntimeUiFactory.PrimaryColor;
            requiredBadge.gameObject.SetActive(false);

            var amountField = RuntimeUiFactory.CreateInputField(row, "0");
            amountField.contentType = InputField.ContentType.DecimalNumber;
            AddLayoutElement(amountField.gameObject, minimumHeight: 25f, preferredWidth: 160f);

            var sourceButton = RuntimeUiFactory.CreateSecondaryButton(row, "Источник", 46f);
            AddLayoutElement(sourceButton.gameObject, preferredWidth: 180f);

            _expenseRows[definition.Id] = new ExpenseRowWidgets
            {
                ExpenseId = definition.Id,
                TitleLabel = title,
                MetaLabel = meta,
                RequiredBadge = requiredBadge.gameObject,
                RequiredBadgeLabel = requiredLabel,
                AmountInput = amountField,
                SourceButton = sourceButton
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

    private void SetEmptyState(string message)
    {
        _mainLayout.gameObject.SetActive(false);
        _footerRow.gameObject.SetActive(false);
        _emptyStateLabel.gameObject.SetActive(true);
        _emptyStateLabel.text = message ?? string.Empty;
        _periodTitleLabel.text = "Период";
        _periodSubtitleLabel.text = string.Empty;
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

    private static Text CreateMetricCard(Transform parent, string label, string value)
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

        RuntimeUiFactory.CreateCaption(card, label);
        return RuntimeUiFactory.CreateValueText(card, value, 34, TextAnchor.MiddleLeft);
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

    private static void SyncInputFieldText(InputField inputField, string value)
    {
        if (inputField == null)
        {
            return;
        }

        var safeValue = value ?? string.Empty;

        if (inputField.text != safeValue)
        {
            inputField.text = safeValue;
        }

        if (inputField.textComponent != null)
        {
            inputField.textComponent.gameObject.SetActive(true);
            inputField.textComponent.text = safeValue;
            inputField.textComponent.color = RuntimeUiFactory.TextPrimaryColor;
            inputField.textComponent.enabled = true;
        }

        if (inputField.placeholder is Graphic placeholderGraphic)
        {
            placeholderGraphic.gameObject.SetActive(true);
            placeholderGraphic.enabled = string.IsNullOrWhiteSpace(safeValue);
        }

        inputField.ForceLabelUpdate();

        var rectTransform = inputField.transform as RectTransform;

        if (rectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }
}
