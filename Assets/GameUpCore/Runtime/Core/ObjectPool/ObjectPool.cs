using System;
using System.Collections.Generic;

namespace GameUp.Core
{
    /// <summary>
    /// Generic object pool for plain C# objects. Supports pre-warming and auto-expand.
    /// </summary>
    public sealed class ObjectPool<T> where T : class, new()
    {
        readonly Queue<T> _pool = new();
        readonly Action<T> _onGet;
        readonly Action<T> _onRelease;

        public int CountInactive => _pool.Count;

        public ObjectPool(int preWarm = 0, Action<T> onGet = null, Action<T> onRelease = null)
        {
            _onGet = onGet;
            _onRelease = onRelease;
            for (int i = 0; i < preWarm; i++)
                _pool.Enqueue(new T());
        }

        /// <summary>Get an object from the pool or create a new one if empty.</summary>
        public T Get()
        {
            var item = _pool.Count > 0 ? _pool.Dequeue() : new T();
            if (item is IPoolable poolable) poolable.OnSpawn();
            _onGet?.Invoke(item);
            return item;
        }

        /// <summary>Return an object to the pool.</summary>
        public void Release(T item)
        {
            if (item == null) return;
            if (item is IPoolable poolable) poolable.OnDespawn();
            _onRelease?.Invoke(item);
            _pool.Enqueue(item);
        }

        public void Clear() => _pool.Clear();
    }
}
