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
    private Text _titleLabel;
    private Text _subtitleLabel;
    private Text _bodyLabel;
    private Text _statusLabel;
    private RectTransform _questionContainer;
    private Button _primaryButton;
    private Button _secondaryButton;
    private bool _isBuilt;
    private string _lastStepKey = string.Empty;
    private readonly Dictionary<string, InputField> _questionInputs =
        new Dictionary<string, InputField>();
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

        foreach (var pair in _questionInputs)
        {
            if (pair.Value == null)
            {
                continue;
            }

            result[pair.Key] = pair.Value.text ?? string.Empty;
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
        _questionInputs.Clear();

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

            var questionPanel = RuntimeUiFactory.CreateContentRoot(
                $"Question_{index + 1}",
                _questionContainer,
                new RectOffset(0, 0, 0, 0),
                6f);

            var title = question.IsRequired
                ? $"{question.Label} *"
                : question.Label;
            RuntimeUiFactory.CreateBodyText(questionPanel, title);

            if (!string.IsNullOrWhiteSpace(question.Description))
            {
                RuntimeUiFactory.CreateCaption(questionPanel, question.Description);
            }

            var input = RuntimeUiFactory.CreateInputField(
                questionPanel,
                string.IsNullOrWhiteSpace(question.Placeholder)
                    ? "Введите ответ"
                    : question.Placeholder);

            _questionInputs[question.Id] = input;
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
}
