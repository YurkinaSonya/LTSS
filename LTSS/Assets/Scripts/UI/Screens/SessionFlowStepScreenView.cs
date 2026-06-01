using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Game.Core.Application;
using Game.Core.Application.Session;
using Game.Core.Application.UI;
using UnityEngine;
using UnityEngine.UI;

[ScreenDefinition(ScreenId.FlowStep)]
public sealed class SessionFlowStepScreenView : ScreenView
{
    private sealed class QuestionBinding
    {
        public SessionFlowQuestionRuntime Question;
        public InputField TextInput;
        public Text FeedbackLabel;
        public RectTransform Panel;
        public int PageIndex;
        public readonly List<OptionBinding> Options = new List<OptionBinding>();
    }

    private sealed class OptionBinding
    {
        public SessionFlowQuestionOptionRuntime Option;
        public Toggle Toggle;
        public InputField OtherInput;
        public Text Label;
    }

    private Text _titleLabel;
    private Text _subtitleLabel;
    private Text _bodyLabel;
    private Text _statusLabel;
    private RectTransform _cardRect;
    private RectTransform _bodyPanel;
    private LayoutElement _bodyPanelLayout;
    private ScrollRect _bodyScrollRect;
    private RectTransform _bodyContainer;
    private LayoutElement _bodyScrollAreaLayoutElement;
    private LayoutElement _bodyViewportLayoutElement;
    private RectTransform _questionPanel;
    private ScrollRect _questionScrollRect;
    private RectTransform _questionContainer;
    private RectTransform _paginationRow;
    private Button _previousPageButton;
    private Button _nextPageButton;
    private Text _pageIndicatorLabel;
    private Button _primaryButton;
    private Button _secondaryButton;
    private Button _instructionReferenceButton;
    private RectTransform _instructionPopupOverlay;
    private Text _instructionPopupTitleLabel;
    private Text _instructionPopupNoticeLabel;
    private Text _instructionPopupBodyLabel;
    private ScrollRect _instructionPopupScrollRect;
    private bool _isBuilt;
    private string _lastStepKey = string.Empty;
    private int _currentPageIndex;
    private string _instructionReferenceTitle = string.Empty;
    private string _instructionReferenceBody = string.Empty;
    private readonly List<int> _pageOrder = new List<int>();
    private UIContext _context;
    private readonly Dictionary<string, QuestionBinding> _questionBindings =
        new Dictionary<string, QuestionBinding>();
    private static readonly Regex BoldRegex = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    public override ScreenController Construct(UIContext context)
    {
        _context = context;
        EnsureBuilt();
        return new SessionFlowStepScreenController(this, context);
    }

    public void BindPrimary(Action callback)
    {
        EnsureBuilt();
        BindButton(_primaryButton, callback);
    }

    public void BindSecondary(Action callback)
    {
        EnsureBuilt();
        BindButton(_secondaryButton, callback);
    }

    public void Apply(SessionFlowRuntimeState flowState)
    {
        EnsureBuilt();

        var viewModel = flowState != null
            ? flowState.ActiveStepView
            : SessionFlowStepViewModel.Empty;
        var title = !string.IsNullOrWhiteSpace(viewModel.Title)
            ? viewModel.Title
            : "Шаг сценария";

        var isSurvey = viewModel.RendererKind == SessionFlowRendererKind.Survey;
        ResolveInstructionReference(flowState);

        _titleLabel.text = title;
        _subtitleLabel.text = viewModel.Subtitle ?? string.Empty;
        _subtitleLabel.gameObject.SetActive(!isSurvey && !string.IsNullOrWhiteSpace(_subtitleLabel.text));
        _bodyLabel.text = SimpleMarkdownFormatter.Format(viewModel.Body);
        var hasBody = !string.IsNullOrWhiteSpace(_bodyLabel.text);
        ApplyStepLayoutPreset(isSurvey, hasBody);
        _bodyLabel.gameObject.SetActive(!isSurvey && hasBody);
        if (_bodyPanel != null)
        {
            _bodyPanel.gameObject.SetActive(!isSurvey && hasBody);
        }
        if (_bodyScrollRect != null)
        {
            _bodyScrollRect.verticalNormalizedPosition = 1f;
        }
        _statusLabel.text = !string.IsNullOrWhiteSpace(flowState != null ? flowState.LastError : string.Empty)
            ? flowState.LastError
            : flowState != null
                ? flowState.StatusMessage
                : string.Empty;
        _statusLabel.color = !string.IsNullOrWhiteSpace(flowState != null ? flowState.LastError : string.Empty)
            ? RuntimeUiFactory.DangerColor
            : RuntimeUiFactory.TextSecondaryColor;
        _statusLabel.gameObject.SetActive(!isSurvey && !string.IsNullOrWhiteSpace(_statusLabel.text));

        if (_lastStepKey != viewModel.Descriptor.Key)
        {
            RebuildQuestions(flowState, viewModel.Questions);
            _lastStepKey = viewModel.Descriptor.Key ?? string.Empty;
        }

        RuntimeUiFactory.SetButtonText(
            _primaryButton,
            string.IsNullOrWhiteSpace(viewModel.PrimaryActionText)
                ? "Далее"
                : viewModel.PrimaryActionText);

        var canShowSecondary = viewModel.CanSkip && !string.IsNullOrWhiteSpace(viewModel.SecondaryActionText);
        _secondaryButton.gameObject.SetActive(canShowSecondary);
        _instructionReferenceButton.gameObject.SetActive(
            isSurvey && !string.IsNullOrWhiteSpace(_instructionReferenceBody));

        if (canShowSecondary)
        {
            RuntimeUiFactory.SetButtonText(_secondaryButton, viewModel.SecondaryActionText);
        }

        if (!isSurvey || string.IsNullOrWhiteSpace(_instructionReferenceBody))
        {
            CloseInstructionReferencePopup();
        }

        UpdatePrimaryButtonInteractable(viewModel);
    }

    public IReadOnlyDictionary<string, string> CollectAnswers()
    {
        var result = new Dictionary<string, string>();

        foreach (var pair in _questionBindings)
        {
            result[pair.Key] = CollectQuestionAnswer(pair.Value);
        }

        return result;
    }

    public bool TryPrepareSurveySubmission()
    {
        var answers = CollectAnswers();

        if (HasMissingRequiredAnswers(answers))
        {
            return false;
        }

        if (!IsInstructionQuizActive())
        {
            return true;
        }

        var incorrectAnswersCount = CountIncorrectInstructionQuizAnswers();

        if (incorrectAnswersCount <= 0)
        {
            return true;
        }

        OpenInstructionReferencePopup(
            $"Вы ответили неправильно на {incorrectAnswersCount} {GetQuestionCountWordForm(incorrectAnswersCount)} теста. Перечитайте инструкцию и проверьте ответы еще раз.");
        return false;
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        var background = RuntimeUiFactory.CreateScreenBackground(transform);
        _cardRect = RuntimeUiFactory.CreateCard("FlowCard", background, new Vector2(1120f, 820f));
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            _cardRect,
            new RectOffset(26, 26, 24, 22),
            14f);

        _titleLabel = RuntimeUiFactory.CreateTitle(content, "Шаг сценария", TextAnchor.MiddleLeft);
        _subtitleLabel = RuntimeUiFactory.CreateCaption(content, string.Empty);
        _subtitleLabel.gameObject.SetActive(false);

        _bodyPanel = RuntimeUiFactory.CreatePanel(
            "BodyPanel",
            content,
            new RectOffset(14, 14, 14, 12),
            10f,
            RuntimeUiFactory.SurfaceColor);
        DisableContentSizeFitter(_bodyPanel);
        _bodyPanelLayout = _bodyPanel.gameObject.AddComponent<LayoutElement>();
        _bodyPanelLayout.minHeight = 420f;
        _bodyPanelLayout.preferredHeight = 560f;
        _bodyPanelLayout.flexibleHeight = 1f;

        var bodyScrollArea = RuntimeUiFactory.CreateRow("BodyScrollArea", _bodyPanel, 8f, TextAnchor.UpperLeft);
        var bodyScrollAreaLayout = bodyScrollArea.GetComponent<HorizontalLayoutGroup>();
        bodyScrollAreaLayout.childForceExpandWidth = false;
        bodyScrollAreaLayout.childForceExpandHeight = true;
        bodyScrollAreaLayout.childControlHeight = true;
        _bodyScrollAreaLayoutElement = bodyScrollArea.gameObject.AddComponent<LayoutElement>();
        _bodyScrollAreaLayoutElement.flexibleHeight = 1f;
        _bodyScrollAreaLayoutElement.minHeight = 390f;

        var bodyViewport = CreateRect("BodyViewport", bodyScrollArea);
        _bodyViewportLayoutElement = bodyViewport.gameObject.AddComponent<LayoutElement>();
        _bodyViewportLayoutElement.flexibleWidth = 1f;
        _bodyViewportLayoutElement.flexibleHeight = 1f;
        _bodyViewportLayoutElement.minHeight = 390f;
        var bodyViewportImage = bodyViewport.gameObject.AddComponent<Image>();
        bodyViewportImage.color = Color.white;
        var bodyViewportMask = bodyViewport.gameObject.AddComponent<Mask>();
        bodyViewportMask.showMaskGraphic = false;

        _bodyScrollRect = _bodyPanel.gameObject.AddComponent<ScrollRect>();
        _bodyScrollRect.viewport = bodyViewport;
        _bodyScrollRect.horizontal = false;
        _bodyScrollRect.vertical = true;
        _bodyScrollRect.movementType = ScrollRect.MovementType.Clamped;
        _bodyScrollRect.scrollSensitivity = 24f;
        var bodyScrollbar = CreateVerticalScrollbar(bodyScrollArea);
        _bodyScrollRect.verticalScrollbar = bodyScrollbar;
        _bodyScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        _bodyContainer = RuntimeUiFactory.CreateContentRoot(
            "BodyContent",
            bodyViewport,
            new RectOffset(0, 0, 0, 0),
            8f);
        _bodyContainer.anchorMin = new Vector2(0f, 1f);
        _bodyContainer.anchorMax = new Vector2(1f, 1f);
        _bodyContainer.pivot = new Vector2(0.5f, 1f);
        _bodyContainer.anchoredPosition = Vector2.zero;
        _bodyContainer.sizeDelta = new Vector2(0f, 0f);
        var bodyContentFitter = _bodyContainer.gameObject.AddComponent<ContentSizeFitter>();
        bodyContentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bodyContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _bodyScrollRect.content = _bodyContainer;

        _bodyLabel = RuntimeUiFactory.CreateBodyText(_bodyContainer, string.Empty);
        _bodyLabel.alignment = TextAnchor.UpperLeft;
        _bodyLabel.supportRichText = true;
        _bodyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bodyLabel.verticalOverflow = VerticalWrapMode.Overflow;

        _questionPanel = RuntimeUiFactory.CreatePanel(
            "QuestionPanel",
            content,
            new RectOffset(12, 12, 12, 12),
            8f,
            RuntimeUiFactory.PrimarySoftColor);
        DisableContentSizeFitter(_questionPanel);
        var questionPanelLayout = _questionPanel.gameObject.AddComponent<LayoutElement>();
        questionPanelLayout.minHeight = 340f;
        questionPanelLayout.preferredHeight = 420f;
        questionPanelLayout.flexibleHeight = 1f;
        _questionPanel.gameObject.SetActive(false);

        var scrollArea = RuntimeUiFactory.CreateRow("QuestionScrollArea", _questionPanel, 8f, TextAnchor.UpperLeft);
        var scrollAreaLayout = scrollArea.GetComponent<HorizontalLayoutGroup>();
        scrollAreaLayout.childForceExpandWidth = false;
        scrollAreaLayout.childForceExpandHeight = true;
        scrollAreaLayout.childControlHeight = true;
        var scrollAreaElement = scrollArea.gameObject.AddComponent<LayoutElement>();
        scrollAreaElement.flexibleHeight = 1f;
        scrollAreaElement.minHeight = 280f;

        var viewport = CreateRect("QuestionViewport", scrollArea);
        var viewportLayout = viewport.gameObject.AddComponent<LayoutElement>();
        viewportLayout.flexibleWidth = 1f;
        viewportLayout.flexibleHeight = 1f;
        viewportLayout.minHeight = 280f;
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = Color.white;
        var viewportMask = viewport.gameObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        _questionScrollRect = _questionPanel.gameObject.AddComponent<ScrollRect>();
        _questionScrollRect.viewport = viewport;
        _questionScrollRect.horizontal = false;
        _questionScrollRect.vertical = true;
        _questionScrollRect.movementType = ScrollRect.MovementType.Clamped;
        _questionScrollRect.scrollSensitivity = 24f;
        var verticalScrollbar = CreateVerticalScrollbar(scrollArea);
        _questionScrollRect.verticalScrollbar = verticalScrollbar;
        _questionScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        _questionContainer = RuntimeUiFactory.CreateContentRoot(
            "QuestionContent",
            viewport,
            new RectOffset(0, 0, 0, 0),
            8f);
        _questionContainer.anchorMin = new Vector2(0f, 1f);
        _questionContainer.anchorMax = new Vector2(1f, 1f);
        _questionContainer.pivot = new Vector2(0.5f, 1f);
        _questionContainer.anchoredPosition = Vector2.zero;
        _questionContainer.sizeDelta = new Vector2(0f, 0f);
        var questionContentFitter = _questionContainer.gameObject.AddComponent<ContentSizeFitter>();
        questionContentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        questionContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _questionScrollRect.content = _questionContainer;

        _paginationRow = RuntimeUiFactory.CreateRow("PageNavigation", _questionPanel, 8f, TextAnchor.MiddleCenter);
        var paginationLayout = _paginationRow.GetComponent<HorizontalLayoutGroup>();
        paginationLayout.childForceExpandWidth = false;
        paginationLayout.childControlWidth = true;
        _previousPageButton = RuntimeUiFactory.CreateSecondaryButton(_paginationRow, "←", 42f);
        _previousPageButton.onClick.AddListener(GoToPreviousPage);
        _pageIndicatorLabel = RuntimeUiFactory.CreateCaption(_paginationRow, string.Empty, TextAnchor.MiddleCenter);
        var pageLabelLayout = _pageIndicatorLabel.gameObject.AddComponent<LayoutElement>();
        pageLabelLayout.minWidth = 84f;
        _nextPageButton = RuntimeUiFactory.CreateSecondaryButton(_paginationRow, "→", 42f);
        _nextPageButton.onClick.AddListener(GoToNextPage);
        _paginationRow.gameObject.SetActive(false);

        _statusLabel = RuntimeUiFactory.CreateCaption(content, string.Empty);
        _statusLabel.gameObject.SetActive(false);

        var buttonRow = RuntimeUiFactory.CreateRow("Buttons", content, 12f, TextAnchor.MiddleCenter);
        _instructionReferenceButton = RuntimeUiFactory.CreateSecondaryButton(buttonRow, "Открыть инструкцию");
        var instructionButtonLayout = _instructionReferenceButton.gameObject.GetComponent<LayoutElement>();

        if (instructionButtonLayout != null)
        {
            instructionButtonLayout.flexibleWidth = 0f;
            instructionButtonLayout.preferredWidth = 240f;
        }

        _instructionReferenceButton.onClick.AddListener(OpenInstructionReferencePopup);
        _instructionReferenceButton.gameObject.SetActive(false);
        _secondaryButton = RuntimeUiFactory.CreateSecondaryButton(buttonRow, "Пропустить");
        _primaryButton = RuntimeUiFactory.CreatePrimaryButton(buttonRow, "Далее");
        _secondaryButton.gameObject.SetActive(false);
        BuildInstructionReferencePopup(background);
    }

    private void RebuildQuestions(SessionFlowRuntimeState flowState, IReadOnlyList<SessionFlowQuestionRuntime> questions)
    {
        _questionBindings.Clear();
        _pageOrder.Clear();
        _currentPageIndex = 0;

        if (_questionContainer == null)
        {
            return;
        }

        for (var index = _questionContainer.childCount - 1; index >= 0; index--)
        {
            var child = _questionContainer.GetChild(index);
            Destroy(child.gameObject);
        }

        if (questions == null || questions.Count == 0)
        {
            if (_questionPanel != null)
            {
                _questionPanel.gameObject.SetActive(false);
            }
            return;
        }

        if (_questionPanel != null)
        {
            _questionPanel.gameObject.SetActive(true);
        }

        var pageIndices = ResolveQuestionPageIndices(flowState, questions);

        for (var index = 0; index < questions.Count; index++)
        {
            var question = questions[index];

            if (question == null)
            {
                continue;
            }

            var binding = new QuestionBinding
            {
                Question = question,
                PageIndex = pageIndices.TryGetValue(question.Id, out var pageIndex)
                    ? pageIndex
                    : 0
            };

            var questionPanel = RuntimeUiFactory.CreatePanel(
                $"Question_{index + 1}",
                _questionContainer,
                new RectOffset(14, 14, 14, 14),
                8f,
                RuntimeUiFactory.SurfaceColor);
            binding.Panel = questionPanel;

            BuildQuestionHeader(questionPanel, question);
            BuildQuestionInput(questionPanel, binding, question);
            binding.FeedbackLabel = RuntimeUiFactory.CreateCaption(questionPanel, string.Empty);
            binding.FeedbackLabel.gameObject.SetActive(false);
            _questionBindings[question.Id] = binding;
        }

        BuildPageOrder();
        RefreshPageVisibility();
    }

    private static void BuildQuestionHeader(Transform parent, SessionFlowQuestionRuntime question)
    {
        var titleLabel = RuntimeUiFactory.CreateBodyText(parent, question.Label);
        RuntimeUiFactory.ApplyTextStyle(titleLabel, FontStyle.Bold);

        if (!string.IsNullOrWhiteSpace(question.Description))
        {
            RuntimeUiFactory.CreateCaption(parent, question.Description);
        }
    }

    private void BuildQuestionInput(
        Transform parent,
        QuestionBinding binding,
        SessionFlowQuestionRuntime question)
    {
        if (question.Type != SessionFlowQuestionType.Text
            && question.Options != null
            && question.Options.Count > 0)
        {
            switch (question.Type)
            {
                case SessionFlowQuestionType.SingleChoice:
                    BuildChoiceOptions(parent, binding, question, false);
                    return;
                case SessionFlowQuestionType.MultipleChoice:
                    BuildChoiceOptions(parent, binding, question, true);
                    return;
                case SessionFlowQuestionType.Scale:
                    BuildScaleOptions(parent, binding, question);
                    return;
            }
        }

        binding.TextInput = RuntimeUiFactory.CreateInputField(
            parent,
            string.IsNullOrWhiteSpace(question.Placeholder)
                ? "Введите ответ"
                : question.Placeholder);
        if (IsAgeQuestion(question))
        {
            binding.TextInput.contentType = InputField.ContentType.IntegerNumber;
            binding.TextInput.lineType = InputField.LineType.SingleLine;
        }

        binding.TextInput.onValueChanged.AddListener(_ => OnSurveyAnswerChanged());
    }

    private static bool IsAgeQuestion(SessionFlowQuestionRuntime question)
    {
        return question != null
               && string.Equals((question.Label ?? string.Empty).Trim(), "Возраст", StringComparison.OrdinalIgnoreCase);
    }

    private void BuildChoiceOptions(
        Transform parent,
        QuestionBinding binding,
        SessionFlowQuestionRuntime question,
        bool allowMultiple)
    {
        var optionsRoot = RuntimeUiFactory.CreateContentRoot(
            "Options",
            parent,
            new RectOffset(0, 0, 0, 0),
            6f);
        var toggleGroup = allowMultiple
            ? null
            : optionsRoot.gameObject.AddComponent<ToggleGroup>();

        for (var index = 0; index < question.Options.Count; index++)
        {
            var option = question.Options[index];

            if (option == null)
            {
                continue;
            }

            var optionBinding = new OptionBinding
            {
                Option = option
            };

            var row = CreateLayoutRoot(
                $"Option_{index + 1}",
                optionsRoot,
                true,
                10f,
                TextAnchor.MiddleLeft);
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childForceExpandWidth = false;

            var rowBackground = row.gameObject.AddComponent<Image>();
            rowBackground.color = new Color(1f, 1f, 1f, 0.001f);

            CreateSelectionBox(row, !allowMultiple, out var checkmark);
            optionBinding.Toggle = ConfigureToggle(row.gameObject, rowBackground, checkmark, toggleGroup, allowMultiple);

            var label = RuntimeUiFactory.CreateBodyText(row, option.Label);
            label.alignment = TextAnchor.MiddleLeft;
            optionBinding.Label = label;
            var labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;

            if (option.IsOther)
            {
                optionBinding.OtherInput = RuntimeUiFactory.CreateInputField(optionsRoot, "Укажите свой вариант");
                optionBinding.OtherInput.gameObject.SetActive(false);
            }

            binding.Options.Add(optionBinding);
            optionBinding.Toggle.onValueChanged.AddListener(_ => HandleQuestionAnswerChanged(binding));

            if (optionBinding.OtherInput != null)
            {
                optionBinding.OtherInput.onValueChanged.AddListener(_ => HandleQuestionAnswerChanged(binding));
            }
        }

        if (toggleGroup != null)
        {
            toggleGroup.allowSwitchOff = true;
            toggleGroup.SetAllTogglesOff();
        }

        RefreshOtherInputs(binding);
    }

    private void BuildScaleOptions(
        Transform parent,
        QuestionBinding binding,
        SessionFlowQuestionRuntime question)
    {
        var optionsRow = RuntimeUiFactory.CreateRow("ScaleOptions", parent, 10f, TextAnchor.UpperCenter);
        var optionsLayout = optionsRow.GetComponent<HorizontalLayoutGroup>();
        optionsLayout.childForceExpandWidth = true;
        optionsLayout.childControlWidth = true;
        var toggleGroup = optionsRow.gameObject.AddComponent<ToggleGroup>();

        for (var index = 0; index < question.Options.Count; index++)
        {
            var option = question.Options[index];

            if (option == null)
            {
                continue;
            }

            var optionBinding = new OptionBinding
            {
                Option = option
            };

            var column = CreateLayoutRoot(
                $"ScaleOption_{index + 1}",
                optionsRow,
                false,
                8f,
                TextAnchor.UpperCenter);
            var columnLayoutElement = column.gameObject.AddComponent<LayoutElement>();
            columnLayoutElement.flexibleWidth = 1f;
            columnLayoutElement.minWidth = 90f;

            var columnBackground = column.gameObject.AddComponent<Image>();
            columnBackground.color = new Color(1f, 1f, 1f, 0.001f);

            var label = RuntimeUiFactory.CreateCaption(column, option.Label, TextAnchor.MiddleCenter);
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            optionBinding.Label = label;

            CreateSelectionBox(column, true, out var checkmark);
            optionBinding.Toggle = ConfigureToggle(column.gameObject, columnBackground, checkmark, toggleGroup, false);

            if (option.IsOther)
            {
                optionBinding.OtherInput = RuntimeUiFactory.CreateInputField(column, "Свой вариант");
                optionBinding.OtherInput.gameObject.SetActive(false);
            }

            binding.Options.Add(optionBinding);
            optionBinding.Toggle.onValueChanged.AddListener(_ => HandleQuestionAnswerChanged(binding));

            if (optionBinding.OtherInput != null)
            {
                optionBinding.OtherInput.onValueChanged.AddListener(_ => HandleQuestionAnswerChanged(binding));
            }
        }

        if (toggleGroup != null)
        {
            toggleGroup.allowSwitchOff = true;
            toggleGroup.SetAllTogglesOff();
        }

        RefreshOtherInputs(binding);
    }

    private static RectTransform CreateLayoutRoot(
        string name,
        Transform parent,
        bool horizontal,
        float spacing,
        TextAnchor alignment)
    {
        var root = CreateRect(name, parent);

        if (horizontal)
        {
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }
        else
        {
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return root;
    }

    private static void CreateSelectionBox(Transform parent, bool useCircleStyle, out Graphic checkmark)
    {
        var box = CreateRect("Box", parent);
        var boxLayout = box.gameObject.AddComponent<LayoutElement>();
        boxLayout.preferredWidth = 24f;
        boxLayout.preferredHeight = 24f;
        boxLayout.minWidth = 24f;
        boxLayout.minHeight = 24f;

        if (useCircleStyle)
        {
            var outlineText = RuntimeUiFactory.CreateBodyText(box, "○", TextAnchor.MiddleCenter);
            outlineText.color = RuntimeUiFactory.BorderColor;
            outlineText.fontSize = 24;
            outlineText.raycastTarget = false;
            var outlineRect = outlineText.rectTransform;
            outlineRect.anchorMin = Vector2.zero;
            outlineRect.anchorMax = Vector2.one;
            outlineRect.offsetMin = Vector2.zero;
            outlineRect.offsetMax = Vector2.zero;

            var markText = RuntimeUiFactory.CreateBodyText(box, "●", TextAnchor.MiddleCenter);
            markText.color = RuntimeUiFactory.PrimaryColor;
            markText.fontSize = 13;
            markText.raycastTarget = false;
            var markRect = markText.rectTransform;
            markRect.anchorMin = Vector2.zero;
            markRect.anchorMax = Vector2.one;
            markRect.offsetMin = Vector2.zero;
            markRect.offsetMax = Vector2.zero;
            checkmark = markText;
            return;
        }

        var boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = RuntimeUiFactory.ElevatedSurfaceColor;
        var outline = box.gameObject.AddComponent<Outline>();
        outline.effectColor = RuntimeUiFactory.BorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        var squareMarkText = RuntimeUiFactory.CreateBodyText(box, "✓", TextAnchor.MiddleCenter);
        squareMarkText.color = RuntimeUiFactory.PrimaryColor;
        squareMarkText.fontSize = 18;
        squareMarkText.raycastTarget = false;
        var mark = squareMarkText.rectTransform;
        mark.anchorMin = Vector2.zero;
        mark.anchorMax = Vector2.one;
        mark.offsetMin = Vector2.zero;
        mark.offsetMax = Vector2.zero;

        checkmark = squareMarkText;
    }

    private static Toggle ConfigureToggle(
        GameObject target,
        Graphic targetGraphic,
        Graphic graphic,
        ToggleGroup toggleGroup,
        bool allowSwitchOff)
    {
        var toggle = target.AddComponent<Toggle>();
        toggle.targetGraphic = targetGraphic;
        toggle.graphic = graphic;
        toggle.group = toggleGroup;

        var colors = toggle.colors;
        colors.normalColor = targetGraphic.color;
        colors.highlightedColor = new Color(
            RuntimeUiFactory.PrimarySoftColor.r,
            RuntimeUiFactory.PrimarySoftColor.g,
            RuntimeUiFactory.PrimarySoftColor.b,
            0.65f);
        colors.pressedColor = RuntimeUiFactory.PrimarySoftColor;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(
            RuntimeUiFactory.SurfaceColor.r,
            RuntimeUiFactory.SurfaceColor.g,
            RuntimeUiFactory.SurfaceColor.b,
            0.55f);
        toggle.colors = colors;
        toggle.toggleTransition = Toggle.ToggleTransition.Fade;
        toggle.SetIsOnWithoutNotify(false);

        if (toggleGroup != null)
        {
            toggleGroup.allowSwitchOff = true;
        }

        return toggle;
    }

    private static void RefreshOtherInputs(QuestionBinding binding)
    {
        if (binding == null || binding.Options == null)
        {
            return;
        }

        for (var index = 0; index < binding.Options.Count; index++)
        {
            var option = binding.Options[index];

            if (option == null || option.OtherInput == null)
            {
                continue;
            }

            var isVisible = option.Toggle != null && option.Toggle.isOn;
            option.OtherInput.gameObject.SetActive(isVisible);
        }
    }

    private Dictionary<string, int> ResolveQuestionPageIndices(
        SessionFlowRuntimeState flowState,
        IReadOnlyList<SessionFlowQuestionRuntime> questions)
    {
        var result = new Dictionary<string, int>();

        if (questions == null)
        {
            return result;
        }

        for (var index = 0; index < questions.Count; index++)
        {
            var question = questions[index];

            if (question != null && !result.ContainsKey(question.Id))
            {
                result[question.Id] = question.PageIndex;
            }
        }

        var template = ResolveActiveSurveyTemplate(flowState);

        if (template == null
            || template.TemplateDocument == null
            || !template.TemplateDocument.IsValid
            || template.TemplateDocument.Root.Kind == JsonValueKind.Null)
        {
            return result;
        }

        if (!TryGetPropertyIgnoreCase(template.TemplateDocument.Root, "pages", out var pagesNode)
            || pagesNode.Kind != JsonValueKind.Array)
        {
            return result;
        }

        for (var pageIndex = 0; pageIndex < pagesNode.ArrayValue.Count; pageIndex++)
        {
            var pageNode = pagesNode.ArrayValue[pageIndex];

            if (pageNode == null || pageNode.Kind != JsonValueKind.Object)
            {
                continue;
            }

            var pageQuestionsNode = FindFirstProperty(pageNode, "questions", "items", "fields");

            if (pageQuestionsNode.Kind != JsonValueKind.Array)
            {
                continue;
            }

            for (var questionIndex = 0; questionIndex < pageQuestionsNode.ArrayValue.Count; questionIndex++)
            {
                var questionNode = pageQuestionsNode.ArrayValue[questionIndex];

                if (questionNode == null || questionNode.Kind != JsonValueKind.Object)
                {
                    continue;
                }

                var questionId = ReadJsonString(questionNode, "id", "code", "key");

                if (!string.IsNullOrWhiteSpace(questionId))
                {
                    result[questionId] = pageIndex;
                }
            }
        }

        return result;
    }

    private SurveyTemplateRuntimeModel ResolveActiveSurveyTemplate(SessionFlowRuntimeState flowState)
    {
        var clientRuntime = _context != null && _context.SessionCoordinator != null
            ? _context.SessionCoordinator.CurrentRuntime
            : ClientRuntimeState.Empty;
        var descriptor = flowState != null
            ? flowState.ActiveStepView.Descriptor
            : SessionFlowStepDescriptor.Empty;
        var config = flowState != null ? flowState.Config : SessionConfigRuntime.Empty;

        if (clientRuntime == null
            || !clientRuntime.HasSession
            || clientRuntime.Bootstrap == null
            || clientRuntime.Bootstrap.SurveyTemplates == null
            || config == null
            || !descriptor.IsDefined)
        {
            return null;
        }

        var surveyRef = ResolveDescriptorSurveyRef(config, descriptor);

        if (surveyRef == null || surveyRef.IsEmpty)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(surveyRef.SharedRefId)
            && config.TryGetSharedSurveyRef(surveyRef.SharedRefId, out var sharedSurveyRef)
            && sharedSurveyRef != null
            && sharedSurveyRef.IsValid)
        {
            surveyRef = surveyRef.Merge(sharedSurveyRef.Survey);
        }

        for (var index = 0; index < clientRuntime.Bootstrap.SurveyTemplates.Count; index++)
        {
            var candidate = clientRuntime.Bootstrap.SurveyTemplates[index];

            if (candidate == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(surveyRef.TemplateCode)
                && string.Equals(candidate.Code, surveyRef.TemplateCode, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            if (surveyRef.TemplateId.HasValue && candidate.Id == surveyRef.TemplateId.Value)
            {
                return candidate;
            }
        }

        return null;
    }

    private static SurveyRefRuntime ResolveDescriptorSurveyRef(SessionConfigRuntime config, SessionFlowStepDescriptor descriptor)
    {
        if (config == null || descriptor == null || !descriptor.IsDefined)
        {
            return SurveyRefRuntime.Empty;
        }

        switch (descriptor.Scope)
        {
            case SessionFlowStepScope.PreSession:
                return ResolveFlowSurveyRef(config.PreSessionFlow, descriptor.Key);
            case SessionFlowStepScope.PostSession:
                return ResolveFlowSurveyRef(config.PostSessionFlow, descriptor.Key);
            case SessionFlowStepScope.PostPeriodSurvey:
                return ResolvePostPeriodSurvey(config, descriptor.PeriodNumber, descriptor.Key);
            default:
                return SurveyRefRuntime.Empty;
        }
    }

    private static SurveyRefRuntime ResolveFlowSurveyRef(SessionFlowRuntime flow, string key)
    {
        if (flow == null || flow.Steps == null || string.IsNullOrWhiteSpace(key))
        {
            return SurveyRefRuntime.Empty;
        }

        for (var index = 0; index < flow.Steps.Count; index++)
        {
            var step = flow.Steps[index];

            if (step != null && string.Equals(step.Id, key, StringComparison.Ordinal))
            {
                return step.SurveyRef;
            }
        }

        return SurveyRefRuntime.Empty;
    }

    private static SurveyRefRuntime ResolvePostPeriodSurvey(SessionConfigRuntime config, int periodNumber, string key)
    {
        if (config == null
            || !config.TryGetPeriod(periodNumber, out var period)
            || period == null
            || period.PostPeriodSurveyRefs == null)
        {
            return SurveyRefRuntime.Empty;
        }

        for (var index = 0; index < period.PostPeriodSurveyRefs.Count; index++)
        {
            var surveyRef = period.PostPeriodSurveyRefs[index];
            var candidateKey = BuildSurveyProgressKey(periodNumber, surveyRef, index);

            if (string.Equals(candidateKey, key, StringComparison.Ordinal))
            {
                return surveyRef;
            }
        }

        return SurveyRefRuntime.Empty;
    }

    private void BuildPageOrder()
    {
        foreach (var pair in _questionBindings)
        {
            var binding = pair.Value;

            if (binding == null)
            {
                continue;
            }

            if (!_pageOrder.Contains(binding.PageIndex))
            {
                _pageOrder.Add(binding.PageIndex);
            }
        }

        _pageOrder.Sort();
    }

    private void RefreshPageVisibility()
    {
        var hasMultiplePages = _pageOrder.Count > 1;

        foreach (var pair in _questionBindings)
        {
            var binding = pair.Value;

            if (binding == null || binding.Panel == null)
            {
                continue;
            }

            binding.Panel.gameObject.SetActive(
                _pageOrder.Count == 0 || binding.PageIndex == _pageOrder[_currentPageIndex]);
        }

        if (_previousPageButton != null)
        {
            _previousPageButton.gameObject.SetActive(hasMultiplePages);
            _previousPageButton.interactable = _currentPageIndex > 0;
        }

        if (_nextPageButton != null)
        {
            _nextPageButton.gameObject.SetActive(hasMultiplePages);
            _nextPageButton.interactable = _currentPageIndex < _pageOrder.Count - 1;
        }

        if (_pageIndicatorLabel != null)
        {
            _pageIndicatorLabel.gameObject.SetActive(hasMultiplePages);
            _pageIndicatorLabel.text = hasMultiplePages
                ? $"{_currentPageIndex + 1} / {_pageOrder.Count}"
                : string.Empty;
        }

        if (_paginationRow != null)
        {
            _paginationRow.gameObject.SetActive(hasMultiplePages);
        }

        if (_questionScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            _questionScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void GoToPreviousPage()
    {
        if (_pageOrder.Count <= 1 || _currentPageIndex <= 0)
        {
            return;
        }

        _currentPageIndex--;
        RefreshPageVisibility();
    }

    private void GoToNextPage()
    {
        if (_pageOrder.Count <= 1 || _currentPageIndex >= _pageOrder.Count - 1)
        {
            return;
        }

        _currentPageIndex++;
        RefreshPageVisibility();
    }

    private static string BuildSurveyProgressKey(int periodNumber, SurveyRefRuntime surveyRef, int index)
    {
        if (surveyRef != null)
        {
            if (!string.IsNullOrWhiteSpace(surveyRef.Id))
            {
                return $"period_{periodNumber}_survey:{surveyRef.Id}";
            }

            if (!string.IsNullOrWhiteSpace(surveyRef.TemplateCode))
            {
                return $"period_{periodNumber}_survey:{surveyRef.TemplateCode}";
            }
        }

        return $"period_{periodNumber}_survey:{index + 1}";
    }

    private static string ReadJsonString(JsonValue node, params string[] propertyNames)
    {
        if (node == null || propertyNames == null)
        {
            return string.Empty;
        }

        for (var index = 0; index < propertyNames.Length; index++)
        {
            if (TryGetPropertyIgnoreCase(node, propertyNames[index], out var value))
            {
                return GetStringOrDefault(value);
            }
        }

        return string.Empty;
    }

    private static JsonValue FindFirstProperty(JsonValue node, params string[] propertyNames)
    {
        if (propertyNames == null)
        {
            return JsonValue.Null;
        }

        for (var index = 0; index < propertyNames.Length; index++)
        {
            if (TryGetPropertyIgnoreCase(node, propertyNames[index], out var value))
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

    private bool HasMissingRequiredAnswers(IReadOnlyDictionary<string, string> answers)
    {
        foreach (var pair in _questionBindings)
        {
            var binding = pair.Value;

            if (binding == null || binding.Question == null || !binding.Question.IsRequired)
            {
                continue;
            }

            if (answers == null
                || !answers.TryGetValue(binding.Question.Id, out var value)
                || string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyStepLayoutPreset(bool isSurvey, bool hasBody)
    {
        if (_cardRect == null
            || _bodyPanelLayout == null
            || _bodyScrollAreaLayoutElement == null
            || _bodyViewportLayoutElement == null
            || _bodyLabel == null)
        {
            return;
        }

        if (isSurvey)
        {
            _cardRect.sizeDelta = new Vector2(1120f, 820f);
            _bodyPanelLayout.minHeight = 420f;
            _bodyPanelLayout.preferredHeight = 560f;
            _bodyScrollAreaLayoutElement.minHeight = 390f;
            _bodyViewportLayoutElement.minHeight = 390f;
            _bodyLabel.fontSize = 17;
            return;
        }

        if (!hasBody)
        {
            _cardRect.sizeDelta = new Vector2(780f, 360f);
            _bodyPanelLayout.minHeight = 180f;
            _bodyPanelLayout.preferredHeight = 180f;
            _bodyScrollAreaLayoutElement.minHeight = 140f;
            _bodyViewportLayoutElement.minHeight = 140f;
            _bodyLabel.fontSize = 18;
            return;
        }

        var bodyLength = _bodyLabel.text != null
            ? _bodyLabel.text.Length
            : 0;

        if (bodyLength <= 260)
        {
            _cardRect.sizeDelta = new Vector2(820f, 500f);
            _bodyPanelLayout.minHeight = 220f;
            _bodyPanelLayout.preferredHeight = 240f;
            _bodyScrollAreaLayoutElement.minHeight = 180f;
            _bodyViewportLayoutElement.minHeight = 180f;
            _bodyLabel.fontSize = 21;
            return;
        }

        if (bodyLength <= 760)
        {
            _cardRect.sizeDelta = new Vector2(940f, 620f);
            _bodyPanelLayout.minHeight = 320f;
            _bodyPanelLayout.preferredHeight = 360f;
            _bodyScrollAreaLayoutElement.minHeight = 280f;
            _bodyViewportLayoutElement.minHeight = 280f;
            _bodyLabel.fontSize = 19;
            return;
        }

        _cardRect.sizeDelta = new Vector2(1120f, 820f);
        _bodyPanelLayout.minHeight = 420f;
        _bodyPanelLayout.preferredHeight = 560f;
        _bodyScrollAreaLayoutElement.minHeight = 390f;
        _bodyViewportLayoutElement.minHeight = 390f;
        _bodyLabel.fontSize = 17;
    }

    private void OnSurveyAnswerChanged()
    {
        var flowState = _context != null && _context.SessionFlow != null
            ? _context.SessionFlow.Current
            : SessionFlowRuntimeState.Empty;
        var viewModel = flowState != null
            ? flowState.ActiveStepView
            : SessionFlowStepViewModel.Empty;

        UpdatePrimaryButtonInteractable(viewModel);
    }

    private void HandleQuestionAnswerChanged(QuestionBinding binding)
    {
        RefreshOtherInputs(binding);

        if (IsInstructionQuizActive())
        {
            ResetOptionFeedback(binding);
            SetQuestionFeedback(binding, string.Empty, true);
            OnSurveyAnswerChanged();
            return;
        }

        if (binding != null
            && binding.Question != null
            && binding.Question.IsCheckableChoiceQuestion)
        {
            EvaluateCheckableQuestion(binding);
        }

        OnSurveyAnswerChanged();
    }

    private int CountIncorrectInstructionQuizAnswers()
    {
        var incorrectAnswersCount = 0;

        foreach (var pair in _questionBindings)
        {
            var binding = pair.Value;

            if (binding == null
                || binding.Question == null
                || !binding.Question.IsCheckableChoiceQuestion)
            {
                continue;
            }

            if (!IsCheckableQuestionAnsweredCorrectly(binding))
            {
                incorrectAnswersCount++;
            }
        }

        return incorrectAnswersCount;
    }

    private void UpdatePrimaryButtonInteractable(SessionFlowStepViewModel viewModel)
    {
        if (_primaryButton == null)
        {
            return;
        }

        if (viewModel == null || viewModel.RendererKind != SessionFlowRendererKind.Survey)
        {
            _primaryButton.interactable = true;
            return;
        }

        _primaryButton.interactable = !HasMissingRequiredAnswers(CollectAnswers());
    }

    private static bool IsCheckableQuestionAnsweredCorrectly(QuestionBinding binding)
    {
        if (binding == null || binding.Question == null || binding.Options == null)
        {
            return true;
        }

        var selectedOptionIds = CollectSelectedOptionIds(binding);
        var correctOptionIds = new List<string>();

        for (var index = 0; index < binding.Options.Count; index++)
        {
            var option = binding.Options[index];

            if (option != null && option.Option != null && option.Option.IsCorrect)
            {
                correctOptionIds.Add(ResolveOptionValue(option.Option));
            }
        }

        return binding.Question.Type == SessionFlowQuestionType.MultipleChoice
            ? AreExactSetsEqual(selectedOptionIds, correctOptionIds)
            : selectedOptionIds.Count == 1
              && correctOptionIds.Count == 1
              && string.Equals(selectedOptionIds[0], correctOptionIds[0], StringComparison.Ordinal);
    }

    private void EvaluateCheckableQuestion(QuestionBinding binding)
    {
        var selectedOptionIds = CollectSelectedOptionIds(binding);
        ResetOptionFeedback(binding);

        if (selectedOptionIds.Count == 0)
        {
            SetQuestionFeedback(binding, string.Empty, true);
            return;
        }

        var correctOptionIds = new List<string>();
        var hasIncorrectSelection = false;

        for (var index = 0; index < binding.Options.Count; index++)
        {
            var option = binding.Options[index];

            if (option != null && option.Option != null && option.Option.IsCorrect)
            {
                correctOptionIds.Add(ResolveOptionValue(option.Option));
            }

            if (option != null
                && option.Option != null
                && !option.Option.IsCorrect
                && option.Toggle != null
                && option.Toggle.isOn)
            {
                hasIncorrectSelection = true;
            }
        }

        var isCorrect = binding.Question.Type == SessionFlowQuestionType.MultipleChoice
            ? AreExactSetsEqual(selectedOptionIds, correctOptionIds)
            : selectedOptionIds.Count == 1
              && correctOptionIds.Count == 1
              && string.Equals(selectedOptionIds[0], correctOptionIds[0], StringComparison.Ordinal);

        if (hasIncorrectSelection)
        {
            if (IsInstructionQuizActive())
            {
                ResetQuestionSelection(binding);
                SetQuestionFeedback(binding, string.Empty, false);
                OpenInstructionReferencePopup("Ответ не верный, найдите в инструкции правильный");
                return;
            }

            ApplyIncorrectOptionFeedback(binding, selectedOptionIds);
            SetQuestionFeedback(binding, "Неверно.", false);
            return;
        }

        if (isCorrect)
        {
            ApplyCorrectOptionFeedback(binding, selectedOptionIds);
            SetQuestionFeedback(binding, "Верно.", true);
            return;
        }

        SetQuestionFeedback(binding, string.Empty, true);
    }

    private static List<string> CollectSelectedOptionIds(QuestionBinding binding)
    {
        var result = new List<string>();

        if (binding == null || binding.Options == null)
        {
            return result;
        }

        for (var index = 0; index < binding.Options.Count; index++)
        {
            var option = binding.Options[index];

            if (option == null || option.Toggle == null || !option.Toggle.isOn || option.Option == null)
            {
                continue;
            }

            result.Add(ResolveOptionValue(option.Option));
        }

        return result;
    }

    private static bool AreExactSetsEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left == null || right == null || left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!ContainsValue(right, left[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsValue(IReadOnlyList<string> values, string candidate)
    {
        if (values == null)
        {
            return false;
        }

        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void ResetOptionFeedback(QuestionBinding binding)
    {
        if (binding == null || binding.Options == null)
        {
            return;
        }

        for (var index = 0; index < binding.Options.Count; index++)
        {
            var option = binding.Options[index];

            if (option == null || option.Option == null || option.Label == null)
            {
                continue;
            }

            option.Label.color = RuntimeUiFactory.TextPrimaryColor;
            option.Label.supportRichText = false;
            option.Label.text = option.Option.Label;
        }
    }

    private static string GetQuestionCountWordForm(int count)
    {
        var mod100 = count % 100;

        if (mod100 >= 11 && mod100 <= 14)
        {
            return "вопросов";
        }

        switch (count % 10)
        {
            case 1:
                return "вопрос";
            case 2:
            case 3:
            case 4:
                return "вопроса";
            default:
                return "вопросов";
        }
    }

    private static void ApplyIncorrectOptionFeedback(
        QuestionBinding binding,
        IReadOnlyList<string> selectedOptionIds)
    {
        if (binding == null || binding.Options == null)
        {
            return;
        }

        for (var index = 0; index < binding.Options.Count; index++)
        {
            var option = binding.Options[index];

            if (option == null || option.Option == null || option.Label == null)
            {
                continue;
            }

            var optionId = ResolveOptionValue(option.Option);

            if (ContainsValue(selectedOptionIds, optionId) && !option.Option.IsCorrect)
            {
                option.Label.color = RuntimeUiFactory.DangerColor;
                option.Label.supportRichText = true;
                option.Label.text = $"{option.Option.Label} <b>(неверно)</b>";
            }
        }
    }

    private static void ApplyCorrectOptionFeedback(
        QuestionBinding binding,
        IReadOnlyList<string> selectedOptionIds)
    {
        if (binding == null || binding.Options == null)
        {
            return;
        }

        for (var index = 0; index < binding.Options.Count; index++)
        {
            var option = binding.Options[index];

            if (option == null || option.Option == null || option.Label == null)
            {
                continue;
            }

            var optionId = ResolveOptionValue(option.Option);

            if (ContainsValue(selectedOptionIds, optionId))
            {
                option.Label.color = RuntimeUiFactory.TextPrimaryColor;
                option.Label.supportRichText = true;
                option.Label.text = $"{option.Option.Label} <b>(верно)</b>";
            }
        }
    }

    private void ResetQuestionSelection(QuestionBinding binding)
    {
        if (binding == null || binding.Options == null)
        {
            return;
        }

        for (var index = 0; index < binding.Options.Count; index++)
        {
            var option = binding.Options[index];

            if (option == null)
            {
                continue;
            }

            if (option.Toggle != null)
            {
                option.Toggle.SetIsOnWithoutNotify(false);
            }

            if (option.OtherInput != null)
            {
                option.OtherInput.SetTextWithoutNotify(string.Empty);
            }
        }

        RefreshOtherInputs(binding);
        ResetOptionFeedback(binding);
    }

    private static void SetQuestionFeedback(QuestionBinding binding, string text, bool isCorrect)
    {
        if (binding == null || binding.FeedbackLabel == null)
        {
            return;
        }

        binding.FeedbackLabel.text = text ?? string.Empty;
        binding.FeedbackLabel.color = isCorrect
            ? RuntimeUiFactory.TextSecondaryColor
            : RuntimeUiFactory.DangerColor;
        binding.FeedbackLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(binding.FeedbackLabel.text));
    }

    private static string CollectQuestionAnswer(QuestionBinding binding)
    {
        if (binding == null || binding.Question == null)
        {
            return string.Empty;
        }

        switch (binding.Question.Type)
        {
            case SessionFlowQuestionType.SingleChoice:
            case SessionFlowQuestionType.Scale:
                return CollectSingleChoiceAnswer(binding);
            case SessionFlowQuestionType.MultipleChoice:
                return CollectMultipleChoiceAnswer(binding);
            default:
                return binding.TextInput != null
                    ? (binding.TextInput.text ?? string.Empty).Trim()
                    : string.Empty;
        }
    }

    private static string CollectSingleChoiceAnswer(QuestionBinding binding)
    {
        if (binding.Options == null)
        {
            return string.Empty;
        }

        for (var index = 0; index < binding.Options.Count; index++)
        {
            var option = binding.Options[index];

            if (option == null || option.Toggle == null || !option.Toggle.isOn)
            {
                continue;
            }

            return ResolveOptionAnswer(option);
        }

        return string.Empty;
    }

    private static string CollectMultipleChoiceAnswer(QuestionBinding binding)
    {
        if (binding.Options == null)
        {
            return string.Empty;
        }

        var values = new List<string>();

        for (var index = 0; index < binding.Options.Count; index++)
        {
            var option = binding.Options[index];

            if (option == null || option.Toggle == null || !option.Toggle.isOn)
            {
                continue;
            }

            var value = ResolveOptionAnswer(option);

            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            values.Add(value);
        }

        return values.Count > 0
            ? string.Join("\n", values.ToArray())
            : string.Empty;
    }

    private static string ResolveOptionAnswer(OptionBinding option)
    {
        if (option == null || option.Option == null)
        {
            return string.Empty;
        }

        if (!option.Option.IsOther)
        {
            return ResolveOptionValue(option.Option);
        }

        var otherValue = option.OtherInput != null
            ? (option.OtherInput.text ?? string.Empty).Trim()
            : string.Empty;

        return string.IsNullOrWhiteSpace(otherValue)
            ? string.Empty
            : $"other:{otherValue}";
    }

    private static string ResolveOptionValue(SessionFlowQuestionOptionRuntime option)
    {
        if (option == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(option.Id)
            ? option.Id
            : option.Label ?? string.Empty;
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
        var titleLayout = _instructionPopupTitleLabel.gameObject.AddComponent<LayoutElement>();
        titleLayout.flexibleWidth = 1f;

        var closeButton = RuntimeUiFactory.CreateSecondaryButton(headerRow, "Закрыть", 42f);
        var closeLayout = closeButton.gameObject.GetComponent<LayoutElement>();

        if (closeLayout != null)
        {
            closeLayout.flexibleWidth = 0f;
            closeLayout.preferredWidth = 140f;
        }

        closeButton.onClick.AddListener(CloseInstructionReferencePopup);

        _instructionPopupNoticeLabel = RuntimeUiFactory.CreateBodyText(content, string.Empty, TextAnchor.MiddleLeft);
        _instructionPopupNoticeLabel.color = RuntimeUiFactory.DangerColor;
        RuntimeUiFactory.ApplyTextStyle(_instructionPopupNoticeLabel, FontStyle.Bold);
        _instructionPopupNoticeLabel.gameObject.SetActive(false);

        var bodyPanel = RuntimeUiFactory.CreatePanel(
            "InstructionReferenceBodyPanel",
            content,
            new RectOffset(12, 12, 12, 12),
            10f,
            RuntimeUiFactory.SurfaceColor);
        DisableContentSizeFitter(bodyPanel);
        var panelLayout = bodyPanel.gameObject.AddComponent<LayoutElement>();
        panelLayout.flexibleHeight = 1f;
        panelLayout.minHeight = 600f;

        var scrollArea = RuntimeUiFactory.CreateRow("InstructionReferenceScrollArea", bodyPanel, 8f, TextAnchor.UpperLeft);
        var scrollAreaLayout = scrollArea.GetComponent<HorizontalLayoutGroup>();
        scrollAreaLayout.childForceExpandWidth = false;
        scrollAreaLayout.childForceExpandHeight = true;
        scrollAreaLayout.childControlHeight = true;
        var scrollAreaElement = scrollArea.gameObject.AddComponent<LayoutElement>();
        scrollAreaElement.flexibleHeight = 1f;
        scrollAreaElement.minHeight = 560f;

        var viewport = CreateRect("InstructionReferenceViewport", scrollArea);
        var viewportLayout = viewport.gameObject.AddComponent<LayoutElement>();
        viewportLayout.flexibleWidth = 1f;
        viewportLayout.flexibleHeight = 1f;
        viewportLayout.minHeight = 560f;
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

    private void ResolveInstructionReference(SessionFlowRuntimeState flowState)
    {
        _instructionReferenceTitle = string.Empty;
        _instructionReferenceBody = string.Empty;

        if (flowState == null || flowState.Config == null || flowState.ActiveStepView == null)
        {
            return;
        }

        var descriptor = flowState.ActiveStepView.Descriptor;

        if (descriptor == null
            || descriptor.Scope != SessionFlowStepScope.PreSession
            || flowState.ActiveStepView.RendererKind != SessionFlowRendererKind.Survey)
        {
            return;
        }

        var flow = flowState.Config.PreSessionFlow;

        if (flow == null || flow.Steps == null)
        {
            return;
        }

        for (var index = 0; index < flow.Steps.Count; index++)
        {
            var step = flow.Steps[index];

            if (step == null || !string.Equals(step.Id, descriptor.Key, StringComparison.Ordinal))
            {
                continue;
            }

            for (var previousIndex = index - 1; previousIndex >= 0; previousIndex--)
            {
                var previousStep = flow.Steps[previousIndex];

                if (!IsInstructionReferenceCandidate(previousStep))
                {
                    continue;
                }

                _instructionReferenceTitle = string.IsNullOrWhiteSpace(previousStep.Title)
                    ? "Инструкция"
                    : previousStep.Title;
                _instructionReferenceBody = previousStep.Body ?? string.Empty;
                return;
            }

            return;
        }
    }

    private void OpenInstructionReferencePopup()
    {
        OpenInstructionReferencePopup(null);
    }

    private void OpenInstructionReferencePopup(string noticeText)
    {
        if (_instructionPopupOverlay == null || string.IsNullOrWhiteSpace(_instructionReferenceBody))
        {
            return;
        }

        _instructionPopupTitleLabel.text = string.IsNullOrWhiteSpace(_instructionReferenceTitle)
            ? "Инструкция"
            : _instructionReferenceTitle;
        _instructionPopupNoticeLabel.text = noticeText ?? string.Empty;
        _instructionPopupNoticeLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(_instructionPopupNoticeLabel.text));
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

    private bool IsInstructionQuizActive()
    {
        var flowState = _context != null && _context.SessionFlow != null
            ? _context.SessionFlow.Current
            : SessionFlowRuntimeState.Empty;
        var descriptor = flowState != null && flowState.ActiveStepView != null
            ? flowState.ActiveStepView.Descriptor
            : SessionFlowStepDescriptor.Empty;
        return descriptor != null && descriptor.Type == SessionFlowStepType.InstructionQuiz;
    }

    private static bool IsInstructionReferenceCandidate(FlowStepRuntime step)
    {
        return step != null
               && !string.IsNullOrWhiteSpace(step.Id)
               && !string.IsNullOrWhiteSpace(step.Body)
               && !IsSurveyStepType(step.Type);
    }

    private static bool IsSurveyStepType(SessionFlowStepType type)
    {
        switch (type)
        {
            case SessionFlowStepType.InstructionQuiz:
            case SessionFlowStepType.PreTest:
            case SessionFlowStepType.PostTest:
            case SessionFlowStepType.PostPeriodSurvey:
                return true;
            default:
                return false;
        }
    }

    private static string FormatSimpleMarkdown(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        var normalized = rawText.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var builder = new StringBuilder();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index] ?? string.Empty;
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                if (builder.Length > 0 && !EndsWithDoubleLineBreak(builder))
                {
                    builder.Append("\n\n");
                }

                continue;
            }

            if (TryAppendHeading(builder, trimmed))
            {
                continue;
            }

            if (IsBulletLine(trimmed))
            {
                builder.Append("• ");
                builder.Append(FormatInline(trimmed.Substring(2).Trim()));
                builder.Append('\n');
                continue;
            }

            builder.Append(FormatInline(trimmed));

            if (index < lines.Length - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString().Trim();
    }

    private static bool TryAppendHeading(StringBuilder builder, string line)
    {
        var level = 0;

        while (level < line.Length && line[level] == '#')
        {
            level++;
        }

        if (level == 0 || level >= line.Length || line[level] != ' ')
        {
            return false;
        }

        var text = line.Substring(level + 1).Trim();

        if (text.Length == 0)
        {
            return false;
        }

        var size = level <= 1
            ? 24
            : level == 2
                ? 21
                : 19;

        builder.Append("<b><size=");
        builder.Append(size);
        builder.Append('>');
        builder.Append(FormatInline(text));
        builder.Append("</size></b>\n");
        return true;
    }

    private static bool IsBulletLine(string line)
    {
        return line.StartsWith("- ") || line.StartsWith("* ");
    }

    private static string FormatInline(string text)
    {
        var escaped = EscapeRichText(text ?? string.Empty);
        return BoldRegex.Replace(escaped, "<b>$1</b>");
    }

    private static string EscapeRichText(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static bool EndsWithDoubleLineBreak(StringBuilder builder)
    {
        return builder.Length >= 2
               && builder[builder.Length - 1] == '\n'
               && builder[builder.Length - 2] == '\n';
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
}
