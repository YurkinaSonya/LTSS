using System;
using UnityEngine;
using UnityEngine.UI;

public static class RuntimeUiFactory
{
    private static Font _defaultFont;
    private static Sprite _defaultSprite;
    private static bool _spriteResolved;

    public static Font DefaultFont =>
        _defaultFont ?? (_defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf"));

    public static Sprite DefaultSprite
    {
        get
        {
            if (_spriteResolved)
            {
                return _defaultSprite;
            }

            _spriteResolved = true;
            _defaultSprite = Resources.GetBuiltinResource<Sprite>("UISprite.psd");

            if (_defaultSprite == null)
            {
                _defaultSprite = Resources.GetBuiltinResource<Sprite>("Background.psd");
            }

            return _defaultSprite;
        }
    }

    public static RectTransform CreateElement(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        var rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.localScale = Vector3.one;
        return rectTransform;
    }

    public static RectTransform CreateFullscreenPanel(
        string name,
        Transform parent,
        Color color)
    {
        var rectTransform = CreateElement(name, parent);
        Stretch(rectTransform);

        var image = rectTransform.gameObject.AddComponent<Image>();
        image.color = color;
        image.sprite = DefaultSprite;
        image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;

        return rectTransform;
    }

    public static RectTransform CreateCard(
        string name,
        Transform parent,
        float width,
        Color color)
    {
        var rectTransform = CreateElement(name, parent);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(width, 0f);

        var image = rectTransform.gameObject.AddComponent<Image>();
        image.color = color;
        image.sprite = DefaultSprite;
        image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;

        var fitter = rectTransform.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = rectTransform.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(36, 36, 32, 32);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return rectTransform;
    }

    public static HorizontalLayoutGroup AddHorizontalLayout(
        RectTransform rectTransform,
        float spacing)
    {
        var layout = rectTransform.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static LayoutElement AddLayoutElement(
        RectTransform rectTransform,
        float minHeight = -1f,
        float preferredHeight = -1f,
        float flexibleWidth = -1f)
    {
        var layout = rectTransform.gameObject.AddComponent<LayoutElement>();

        if (minHeight >= 0f)
        {
            layout.minHeight = minHeight;
        }

        if (preferredHeight >= 0f)
        {
            layout.preferredHeight = preferredHeight;
        }

        if (flexibleWidth >= 0f)
        {
            layout.flexibleWidth = flexibleWidth;
        }

        return layout;
    }

    public static Text CreateText(
        string name,
        Transform parent,
        string content,
        int fontSize,
        Color color,
        TextAnchor anchor = TextAnchor.MiddleCenter,
        FontStyle fontStyle = FontStyle.Normal)
    {
        var rectTransform = CreateElement(name, parent);
        var text = rectTransform.gameObject.AddComponent<Text>();
        text.font = DefaultFont;
        text.text = content ?? string.Empty;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = anchor;
        text.fontStyle = fontStyle;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        AddLayoutElement(rectTransform);
        return text;
    }

    public static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Color backgroundColor,
        Color textColor)
    {
        var rectTransform = CreateElement(name, parent);
        AddLayoutElement(rectTransform, minHeight: 52f, preferredHeight: 52f, flexibleWidth: 1f);

        var image = rectTransform.gameObject.AddComponent<Image>();
        image.color = backgroundColor;
        image.sprite = DefaultSprite;
        image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;

        var button = rectTransform.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
        button.colors = colors;

        var labelText = CreateText(
            "Label",
            rectTransform,
            label,
            18,
            textColor,
            TextAnchor.MiddleCenter,
            FontStyle.Bold);
        Stretch(labelText.rectTransform, 12f, 8f);

        return button;
    }

    public static InputField CreateInputField(
        string name,
        Transform parent,
        string placeholderText,
        bool isPassword)
    {
        var rectTransform = CreateElement(name, parent);
        AddLayoutElement(rectTransform, minHeight: 52f, preferredHeight: 52f);

        var image = rectTransform.gameObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.08f);
        image.sprite = DefaultSprite;
        image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;

        var inputField = rectTransform.gameObject.AddComponent<InputField>();
        inputField.lineType = InputField.LineType.SingleLine;
        inputField.contentType = isPassword
            ? InputField.ContentType.Password
            : InputField.ContentType.Standard;
        inputField.targetGraphic = image;

        var textRect = CreateElement("Text", rectTransform);
        Stretch(textRect, 16f, 12f);

        var text = textRect.gameObject.AddComponent<Text>();
        text.font = DefaultFont;
        text.fontSize = 18;
        text.color = new Color(0.97f, 0.97f, 0.97f, 1f);
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        var placeholderRect = CreateElement("Placeholder", rectTransform);
        Stretch(placeholderRect, 16f, 12f);

        var placeholder = placeholderRect.gameObject.AddComponent<Text>();
        placeholder.font = DefaultFont;
        placeholder.fontSize = 18;
        placeholder.text = placeholderText ?? string.Empty;
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.supportRichText = false;
        placeholder.raycastTarget = false;

        inputField.textComponent = text;
        inputField.placeholder = placeholder;

        return inputField;
    }

    public static void Stretch(RectTransform rectTransform, float horizontalPadding = 0f, float verticalPadding = 0f)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(horizontalPadding, verticalPadding);
        rectTransform.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
    }
}
