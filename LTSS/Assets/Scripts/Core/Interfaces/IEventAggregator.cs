using System.Collections.Generic;
using System;

namespace Game.Core
{
    /// <summary>
    /// Part of system for independence managing events.
    /// </summary>
    public interface IEventAggregator
    {
        void Subscribe<T>(Action<T> action);
        void Unsubscribe<T>(Action<T> action);
        void Publish<T>(T eventData);

    }
}