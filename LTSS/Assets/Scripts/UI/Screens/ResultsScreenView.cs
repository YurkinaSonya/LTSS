using System;
using Game.Core.Application;
using Game.Core.Application.UI;
using UnityEngine;
using UnityEngine.UI;

[ScreenDefinition(ScreenId.Results)]
public sealed class ResultsScreenView : ScreenView
{
    private static readonly Color UjeSuccessColor = new Color32(74, 154, 95, 255);

    private Text _titleLabel;
    private Text _ujeValueLabel;
    private Text _noteLabel;
    private Button _returnButton;
    private bool _isBuilt;

    public override ScreenController Construct(UIContext context)
    {
        EnsureBuilt();
        return new ResultsScreenController(this, context);
    }

    public void BindReturn(Action callback)
    {
        EnsureBuilt();

        if (_returnButton == null)
        {
            return;
        }

        _returnButton.onClick.RemoveAllListeners();

        if (callback != null)
        {
            _returnButton.onClick.AddListener(() => callback.Invoke());
        }
    }

    public void Apply(string ujeValue)
    {
        EnsureBuilt();

        SetText(_titleLabel, "\u0418\u0442\u043E\u0433 \u0436\u0438\u0437\u043D\u0435\u043D\u043D\u043E\u0433\u043E \u043F\u0443\u0442\u0438");
        SetText(_ujeValueLabel, ujeValue);
        SetText(_noteLabel, "\u041D\u0435 \u0437\u0430\u0431\u0443\u0434\u044C\u0442\u0435 \u0437\u0430\u0444\u0438\u043A\u0441\u0438\u0440\u043E\u0432\u0430\u0442\u044C \u0441\u0432\u043E\u0439 \u0440\u0435\u0437\u0443\u043B\u044C\u0442\u0430\u0442");
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        var background = RuntimeUiFactory.CreateScreenBackground(transform);
        var card = RuntimeUiFactory.CreateCard("ResultsCard", background, new Vector2(760f, 560f));
        var content = RuntimeUiFactory.CreateContentRoot(
            "Content",
            card,
            new RectOffset(40, 40, 36, 34),
            16f,
            TextAnchor.UpperCenter);

        _titleLabel = RuntimeUiFactory.CreateTitle(
            content,
            "\u0418\u0442\u043E\u0433 \u0436\u0438\u0437\u043D\u0435\u043D\u043D\u043E\u0433\u043E \u043F\u0443\u0442\u0438",
            TextAnchor.MiddleCenter);
        _titleLabel.fontSize = 26;
        _titleLabel.color = RuntimeUiFactory.TextSecondaryColor;

        RuntimeUiFactory.AddFlexibleSpacer(content);

        RuntimeUiFactory.CreateCaption(content, "\u0418\u0442\u043E\u0433\u043E\u0432\u044B\u0439 \u0423\u0416\u042D", TextAnchor.MiddleCenter);
        _ujeValueLabel = RuntimeUiFactory.CreateValueText(content, "-", 64, TextAnchor.MiddleCenter);
        _ujeValueLabel.color = UjeSuccessColor;

        RuntimeUiFactory.AddSpacer(content, 10f);

        _noteLabel = RuntimeUiFactory.CreateBodyText(content, string.Empty, TextAnchor.MiddleCenter);
        RuntimeUiFactory.ApplyTextStyle(_noteLabel, FontStyle.Bold);
        _noteLabel.color = RuntimeUiFactory.TextPrimaryColor;

        RuntimeUiFactory.AddFlexibleSpacer(content);

        var actions = RuntimeUiFactory.CreateRow("Actions", content, 12f, TextAnchor.MiddleCenter);
        _returnButton = RuntimeUiFactory.CreatePrimaryButton(actions, "\u0412\u0435\u0440\u043D\u0443\u0442\u044C\u0441\u044F \u043A \u0441\u0435\u0441\u0441\u0438\u0438", 50f);
    }

    private static void SetText(Text label, string value)
    {
        if (label == null)
        {
            return;
        }

        label.text = value ?? string.Empty;
    }
}
