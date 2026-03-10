using System.Collections.Generic;
using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// MonoBehaviour-based object pool for prefab instances.
    /// Pooled objects are parented under this transform when inactive.
    /// </summary>
    public sealed class MonoPool : MonoBehaviour
    {
        [SerializeField] GameObject _prefab;
        [SerializeField] int _preWarmCount = 10;

        readonly Stack<GameObject> _pool = new();
        readonly HashSet<GameObject> _active = new();

        public int CountInactive => _pool.Count;
        public int CountActive => _active.Count;

        void Awake()
        {
            PreWarm(_preWarmCount);
        }

        public void Init(GameObject prefab, int preWarm = 10)
        {
            _prefab = prefab;
            _preWarmCount = preWarm;
            PreWarm(_preWarmCount);
        }

        void PreWarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var obj = CreateNew();
                obj.SetActive(false);
                _pool.Push(obj);
            }
        }

        /// <summary>Get an object from the pool. Activates and unparents it.</summary>
        public GameObject Get(Vector3 position = default, Quaternion rotation = default)
        {
            var obj = _pool.Count > 0 ? _pool.Pop() : CreateNew();
            obj.transform.SetParent(null);
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);
            _active.Add(obj);

            var poolables = obj.GetComponents<IPoolable>();
            foreach (var p in poolables) p.OnSpawn();

            return obj;
        }

        /// <summary>Return an object to the pool. Deactivates and reparents it.</summary>
        public void Release(GameObject obj)
        {
            if (obj == null) return;

            var poolables = obj.GetComponents<IPoolable>();
            foreach (var p in poolables) p.OnDespawn();

            obj.SetActive(false);
            obj.transform.SetParent(transform);
            _active.Remove(obj);
            _pool.Push(obj);
        }

        /// <summary>Release all active objects back to the pool.</summary>
        public void ReleaseAll()
        {
            var snapshot = new List<GameObject>(_active);
            foreach (var obj in snapshot)
                Release(obj);
        }

        GameObject CreateNew()
        {
            var obj = Instantiate(_prefab, transform);
            obj.name = _prefab.name;
            return obj;
        }
    }
}
