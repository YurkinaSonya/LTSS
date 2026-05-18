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
        public readonly List<OptionBinding> Options = new List<OptionBinding>();
    }

    private sealed class OptionBinding
    {
        public SessionFlowQuestionOptionRuntime Option;
        public Toggle Toggle;
        public InputField OtherInput;
    }

    private Text _titleLabel;
    private Text _subtitleLabel;
    private Text _bodyLabel;
    private Text _statusLabel;
    private RectTransform _questionContainer;
    private Button _primaryButton;
    private Button _secondaryButton;
    private bool _isBuilt;
    private string _lastStepKey = string.Empty;
    private readonly Dictionary<string, QuestionBinding> _questionBindings =
        new Dictionary<string, QuestionBinding>();
    private static readonly Regex BoldRegex = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

    public override ScreenController Construct(UIContext context)
    {
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

        _titleLabel.text = title;
        _subtitleLabel.text = viewModel.Subtitle ?? string.Empty;
        _subtitleLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(_subtitleLabel.text));
        _bodyLabel.text = FormatSimpleMarkdown(viewModel.Body);
        _bodyLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(_bodyLabel.text));
        _statusLabel.text = !string.IsNullOrWhiteSpace(flowState != null ? flowState.LastError : string.Empty)
            ? flowState.LastError
            : flowState != null
                ? flowState.StatusMessage
                : string.Empty;
        _statusLabel.color = !string.IsNullOrWhiteSpace(flowState != null ? flowState.LastError : string.Empty)
            ? RuntimeUiFactory.DangerColor
            : RuntimeUiFactory.TextSecondaryColor;
        _statusLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(_statusLabel.text));

        if (_lastStepKey != viewModel.Descriptor.Key)
        {
            RebuildQuestions(viewModel.Questions);
            _lastStepKey = viewModel.Descriptor.Key ?? string.Empty;
        }

        RuntimeUiFactory.SetButtonText(
            _primaryButton,
            string.IsNullOrWhiteSpace(viewModel.PrimaryActionText)
                ? "Далее"
                : viewModel.PrimaryActionText);

        var canShowSecondary = viewModel.CanSkip && !string.IsNullOrWhiteSpace(viewModel.SecondaryActionText);
        _secondaryButton.gameObject.SetActive(canShowSecondary);

        if (canShowSecondary)
        {
            RuntimeUiFactory.SetButtonText(_secondaryButton, viewModel.SecondaryActionText);
        }
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

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        var background = RuntimeUiFactory.CreateScreenBackground(transform);
        var card = RuntimeUiFactory.CreateCard("FlowCard", background, new Vector2(860f, 700f));
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(36, 36, 34, 30),
            14f);

        _titleLabel = RuntimeUiFactory.CreateTitle(content, "Шаг сценария", TextAnchor.MiddleLeft);
        _subtitleLabel = RuntimeUiFactory.CreateCaption(content, string.Empty);
        _subtitleLabel.gameObject.SetActive(false);

        var bodyPanel = RuntimeUiFactory.CreatePanel(
            "BodyPanel",
            content,
            new RectOffset(20, 20, 18, 18),
            10f,
            RuntimeUiFactory.SurfaceColor);
        _bodyLabel = RuntimeUiFactory.CreateBodyText(bodyPanel, string.Empty);
        _bodyLabel.alignment = TextAnchor.UpperLeft;
        _bodyLabel.supportRichText = true;
        _bodyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bodyLabel.verticalOverflow = VerticalWrapMode.Overflow;

        _questionContainer = RuntimeUiFactory.CreatePanel(
            "QuestionPanel",
            content,
            new RectOffset(18, 18, 18, 18),
            12f,
            RuntimeUiFactory.PrimarySoftColor);
        _questionContainer.gameObject.SetActive(false);

        _statusLabel = RuntimeUiFactory.CreateCaption(content, string.Empty);
        _statusLabel.gameObject.SetActive(false);

        RuntimeUiFactory.AddFlexibleSpacer(content);
        var buttonRow = RuntimeUiFactory.CreateRow("Buttons", content, 12f, TextAnchor.MiddleCenter);
        _secondaryButton = RuntimeUiFactory.CreateSecondaryButton(buttonRow, "Пропустить");
        _primaryButton = RuntimeUiFactory.CreatePrimaryButton(buttonRow, "Далее");
        _secondaryButton.gameObject.SetActive(false);
    }

    private void RebuildQuestions(IReadOnlyList<SessionFlowQuestionRuntime> questions)
    {
        _questionBindings.Clear();

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
            _questionContainer.gameObject.SetActive(false);
            return;
        }

        _questionContainer.gameObject.SetActive(true);

        for (var index = 0; index < questions.Count; index++)
        {
            var question = questions[index];

            if (question == null)
            {
                continue;
            }

            var binding = new QuestionBinding
            {
                Question = question
            };

            var questionPanel = RuntimeUiFactory.CreatePanel(
                $"Question_{index + 1}",
                _questionContainer,
                new RectOffset(14, 14, 14, 14),
                8f,
                RuntimeUiFactory.SurfaceColor);

            BuildQuestionHeader(questionPanel, question);
            BuildQuestionInput(questionPanel, binding, question);
            _questionBindings[question.Id] = binding;
        }
    }

    private static void BuildQuestionHeader(Transform parent, SessionFlowQuestionRuntime question)
    {
        var title = question.IsRequired
            ? $"{question.Label} *"
            : question.Label;
        var titleLabel = RuntimeUiFactory.CreateBodyText(parent, title);
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

            CreateSelectionBox(row, out var checkmark);
            optionBinding.Toggle = ConfigureToggle(row.gameObject, rowBackground, checkmark, toggleGroup, allowMultiple);

            var label = RuntimeUiFactory.CreateBodyText(row, option.Label);
            label.alignment = TextAnchor.MiddleLeft;
            var labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;

            if (option.IsOther)
            {
                optionBinding.OtherInput = RuntimeUiFactory.CreateInputField(optionsRoot, "Укажите свой вариант");
                optionBinding.OtherInput.gameObject.SetActive(false);
            }

            binding.Options.Add(optionBinding);
            optionBinding.Toggle.onValueChanged.AddListener(_ => RefreshOtherInputs(binding));
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

            CreateSelectionBox(column, out var checkmark);
            optionBinding.Toggle = ConfigureToggle(column.gameObject, columnBackground, checkmark, toggleGroup, false);

            if (option.IsOther)
            {
                optionBinding.OtherInput = RuntimeUiFactory.CreateInputField(column, "Свой вариант");
                optionBinding.OtherInput.gameObject.SetActive(false);
            }

            binding.Options.Add(optionBinding);
            optionBinding.Toggle.onValueChanged.AddListener(_ => RefreshOtherInputs(binding));
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

    private static void CreateSelectionBox(Transform parent, out Image checkmark)
    {
        var box = CreateRect("Box", parent);
        var boxLayout = box.gameObject.AddComponent<LayoutElement>();
        boxLayout.preferredWidth = 24f;
        boxLayout.preferredHeight = 24f;
        boxLayout.minWidth = 24f;
        boxLayout.minHeight = 24f;

        var boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = RuntimeUiFactory.ElevatedSurfaceColor;
        var outline = box.gameObject.AddComponent<Outline>();
        outline.effectColor = RuntimeUiFactory.BorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        var mark = CreateRect("Checkmark", box);
        mark.anchorMin = new Vector2(0.5f, 0.5f);
        mark.anchorMax = new Vector2(0.5f, 0.5f);
        mark.pivot = new Vector2(0.5f, 0.5f);
        mark.sizeDelta = new Vector2(12f, 12f);
        mark.anchoredPosition = Vector2.zero;

        checkmark = mark.gameObject.AddComponent<Image>();
        checkmark.color = RuntimeUiFactory.PrimaryColor;
        checkmark.raycastTarget = false;
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
        toggle.isOn = false;

        if (toggleGroup != null)
        {
            toggleGroup.allowSwitchOff = allowSwitchOff;
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

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }
}
