using System;
using System.Collections.Generic;

namespace GameUp.Core
{
    /// <summary>
    /// Type-safe static event bus. Each event type T gets its own subscriber list.
    /// Subscribe returns an <see cref="EventBinding{T}"/> — dispose it to unsubscribe.
    /// </summary>
    public static class EventBus<T> where T : IEvent
    {
        static readonly List<EventBinding<T>> _bindings = new();

        /// <summary>Subscribe to events of type T. Dispose the returned binding to unsubscribe.</summary>
        public static EventBinding<T> Subscribe(Action<T> handler)
        {
            var binding = new EventBinding<T>(handler);
            _bindings.Add(binding);
            return binding;
        }

        /// <summary>Publish an event to all subscribers.</summary>
        public static void Publish(T evt)
        {
            for (int i = _bindings.Count - 1; i >= 0; i--)
                _bindings[i].Invoke(evt);
        }

        internal static void Unsubscribe(EventBinding<T> binding)
        {
            _bindings.Remove(binding);
        }

        /// <summary>Remove all subscribers. Useful for cleanup between scenes.</summary>
        public static void Clear()
        {
            _bindings.Clear();
        }
    }
}
