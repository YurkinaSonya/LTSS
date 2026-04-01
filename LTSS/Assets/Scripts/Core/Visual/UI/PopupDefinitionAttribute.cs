using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PopupDefinitionAttribute : Attribute
{
    public Enums.PopupType PopupType { get; }

    public PopupDefinitionAttribute(Enums.PopupType popupType)
    {
        PopupType = popupType;
    }
}
