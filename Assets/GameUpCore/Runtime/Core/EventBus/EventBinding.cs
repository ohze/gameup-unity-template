using System;

namespace GameUp.Core
{
    /// <summary>
    /// Handle returned by EventBus subscription. Dispose to unsubscribe.
    /// </summary>
    public sealed class EventBinding<T> : IDisposable where T : IEvent
    {
        readonly Action<T> _handler;
        bool _disposed;

        internal EventBinding(Action<T> handler)
        {
            _handler = handler;
        }

        internal void Invoke(T evt)
        {
            if (!_disposed)
                _handler?.Invoke(evt);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            EventBus<T>.Unsubscribe(this);
        }
    }
}
