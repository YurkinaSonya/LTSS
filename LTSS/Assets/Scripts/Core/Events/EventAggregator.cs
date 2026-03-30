using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Core.Events
{
    /// <summary>
    /// A lightweight in-process event bus.
    /// Provides decoupled communication between system components.
    /// </summary>
    public class EventAggregator : IEventAggregator
    {
        /// <summary>
        /// Stores subscribers grouped by event type, described in EventsProvider.
        /// </summary>
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

        /// <summary>
        /// Registers a handler for a specific event type.
        /// The same handler instance may be added multiple times.
        /// </summary>
        public void Subscribe<T>(Action<T> action)
        {
            if (!_subscribers.ContainsKey(typeof(T)))
                _subscribers[typeof(T)] = new List<Delegate>();

            _subscribers[typeof(T)].Add(action);
        }

        /// <summary>
        /// Unregisters a handler for a specific event type and 
        /// removes the event entry when no handlers remain.
        /// </summary>
        public void Unsubscribe<T>(Action<T> action)
        {
            if (_subscribers.ContainsKey(typeof(T)))
            {
                _subscribers[typeof(T)].Remove(action);

                if (_subscribers[typeof(T)].Count == 0)
                    _subscribers.Remove(typeof(T));
            }
        }

        /// <summary>
        /// Broadcasts an event to all subscribed handlers.
        /// Uses a snapshot of handlers to ensure safe iteration during modifications.
        /// </summary>
        public void Publish<T>(T eventData)
        {
            if (_subscribers.ContainsKey(typeof(T)))
            {
                var actions = _subscribers[typeof(T)].Cast<Action<T>>().ToList();

                foreach (var action in actions)
                {
                    try
                    {
                        action(eventData);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error in subscriber for event {typeof(T)}: {ex.Message}");
                    }
                }
            }
        }
    }
}
