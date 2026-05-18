using System;
using System.Collections.Generic;
using Game.Core.Application.Periods;
using Game.Core.Application.Session;
using UnityEngine;
using UnityEngine.UI;

[PopupDefinition(Enums.PopupType.InterPeriodBlock)]
public sealed class InterPeriodBlockPopup : Popup
{
    private sealed class NewsEntry
    {
        public string Title;
        public string Text;
    }

    private Text _titleLabel;
    private Text _subtitleLabel;
    private RectTransform _scrollContent;
    private Text _emptyLabel;
    private Button _primaryButton;
    private bool _isBuilt;
    private bool _completionHandled;

    public override Enums.PopupType PopupType => Enums.PopupType.InterPeriodBlock;

    protected override void OnInitialize()
    {
        EnsureBuilt();
        BindButton(_primaryButton, HandleContinue);
        ApplyBlock(ResolveBlock());
    }

    public override void Close(Action callback = null)
    {
        TryCompleteOnDismiss();
        base.Close(callback);
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        var background = CreateRect("Backdrop", transform);
        Stretch(background);
        var backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.11f, 0.13f, 0.2f, 0.62f);

        var card = RuntimeUiFactory.CreateCard("InterPeriodCard", transform, new Vector2(940f, 760f), false);
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(28, 28, 24, 24),
            12f);

        _titleLabel = RuntimeUiFactory.CreateTitle(content, "Сообщение периода", TextAnchor.MiddleLeft);
        _subtitleLabel = RuntimeUiFactory.CreateCaption(content, string.Empty);
        _subtitleLabel.gameObject.SetActive(false);

        var scrollArea = RuntimeUiFactory.CreateRow("ScrollArea", content, 10f, TextAnchor.UpperLeft);
        var scrollAreaLayout = scrollArea.GetComponent<HorizontalLayoutGroup>();
        scrollAreaLayout.childForceExpandWidth = false;
        scrollAreaLayout.childForceExpandHeight = true;
        var scrollAreaElement = scrollArea.gameObject.AddComponent<LayoutElement>();
        scrollAreaElement.flexibleHeight = 1f;
        scrollAreaElement.minHeight = 520f;

        var viewport = CreateRect("Viewport", scrollArea);
        var viewportLayout = viewport.gameObject.AddComponent<LayoutElement>();
        viewportLayout.flexibleWidth = 1f;
        viewportLayout.flexibleHeight = 1f;
        viewportLayout.minHeight = 520f;
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.white;
        var viewportMask = viewport.gameObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        var scrollRect = content.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        _scrollContent = RuntimeUiFactory.CreateContentRoot(
            "ScrollContent",
            viewport,
            new RectOffset(0, 0, 0, 0),
            10f);
        _scrollContent.anchorMin = new Vector2(0f, 1f);
        _scrollContent.anchorMax = new Vector2(1f, 1f);
        _scrollContent.pivot = new Vector2(0.5f, 1f);
        _scrollContent.anchoredPosition = Vector2.zero;
        _scrollContent.sizeDelta = Vector2.zero;
        var fitter = _scrollContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = _scrollContent;

        var scrollbar = CreateVerticalScrollbar(scrollArea);
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        _emptyLabel = RuntimeUiFactory.CreateBodyText(_scrollContent, string.Empty);
        _emptyLabel.gameObject.SetActive(false);

        var buttonRow = RuntimeUiFactory.CreateRow("Buttons", content, 12f, TextAnchor.MiddleCenter);
        _primaryButton = RuntimeUiFactory.CreatePrimaryButton(buttonRow, "Понятно");
    }

    private void HandleContinue()
    {
        if (_completionHandled)
        {
            return;
        }

        _completionHandled = true;
        Context.Popups?.Pop("inter_period_block_continue");
        Context.SessionFlow?.CompleteActiveStep();
    }

    private void TryCompleteOnDismiss()
    {
        if (_completionHandled || Context == null || Context.SessionFlow == null)
        {
            return;
        }

        var currentFlow = Context.SessionFlow.Current;
        var activeStep = currentFlow != null && currentFlow.Progress != null
            ? currentFlow.Progress.ActiveStep
            : SessionFlowStepDescriptor.Empty;

        if (!activeStep.IsDefined
            || activeStep.Scope != SessionFlowStepScope.InterPeriodBlock
            || !string.Equals(activeStep.Key, Route != null ? Route.Source : string.Empty, StringComparison.Ordinal))
        {
            return;
        }

        _completionHandled = true;
        Context.SessionFlow.CompleteActiveStep();
    }

    private void ApplyBlock(InterPeriodBlockRuntime block)
    {
        ClearScrollContent();

        if (block == null || string.IsNullOrWhiteSpace(block.Id))
        {
            _titleLabel.text = "Сообщение периода";
            _subtitleLabel.gameObject.SetActive(false);
            _emptyLabel.text = "Блок периода не найден.";
            _emptyLabel.gameObject.SetActive(true);
            return;
        }

        var normalizedType = NormalizeType(block);
        _titleLabel.text = ResolveBlockTitle(block);
        _subtitleLabel.text = string.Empty;
        _subtitleLabel.gameObject.SetActive(false);
        RuntimeUiFactory.SetButtonText(
            _primaryButton,
            normalizedType == "news"
                ? "К периоду"
                : "Понятно");

        if (normalizedType == "news")
        {
            BuildNewsLayout(block);
            return;
        }

        BuildInstructionLayout(block);
    }

    private void BuildInstructionLayout(InterPeriodBlockRuntime block)
    {
        var body = ResolveBody(block);

        if (string.IsNullOrWhiteSpace(body))
        {
            _emptyLabel.text = "Сообщение отсутствует.";
            _emptyLabel.gameObject.SetActive(true);
            return;
        }

        var panel = RuntimeUiFactory.CreatePanel(
            "InstructionPanel",
            _scrollContent,
            new RectOffset(22, 22, 20, 20),
            12f,
            RuntimeUiFactory.SurfaceColor);

        var text = RuntimeUiFactory.CreateBodyText(panel, body, TextAnchor.UpperLeft);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
    }

    private void BuildNewsLayout(InterPeriodBlockRuntime block)
    {
        var masthead = RuntimeUiFactory.CreatePanel(
            "Masthead",
            _scrollContent,
            new RectOffset(20, 20, 16, 16),
            6f,
            new Color(0.95f, 0.93f, 0.86f, 1f));
        var mastheadTitle = RuntimeUiFactory.CreateTitle(masthead, _titleLabel.text, TextAnchor.MiddleCenter);
        mastheadTitle.color = RuntimeUiFactory.TextPrimaryColor;
        mastheadTitle.fontSize = 28;

        var articles = ExtractNewsEntries(block);

        if (articles.Count == 0)
        {
            _emptyLabel.text = "Новостные материалы отсутствуют.";
            _emptyLabel.gameObject.SetActive(true);
            return;
        }

        for (var index = 0; index < articles.Count; index++)
        {
            var article = articles[index];
            var articlePanel = RuntimeUiFactory.CreatePanel(
                $"NewsArticle_{index + 1}",
                _scrollContent,
                new RectOffset(18, 18, 16, 16),
                10f,
                new Color(0.97f, 0.95f, 0.89f, 1f));

            var imagePlaceholder = CreateRect("Photo", articlePanel);
            var imageLayout = imagePlaceholder.gameObject.AddComponent<LayoutElement>();
            imageLayout.preferredHeight = 90f + (index % 3) * 34f;
            var image = imagePlaceholder.gameObject.AddComponent<Image>();
            image.color = new Color(0.82f, 0.8f, 0.74f, 1f);

            if (!string.IsNullOrWhiteSpace(article.Title))
            {
                var articleTitle = RuntimeUiFactory.CreateBodyText(articlePanel, article.Title, TextAnchor.UpperLeft);
                RuntimeUiFactory.ApplyTextStyle(articleTitle, FontStyle.Bold);
            }

            var articleText = RuntimeUiFactory.CreateBodyText(
                articlePanel,
                string.IsNullOrWhiteSpace(article.Text) ? "..." : article.Text,
                TextAnchor.UpperLeft);
            articleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            articleText.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }

    private List<NewsEntry> ExtractNewsEntries(InterPeriodBlockRuntime block)
    {
        var result = new List<NewsEntry>();
        var rawNode = block != null ? block.RawNode : JsonValue.Null;
        var payload = GetObjectCandidate(rawNode, "payload", "content", "data");
        var itemsNode = FindFirstProperty(payload, "texts", "items", "articles", "blocks", "paragraphs");

        if (itemsNode.Kind == JsonValueKind.Array)
        {
            for (var index = 0; index < itemsNode.ArrayValue.Count; index++)
            {
                var item = itemsNode.ArrayValue[index];

                if (item == null || item.Kind == JsonValueKind.Null)
                {
                    continue;
                }

                if (item.Kind == JsonValueKind.String)
                {
                    var value = GetStringOrDefault(item).Trim();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Add(new NewsEntry { Title = string.Empty, Text = value });
                    }

                    continue;
                }

                if (item.Kind != JsonValueKind.Object)
                {
                    continue;
                }

                var title = ReadString(item, "title", "label", "heading", "name");
                var text = ReadString(item, "text", "body", "content", "description", "message");

                if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(text))
                {
                    result.Add(new NewsEntry { Title = title, Text = text });
                }
            }
        }

        if (result.Count > 0)
        {
            return result;
        }

        var fallbackBody = ResolveBody(block);
        var segments = fallbackBody.Split(
            new string[] { "\r\n\r\n", "\n\n" },
            StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < segments.Length; index++)
        {
            var segment = (segments[index] ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(segment))
            {
                result.Add(new NewsEntry { Title = string.Empty, Text = segment });
            }
        }

        return result;
    }

    private InterPeriodBlockRuntime ResolveBlock()
    {
        var currentFlow = Context != null && Context.SessionFlow != null
            ? Context.SessionFlow.Current
            : SessionFlowRuntimeState.Empty;
        var config = currentFlow != null ? currentFlow.Config : SessionConfigRuntime.Empty;

        if (config == null || !config.IsValid)
        {
            return InterPeriodBlockRuntime.Empty;
        }

        var targetKey = Route != null ? Route.Source : string.Empty;

        if (!string.IsNullOrWhiteSpace(targetKey))
        {
            for (var periodIndex = 0; periodIndex < config.Periods.Count; periodIndex++)
            {
                var period = config.Periods[periodIndex];

                if (period == null || period.InterPeriodBlocks == null)
                {
                    continue;
                }

                for (var blockIndex = 0; blockIndex < period.InterPeriodBlocks.Count; blockIndex++)
                {
                    var block = period.InterPeriodBlocks[blockIndex];

                    if (block != null && string.Equals(block.Id, targetKey, StringComparison.Ordinal))
                    {
                        return block;
                    }
                }
            }
        }

        var activeStep = currentFlow != null && currentFlow.Progress != null
            ? currentFlow.Progress.ActiveStep
            : SessionFlowStepDescriptor.Empty;

        if (!activeStep.IsDefined || activeStep.Scope != SessionFlowStepScope.InterPeriodBlock)
        {
            return InterPeriodBlockRuntime.Empty;
        }

        if (!config.TryGetPeriod(activeStep.PeriodNumber, out var activePeriod)
            || activePeriod == null
            || activePeriod.InterPeriodBlocks == null)
        {
            return InterPeriodBlockRuntime.Empty;
        }

        for (var index = 0; index < activePeriod.InterPeriodBlocks.Count; index++)
        {
            var block = activePeriod.InterPeriodBlocks[index];

            if (block != null && string.Equals(block.Id, activeStep.Key, StringComparison.Ordinal))
            {
                return block;
            }
        }

        return InterPeriodBlockRuntime.Empty;
    }

    private static string ResolveBlockTitle(InterPeriodBlockRuntime block)
    {
        if (block == null)
        {
            return "Сообщение периода";
        }

        if (!string.IsNullOrWhiteSpace(block.Title))
        {
            return block.Title;
        }

        var rawNode = block.RawNode;
        var contentNode = GetObjectCandidate(rawNode, "contentBlock", "content", "block");
        var title = ReadString(contentNode, "title", "label", "heading", "name");

        if (!string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        return NormalizeType(block) == "news"
            ? "Новости периода"
            : "Сообщение периода";
    }

    private static string ResolveBody(InterPeriodBlockRuntime block)
    {
        if (block == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(block.Body))
        {
            return block.Body;
        }

        var rawNode = block.RawNode;
        var contentNode = GetObjectCandidate(rawNode, "contentBlock", "content", "block");
        var direct = ReadString(rawNode, "body", "text", "content", "description", "message", "markdown");

        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        if (TryGetPropertyIgnoreCase(rawNode, "payload", out var payloadNode)
            && payloadNode.Kind == JsonValueKind.Object)
        {
            var payloadText = ReadString(payloadNode, "text", "body", "content", "description", "message");

            if (!string.IsNullOrWhiteSpace(payloadText))
            {
                return payloadText;
            }
        }

        return ReadString(contentNode, "body", "text", "content", "description", "message", "markdown");
    }

    private static string NormalizeType(InterPeriodBlockRuntime block)
    {
        if (block == null)
        {
            return string.Empty;
        }

        switch (block.Type)
        {
            case SessionFlowStepType.News:
                return "news";
            case SessionFlowStepType.InstructionalPopup:
                return "instructionalpopup";
        }

        var rawType = ReadString(block.RawNode, "type", "kind", "contentType");
        return Normalize(rawType);
    }

    private void ClearScrollContent()
    {
        if (_scrollContent == null)
        {
            return;
        }

        for (var index = _scrollContent.childCount - 1; index >= 0; index--)
        {
            Destroy(_scrollContent.GetChild(index).gameObject);
        }

        _emptyLabel = RuntimeUiFactory.CreateBodyText(_scrollContent, string.Empty);
        _emptyLabel.gameObject.SetActive(false);
    }

    private static string ReadString(JsonValue node, params string[] names)
    {
        if (node == null || names == null)
        {
            return string.Empty;
        }

        for (var index = 0; index < names.Length; index++)
        {
            if (TryGetPropertyIgnoreCase(node, names[index], out var value))
            {
                return GetStringOrDefault(value);
            }
        }

        return string.Empty;
    }

    private static JsonValue GetObjectCandidate(JsonValue node, params string[] names)
    {
        var candidate = FindFirstProperty(node, names);
        return candidate != null && candidate.Kind == JsonValueKind.Object
            ? candidate
            : JsonValue.Null;
    }

    private static JsonValue FindFirstProperty(JsonValue node, params string[] names)
    {
        if (names == null)
        {
            return JsonValue.Null;
        }

        for (var index = 0; index < names.Length; index++)
        {
            if (TryGetPropertyIgnoreCase(node, names[index], out var value))
            {
                return value;
            }
        }

        return JsonValue.Null;
    }

    private static string GetStringOrDefault(JsonValue node, string fallback = "")
    {
        if (node == null)
        {
            return fallback ?? string.Empty;
        }

        switch (node.Kind)
        {
            case JsonValueKind.String:
                return node.StringValue ?? string.Empty;
            case JsonValueKind.Number:
                return node.NumberValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case JsonValueKind.Boolean:
                return node.BooleanValue ? "true" : "false";
            default:
                return fallback ?? string.Empty;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonValue node, string propertyName, out JsonValue value)
    {
        value = JsonValue.Null;

        if (node == null || node.Kind != JsonValueKind.Object || string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        if (node.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var pair in node.ObjectValue)
        {
            if (string.Equals(pair.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value ?? JsonValue.Null;
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
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

    private static Scrollbar CreateVerticalScrollbar(Transform parent)
    {
        var root = CreateRect("VerticalScrollbar", parent);
        var layout = root.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 8f;
        layout.minWidth = 8f;
        layout.flexibleHeight = 1f;

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

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
