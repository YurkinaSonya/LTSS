using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public sealed class RuntimeDropdown : Dropdown
{
    private const float TemplateVerticalPadding = 6f;

    private static readonly Type DropdownItemType = typeof(Dropdown).GetNestedType(
        "DropdownItem",
        BindingFlags.NonPublic);

    private RectTransform _templateRect;
    private RectTransform _contentRect;
    private float _itemHeight;

    public void BindRuntimeItemTemplate(Toggle itemToggle, Text itemText, Image itemImage = null)
    {
        if (itemToggle == null || DropdownItemType == null)
        {
            return;
        }

        var existingItems = itemToggle.gameObject.GetComponents(DropdownItemType);
        Component dropdownItem = null;

        if (existingItems != null && existingItems.Length > 0)
        {
            dropdownItem = existingItems[0];

            for (var index = 1; index < existingItems.Length; index++)
            {
                if (Application.isPlaying)
                {
                    Destroy(existingItems[index]);
                }
                else
                {
                    DestroyImmediate(existingItems[index]);
                }
            }
        }
        else
        {
            dropdownItem = itemToggle.gameObject.AddComponent(DropdownItemType);
        }

        SetMember(dropdownItem, "text", itemText);
        SetMember(dropdownItem, "image", itemImage);
        SetMember(dropdownItem, "toggle", itemToggle);
        SetMember(dropdownItem, "rectTransform", itemToggle.transform as RectTransform);
    }

    public void ConfigureRuntimeLayout(
        RectTransform templateRect,
        RectTransform contentRect,
        float itemHeight)
    {
        _templateRect = templateRect;
        _contentRect = contentRect;
        _itemHeight = Mathf.Max(1f, itemHeight);
        RefreshTemplateLayout(options != null ? options.Count : 0);
    }

    public void RefreshTemplateLayout(int optionCount)
    {
        if (_templateRect == null || _contentRect == null)
        {
            return;
        }

        var normalizedCount = Mathf.Max(1, optionCount);
        var contentHeight = normalizedCount * _itemHeight;

        _contentRect.sizeDelta = new Vector2(_contentRect.sizeDelta.x, contentHeight);
        _templateRect.sizeDelta = new Vector2(
            _templateRect.sizeDelta.x,
            contentHeight + TemplateVerticalPadding);
    }

    private static void SetMember(Component target, string memberName, object value)
    {
        if (target == null || string.IsNullOrWhiteSpace(memberName))
        {
            return;
        }

        var targetType = target.GetType();
        var field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        var property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value, null);
        }
    }
}
