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
    private Text _moneyValueLabel;
    private Text _moneyCaptionLabel;
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

    public void Apply(string ujeValue, string moneyValue, string moneyCaption)
    {
        EnsureBuilt();

        SetText(_titleLabel, "Итог жизненного пути");
        SetText(_ujeValueLabel, ujeValue);
        SetText(_moneyValueLabel, moneyValue);
        SetText(_moneyCaptionLabel, moneyCaption);
        _moneyCaptionLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(moneyCaption));
        SetText(_noteLabel, "Не забудьте зафиксировать свой результат");
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

        _titleLabel = RuntimeUiFactory.CreateTitle(content, "Итог жизненного пути", TextAnchor.MiddleCenter);
        _titleLabel.fontSize = 26;
        _titleLabel.color = RuntimeUiFactory.TextSecondaryColor;

        RuntimeUiFactory.AddFlexibleSpacer(content);

        RuntimeUiFactory.CreateCaption(content, "Итоговый УЖЭ", TextAnchor.MiddleCenter);
        _ujeValueLabel = RuntimeUiFactory.CreateValueText(content, "-", 64, TextAnchor.MiddleCenter);
        _ujeValueLabel.color = UjeSuccessColor;

        RuntimeUiFactory.AddSpacer(content, 10f);

        RuntimeUiFactory.CreateCaption(content, "Итог по деньгам", TextAnchor.MiddleCenter);
        _moneyValueLabel = RuntimeUiFactory.CreateValueText(content, "-", 46, TextAnchor.MiddleCenter);
        _moneyCaptionLabel = RuntimeUiFactory.CreateCaption(content, string.Empty, TextAnchor.MiddleCenter);
        _moneyCaptionLabel.color = RuntimeUiFactory.TextSecondaryColor;
        _moneyCaptionLabel.gameObject.SetActive(false);

        RuntimeUiFactory.AddSpacer(content, 10f);

        _noteLabel = RuntimeUiFactory.CreateBodyText(content, string.Empty, TextAnchor.MiddleCenter);
        RuntimeUiFactory.ApplyTextStyle(_noteLabel, FontStyle.Bold);
        _noteLabel.color = RuntimeUiFactory.TextPrimaryColor;

        RuntimeUiFactory.AddFlexibleSpacer(content);

        var actions = RuntimeUiFactory.CreateRow("Actions", content, 12f, TextAnchor.MiddleCenter);
        _returnButton = RuntimeUiFactory.CreatePrimaryButton(actions, "Вернуться к сессии", 50f);
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
