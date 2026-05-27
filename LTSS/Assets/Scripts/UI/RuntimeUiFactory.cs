using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class RuntimeUiFactory
{
    private const string RegularFontResourcePath = "Fonts/Inter-Regular";
    private const string BoldFontResourcePath = "Fonts/Inter-Bold";

    public static readonly Color BackgroundColor = FromHex("EEF1FA");
    public static readonly Color SurfaceColor = FromHex("F8F9FE");
    public static readonly Color ElevatedSurfaceColor = FromHex("FFFFFF");
    public static readonly Color PrimaryColor = FromHex("848CC4");
    public static readonly Color PrimaryPressedColor = FromHex("737CB6");
    public static readonly Color PrimarySoftColor = FromHex("E3E7F8");
    public static readonly Color TextPrimaryColor = FromHex("2A2F45");
    public static readonly Color TextSecondaryColor = FromHex("6D7392");
    public static readonly Color BorderColor = FromHex("CBD2EA");
    public static readonly Color DangerColor = FromHex("C15D74");

    private static Font _defaultFont;
    private static Font _boldFont;

    public static Font DefaultFont
    {
        get
        {
            if (_defaultFont == null)
            {
                _defaultFont = Resources.Load<Font>(RegularFontResourcePath);

                if (_defaultFont == null)
                {
                    _defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
            }

            return _defaultFont;
        }
    }

    public static Font BoldFont
    {
        get
        {
            if (_boldFont == null)
            {
                _boldFont = Resources.Load<Font>(BoldFontResourcePath);

                if (_boldFont == null)
                {
                    _boldFont = DefaultFont;
                }
            }

            return _boldFont;
        }
    }

    public static RectTransform CreateScreenBackground(Transform parent)
    {
        var background = CreateRect("Background", parent);
        Stretch(background);

        var image = background.gameObject.AddComponent<Image>();
        image.color = BackgroundColor;
        image.raycastTarget = false;

        return background;
    }

    public static RectTransform CreateCard(
        string name,
        Transform parent,
        Vector2 size,
        bool accent = true)
    {
        var card = CreateRect(name, parent);
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = size;
        card.anchoredPosition = Vector2.zero;

        var image = card.gameObject.AddComponent<Image>();
        image.color = SurfaceColor;
        image.raycastTarget = false;

        var shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.2f, 0.24f, 0.36f, 0.08f);
        shadow.effectDistance = new Vector2(0f, -6f);

        var outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        if (accent)
        {
            var accentRect = CreateRect("Accent", card);
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(0f, 6f);
            accentRect.anchoredPosition = Vector2.zero;

            var accentImage = accentRect.gameObject.AddComponent<Image>();
            accentImage.color = PrimaryColor;
            accentImage.raycastTarget = false;
        }

        return card;
    }

    public static RectTransform CreateContentRoot(
        string name,
        Transform parent,
        RectOffset padding,
        float spacing,
        TextAnchor alignment = TextAnchor.UpperLeft)
    {
        var content = CreateRect(name, parent);
        Stretch(
            content,
            padding != null ? padding.left : 0,
            padding != null ? padding.right : 0,
            padding != null ? padding.top : 0,
            padding != null ? padding.bottom : 0);

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return content;
    }

    public static RectTransform CreatePanel(
        string name,
        Transform parent,
        RectOffset padding,
        float spacing,
        Color? backgroundColor = null)
    {
        var panel = CreateRect(name, parent);

        var image = panel.gameObject.AddComponent<Image>();
        image.color = backgroundColor ?? PrimarySoftColor;
        image.raycastTarget = false;

        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = padding ?? new RectOffset();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return panel;
    }

    public static RectTransform CreateRow(
        string name,
        Transform parent,
        float spacing,
        TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var row = CreateRect(name, parent);

        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return row;
    }

    public static Text CreateTitle(Transform parent, string value, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        return CreateText(parent, value, 31, TextPrimaryColor, alignment, FontStyle.Bold);
    }

    public static Text CreateBodyText(
        Transform parent,
        string value,
        TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        return CreateText(parent, value, 17, TextPrimaryColor, alignment, FontStyle.Normal);
    }

    public static Text CreateCaption(
        Transform parent,
        string value,
        TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        return CreateText(parent, value, 15, TextSecondaryColor, alignment, FontStyle.Normal);
    }

    public static Text CreateErrorText(Transform parent)
    {
        var label = CreateText(parent, string.Empty, 15, DangerColor, TextAnchor.MiddleCenter, FontStyle.Normal);
        label.gameObject.SetActive(false);
        return label;
    }

    public static Button CreatePrimaryButton(Transform parent, string text)
    {
        return CreatePrimaryButton(parent, text, 54f);
    }

    public static Button CreatePrimaryButton(Transform parent, string text, float height)
    {
        return CreateButton(parent, text, PrimaryColor, Color.white, false, height);
    }

    public static Button CreateSecondaryButton(Transform parent, string text)
    {
        return CreateSecondaryButton(parent, text, 54f);
    }

    public static Button CreateSecondaryButton(Transform parent, string text, float height)
    {
        return CreateButton(parent, text, PrimarySoftColor, TextPrimaryColor, true, height);
    }

    public static InputField CreateInputField(
        Transform parent,
        string placeholder,
        bool isPassword = false)
    {
        var fieldRect = CreateRect("Input", parent);

        var layoutElement = fieldRect.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 52f;

        var image = fieldRect.gameObject.AddComponent<Image>();
        image.color = ElevatedSurfaceColor;

        var outline = fieldRect.gameObject.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        var input = fieldRect.gameObject.AddComponent<InputField>();
        input.targetGraphic = image;
        input.lineType = InputField.LineType.SingleLine;
        input.customCaretColor = true;
        input.caretColor = TextPrimaryColor;
        input.selectionColor = new Color(PrimaryColor.r, PrimaryColor.g, PrimaryColor.b, 0.25f);
        input.contentType = isPassword
            ? InputField.ContentType.Password
            : InputField.ContentType.Standard;

        var textRect = CreateRect("Text", fieldRect);
        Stretch(textRect, 16f, 16f, 12f, 12f);

        var textComponent = textRect.gameObject.AddComponent<Text>();
        textComponent.font = DefaultFont;
        textComponent.fontSize = 18;
        textComponent.color = TextPrimaryColor;
        textComponent.alignment = TextAnchor.MiddleLeft;
        textComponent.supportRichText = false;
        textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
        textComponent.verticalOverflow = VerticalWrapMode.Overflow;
        textComponent.raycastTarget = false;

        var placeholderRect = CreateRect("Placeholder", fieldRect);
        Stretch(placeholderRect, 16f, 16f, 12f, 12f);

        var placeholderText = placeholderRect.gameObject.AddComponent<Text>();
        placeholderText.font = DefaultFont;
        placeholderText.fontSize = 18;
        placeholderText.color = new Color(
            TextSecondaryColor.r,
            TextSecondaryColor.g,
            TextSecondaryColor.b,
            0.7f);
        placeholderText.alignment = TextAnchor.MiddleLeft;
        placeholderText.text = placeholder;
        placeholderText.supportRichText = false;
        placeholderText.horizontalOverflow = HorizontalWrapMode.Overflow;
        placeholderText.verticalOverflow = VerticalWrapMode.Overflow;
        placeholderText.raycastTarget = false;

        input.textComponent = textComponent;
        input.placeholder = placeholderText;

        return input;
    }

    public static Dropdown CreateDropdown(Transform parent, float height = 52f)
    {
        const float dropdownItemHeight = 34f;

        var dropdownRect = CreateRect("Dropdown", parent);

        var layoutElement = dropdownRect.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;

        var image = dropdownRect.gameObject.AddComponent<Image>();
        image.color = ElevatedSurfaceColor;

        var outline = dropdownRect.gameObject.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        var dropdown = dropdownRect.gameObject.AddComponent<RuntimeDropdown>();
        dropdown.targetGraphic = image;

        var labelRect = CreateRect("Label", dropdownRect);
        Stretch(labelRect, 16f, 36f, 0f, 0f);

        var label = labelRect.gameObject.AddComponent<Text>();
        label.font = DefaultFont;
        label.fontSize = 17;
        label.fontStyle = FontStyle.Normal;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = TextPrimaryColor;
        label.supportRichText = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;

        var arrowRect = CreateRect("Arrow", dropdownRect);
        arrowRect.anchorMin = new Vector2(1f, 0.5f);
        arrowRect.anchorMax = new Vector2(1f, 0.5f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.sizeDelta = new Vector2(20f, 20f);
        arrowRect.anchoredPosition = new Vector2(-12f, 0f);

        var arrow = arrowRect.gameObject.AddComponent<Text>();
        arrow.font = BoldFont;
        arrow.fontSize = 14;
        arrow.fontStyle = FontStyle.Normal;
        arrow.alignment = TextAnchor.MiddleCenter;
        arrow.color = TextSecondaryColor;
        arrow.text = "\u25BE";
        arrow.supportRichText = false;
        arrow.raycastTarget = false;

        var templateRect = CreateRect("Template", dropdownRect);
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, -2f);
        templateRect.sizeDelta = new Vector2(0f, dropdownItemHeight + 6f);
        templateRect.gameObject.SetActive(false);

        var templateImage = templateRect.gameObject.AddComponent<Image>();
        templateImage.color = SurfaceColor;

        var templateOutline = templateRect.gameObject.AddComponent<Outline>();
        templateOutline.effectColor = BorderColor;
        templateOutline.effectDistance = new Vector2(1f, -1f);

        var viewportRect = CreateRect("Viewport", templateRect);
        Stretch(viewportRect, 2f, 2f, 2f, 2f);

        var contentRect = CreateRect("Content", viewportRect);
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, dropdownItemHeight);

        var contentLayout = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 0f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        var itemRect = CreateRect("Item", contentRect);
        var itemLayout = itemRect.gameObject.AddComponent<LayoutElement>();
        itemLayout.preferredHeight = dropdownItemHeight;

        var itemBackground = itemRect.gameObject.AddComponent<Image>();
        itemBackground.color = ElevatedSurfaceColor;

        var itemToggle = itemRect.gameObject.AddComponent<Toggle>();
        itemToggle.targetGraphic = itemBackground;
        itemToggle.graphic = null;

        var itemColors = itemToggle.colors;
        itemColors.normalColor = ElevatedSurfaceColor;
        itemColors.highlightedColor = PrimarySoftColor;
        itemColors.pressedColor = PrimarySoftColor;
        itemColors.selectedColor = PrimarySoftColor;
        itemColors.disabledColor = new Color(ElevatedSurfaceColor.r, ElevatedSurfaceColor.g, ElevatedSurfaceColor.b, 0.55f);
        itemToggle.colors = itemColors;

        var itemLabelRect = CreateRect("Item Label", itemRect);
        Stretch(itemLabelRect, 16f, 16f, 0f, 0f);

        var itemLabel = itemLabelRect.gameObject.AddComponent<Text>();
        itemLabel.font = DefaultFont;
        itemLabel.fontSize = 17;
        itemLabel.fontStyle = FontStyle.Normal;
        itemLabel.alignment = TextAnchor.MiddleLeft;
        itemLabel.color = TextPrimaryColor;
        itemLabel.supportRichText = false;
        itemLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        itemLabel.verticalOverflow = VerticalWrapMode.Overflow;
        itemLabel.raycastTarget = false;

        dropdown.BindRuntimeItemTemplate(itemToggle, itemLabel);
        dropdown.ConfigureRuntimeLayout(templateRect, contentRect, dropdownItemHeight);

        dropdown.template = templateRect;
        dropdown.captionText = label;
        dropdown.itemText = itemLabel;

        var colors = dropdown.colors;
        colors.normalColor = ElevatedSurfaceColor;
        colors.highlightedColor = Color.Lerp(ElevatedSurfaceColor, Color.white, 0.05f);
        colors.pressedColor = PrimarySoftColor;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(ElevatedSurfaceColor.r, ElevatedSurfaceColor.g, ElevatedSurfaceColor.b, 0.55f);
        dropdown.colors = colors;

        return dropdown;
    }

    public static void SetDropdownOptions(Dropdown dropdown, IReadOnlyList<string> options, int selectedIndex)
    {
        if (dropdown == null)
        {
            return;
        }

        dropdown.ClearOptions();

        var optionData = new List<Dropdown.OptionData>();

        if (options != null)
        {
            for (var index = 0; index < options.Count; index++)
            {
                optionData.Add(new Dropdown.OptionData(options[index] ?? string.Empty));
            }
        }

        if (optionData.Count == 0)
        {
            optionData.Add(new Dropdown.OptionData(string.Empty));
        }

        dropdown.AddOptions(optionData);
        dropdown.SetValueWithoutNotify(Mathf.Clamp(selectedIndex, 0, optionData.Count - 1));
        dropdown.RefreshShownValue();

        if (dropdown is RuntimeDropdown runtimeDropdown)
        {
            runtimeDropdown.RefreshTemplateLayout(optionData.Count);
        }
    }

    public static void AddSpacer(Transform parent, float height)
    {
        var spacer = CreateRect("Spacer", parent);
        var layoutElement = spacer.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        layoutElement.flexibleHeight = 0f;
    }

    public static void AddFlexibleSpacer(Transform parent)
    {
        var spacer = CreateRect("FlexibleSpacer", parent);
        var layoutElement = spacer.gameObject.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 1f;
    }

    public static Text CreateValueText(
        Transform parent,
        string value,
        int fontSize = 32,
        TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        return CreateText(parent, value, fontSize, TextPrimaryColor, alignment, FontStyle.Bold);
    }

    public static RectTransform CreateSurface(
        string name,
        Transform parent,
        Color? backgroundColor = null,
        bool outlined = true)
    {
        var rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = backgroundColor ?? SurfaceColor;
        image.raycastTarget = false;

        if (outlined)
        {
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        return rect;
    }

    public static void SetButtonText(Button button, string text)
    {
        if (button == null)
        {
            return;
        }

        var label = button.GetComponentInChildren<Text>(true);

        if (label != null)
        {
            label.text = text ?? string.Empty;
        }
    }

    public static void ApplyTextStyle(Text label, FontStyle fontStyle)
    {
        if (label == null)
        {
            return;
        }

        label.font = ResolveFont(fontStyle);
        label.fontStyle = NormalizeFontStyle(fontStyle);
    }

    private static Button CreateButton(
        Transform parent,
        string text,
        Color backgroundColor,
        Color textColor,
        bool outlined,
        float height)
    {
        var buttonRect = CreateRect("Button", parent);
        var layoutElement = buttonRect.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = height;
        layoutElement.flexibleWidth = 1f;

        var image = buttonRect.gameObject.AddComponent<Image>();
        image.color = backgroundColor;

        if (outlined)
        {
            var outline = buttonRect.gameObject.AddComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        var button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = Color.Lerp(backgroundColor, Color.white, 0.05f);
        colors.pressedColor = outlined
            ? Color.Lerp(backgroundColor, PrimaryColor, 0.15f)
            : PrimaryPressedColor;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 0.55f);
        button.colors = colors;

        var labelRect = CreateRect("Label", buttonRect);
        Stretch(labelRect, 14f, 14f, 0f, 0f);

        var label = labelRect.gameObject.AddComponent<Text>();
        label.font = BoldFont;
        label.fontSize = 18;
        label.fontStyle = FontStyle.Normal;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = textColor;
        label.text = text ?? string.Empty;
        label.supportRichText = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;

        return button;
    }

    private static Text CreateText(
        Transform parent,
        string value,
        int fontSize,
        Color color,
        TextAnchor alignment,
        FontStyle fontStyle)
    {
        var labelRect = CreateRect("Text", parent);

        var label = labelRect.gameObject.AddComponent<Text>();
        label.font = ResolveFont(fontStyle);
        label.fontSize = fontSize;
        label.fontStyle = NormalizeFontStyle(fontStyle);
        label.alignment = alignment;
        label.color = color;
        label.text = value ?? string.Empty;
        label.supportRichText = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;

        return label;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static void Stretch(
        RectTransform rectTransform,
        float left = 0f,
        float right = 0f,
        float top = 0f,
        float bottom = 0f)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, -top);
        rectTransform.localScale = Vector3.one;
        rectTransform.anchoredPosition = Vector2.zero;
    }

    private static Color FromHex(string hex)
    {
        if (!hex.StartsWith("#"))
        {
            hex = "#" + hex;
        }

        return ColorUtility.TryParseHtmlString(hex, out var color)
            ? color
            : Color.white;
    }

    private static Font ResolveFont(FontStyle fontStyle)
    {
        switch (fontStyle)
        {
            case FontStyle.Bold:
            case FontStyle.BoldAndItalic:
                return BoldFont != null ? BoldFont : DefaultFont;
            default:
                return DefaultFont;
        }
    }

    private static FontStyle NormalizeFontStyle(FontStyle fontStyle)
    {
        switch (fontStyle)
        {
            case FontStyle.Bold:
            case FontStyle.BoldAndItalic:
                return FontStyle.Normal;
            default:
                return fontStyle;
        }
    }
}
