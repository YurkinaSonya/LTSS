using System;
using Game.Core.Application;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ScreenDefinitionAttribute : Attribute
{
    public ScreenId ScreenId { get; }

    public ScreenDefinitionAttribute(ScreenId screenId)
    {
        ScreenId = screenId;
    }
}
