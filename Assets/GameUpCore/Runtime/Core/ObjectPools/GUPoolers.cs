using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace GameUp.Core
{
    /// <summary>
    /// Object pool: lấy/trả object trong O(1) nhờ stack các clone đang rảnh.
    /// Mọi clone được quản lý trong <see cref="CloneInfo"/> nên không còn dictionary rác khi đổi scene.
    /// </summary>
    public class GUPoolers : MonoSingleton<GUPoolers>
    {
        private const string SUFFIX = "_Pool";

        /// <summary>Thông tin gắn với từng clone: pool gốc, scale ban đầu, đang rảnh hay không.</summary>
        private class CloneInfo
        {
            public GameObject PrefabKey;
            public Vector3 DefaultLocalScale;
            public bool IsFree;
            public IPoolable[] Poolables;
        }

        private class Pool
        {
            public Transform Holder;

            /// <summary>Toàn bộ clone thuộc pool (kể cả đang dùng).</summary>
            public readonly List<GameObject> Members = new();

            /// <summary>Clone đang rảnh — lấy ra O(1), không cần quét danh sách.</summary>
            public readonly Stack<GameObject> Free = new();
        }

        private readonly Dictionary<GameObject, Pool> _pools = new();
        private readonly Dictionary<GameObject, CloneInfo> _clones = new();
        private readonly List<GameObject> _iterationBuffer = new();

        private Transform _cacheTrs;

        protected override void Awake()
        {
            base.Awake();
            _cacheTrs = transform;

            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            Prune();
        }

        #region Public API

        /// <summary>
        /// Tạo sẵn <paramref name="count"/> instance để lần Spawn đầu không bị khựng.
        /// Gọi ở màn Loading cho các prefab bắn ra nhiều (đạn, hiệu ứng, item list...).
        /// </summary>
        public void Prewarm(GameObject prefab, int count)
        {
            if (!prefab)
            {
                GULogger.Error("GUPool", "Prewarm called with a null prefab.");
                return;
            }

            if (count <= 0) return;

            var key = GetPoolKey(prefab);
            var pool = GetOrCreatePool(key);

            for (var i = 0; i < count; i++)
            {
                var clone = Instantiate(key, pool.Holder);
                Register(pool, clone, key);
                clone.Hide();

                var info = _clones[clone];
                info.IsFree = true;
                pool.Free.Push(clone);
            }
        }

        public T Spawn<T>(T go, Transform parent = null, bool worldPositionStays = false) where T : Component
        {
            if (!go) return null;
            var clone = Acquire(go.gameObject, parent, worldPositionStays, false, default, default);
            return clone ? clone.GetComponent<T>() : null;
        }

        public T Spawn<T>(T go, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            if (!go) return null;
            var clone = Acquire(go.gameObject, parent, true, true, position, rotation);
            return clone ? clone.GetComponent<T>() : null;
        }

        public GameObject Spawn(GameObject go, Transform parent = null, bool worldPositionStays = false)
        {
            return Acquire(go, parent, worldPositionStays, false, default, default);
        }

        public GameObject Spawn(GameObject go, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            return Acquire(go, parent, true, true, position, rotation);
        }

        public void DeSpawn<T>(T go) where T : Component
        {
            if (go) DeSpawn(go.gameObject);
        }

        public void DeSpawn(GameObject go)
        {
            if (!go) return;

            if (!_clones.TryGetValue(go, out var info))
            {
                // Không thuộc pool nào — giữ hành vi cũ: chỉ tắt object.
                go.Hide();
                return;
            }

            if (info.IsFree) return;

            NotifyDespawn(info);
            go.Hide();

            if (_pools.TryGetValue(info.PrefabKey, out var pool))
            {
                if (pool.Holder) SetParentByContext(go.transform, pool.Holder, true);
                pool.Free.Push(go);
            }

            info.IsFree = true;
        }

        public void DeSpawn<T>(T go, float timeDelay) where T : Component
        {
            if (!go) return;
            if (timeDelay > 0) this.Delay(timeDelay, () => DeSpawn(go));
            else DeSpawn(go);
        }

        public void DeSpawn(GameObject go, float timeDelay)
        {
            if (!go) return;
            if (timeDelay > 0) this.Delay(timeDelay, () => DeSpawn(go));
            else DeSpawn(go);
        }

        public void DeSpawnAll<T>(T go) where T : Component
        {
            if (go) DeSpawnAll(go.gameObject);
        }

        public void DeSpawnAll(GameObject go)
        {
            if (!go) return;

            var key = GetPoolKey(go);
            if (!key || !_pools.TryGetValue(key, out var pool)) return;

            // Duyệt ngược để an toàn nếu callback IPoolable gọi DestroyObject làm ngắn danh sách.
            for (var i = pool.Members.Count - 1; i >= 0; i--)
            {
                if (i >= pool.Members.Count) continue;
                DeSpawn(pool.Members[i]);
            }
        }

        /// <summary>Hủy hẳn một clone khỏi pool (không tái sử dụng nữa).</summary>
        public void DestroyObject(GameObject go)
        {
            if (!go) return;
            if (!_clones.TryGetValue(go, out var info)) return;

            Unregister(go, info);
            Destroy(go);
        }

        public void DestroyObject<T>(T go) where T : Component
        {
            if (go) DestroyObject(go.gameObject);
        }

        /// <summary>
        /// Dọn các clone đã bị Destroy ngoài tầm kiểm soát của pool (đổi scene, Destroy thủ công).
        /// Tự chạy sau mỗi lần unload scene.
        /// </summary>
        public void Prune()
        {
            _iterationBuffer.Clear();
            foreach (var pair in _clones)
            {
                if (!pair.Key) _iterationBuffer.Add(pair.Key);
            }

            for (var i = 0; i < _iterationBuffer.Count; i++)
            {
                _clones.Remove(_iterationBuffer[i]);
            }

            _iterationBuffer.Clear();

            var staleKeys = new List<GameObject>();
            foreach (var pair in _pools)
            {
                var pool = pair.Value;
                pool.Members.RemoveAll(member => !member);
                RebuildFreeStack(pool);

                if (!pair.Key || (pool.Members.Count == 0 && !pool.Holder)) staleKeys.Add(pair.Key);
            }

            for (var i = 0; i < staleKeys.Count; i++)
            {
                _pools.Remove(staleKeys[i]);
            }
        }

        #endregion

        #region Internal

        private GameObject Acquire(GameObject prefabOrClone, Transform parent, bool worldPositionStays,
            bool hasPose, Vector3 position, Quaternion rotation)
        {
            if (!prefabOrClone)
            {
                GULogger.Error("GUPool", "Attempting to spawn a null prefab.");
                return null;
            }

            var key = GetPoolKey(prefabOrClone);
            var pool = GetOrCreatePool(key);
            var target = parent ? parent : pool.Holder;

            var clone = PopFree(pool);
            if (clone)
            {
                SetParentByContext(clone.transform, target, hasPose || worldPositionStays);
                RestoreDefaultScale(clone);

                if (hasPose) clone.transform.SetPositionAndRotation(position, rotation);
                else ResetSpawnedTransform(clone.transform, parent);

                clone.Show();
            }
            else
            {
                clone = hasPose
                    ? Instantiate(key, position, rotation, target)
                    : Instantiate(key, target, worldPositionStays);

                Register(pool, clone, key);
                if (!hasPose) ResetSpawnedTransform(clone.transform, parent);
                if (!clone.activeSelf) clone.Show();
            }

            _clones[clone].IsFree = false;
            NotifySpawn(_clones[clone]);
            return clone;
        }

        /// <summary>Lấy clone rảnh, bỏ qua những entry đã bị Destroy còn sót trong stack.</summary>
        private GameObject PopFree(Pool pool)
        {
            while (pool.Free.Count > 0)
            {
                var clone = pool.Free.Pop();
                if (clone) return clone;
            }

            return null;
        }

        private Pool GetOrCreatePool(GameObject prefabKey)
        {
            if (_pools.TryGetValue(prefabKey, out var pool) && pool.Holder) return pool;

            if (pool == null)
            {
                pool = new Pool();
                _pools[prefabKey] = pool;
            }

            var holder = new GameObject($"{prefabKey.name}{SUFFIX}").transform;
            holder.SetParent(_cacheTrs ? _cacheTrs : transform);
            pool.Holder = holder;
            return pool;
        }

        private void Register(Pool pool, GameObject clone, GameObject prefabKey)
        {
            pool.Members.Add(clone);
            _clones[clone] = new CloneInfo
            {
                PrefabKey = prefabKey,
                DefaultLocalScale = clone.transform.localScale,
                IsFree = false,
                Poolables = clone.GetComponentsInChildren<IPoolable>(true)
            };
        }

        private void Unregister(GameObject clone, CloneInfo info)
        {
            _clones.Remove(clone);

            if (!_pools.TryGetValue(info.PrefabKey, out var pool)) return;

            pool.Members.Remove(clone);
            if (info.IsFree) RebuildFreeStack(pool);
        }

        /// <summary>Dựng lại stack rảnh từ Members — chỉ chạy khi có object bị hủy ngoài luồng.</summary>
        private void RebuildFreeStack(Pool pool)
        {
            pool.Free.Clear();
            for (var i = 0; i < pool.Members.Count; i++)
            {
                var member = pool.Members[i];
                if (!member) continue;
                if (_clones.TryGetValue(member, out var info) && info.IsFree) pool.Free.Push(member);
            }
        }

        private static void NotifySpawn(CloneInfo info)
        {
            var poolables = info.Poolables;
            if (poolables == null) return;

            for (var i = 0; i < poolables.Length; i++)
            {
                poolables[i]?.OnSpawn();
            }
        }

        private static void NotifyDespawn(CloneInfo info)
        {
            var poolables = info.Poolables;
            if (poolables == null) return;

            for (var i = 0; i < poolables.Length; i++)
            {
                poolables[i]?.OnDespawn();
            }
        }

        /// <summary>
        /// Nếu truyền clone (vd objA1) thì trả về prefab gốc (objA) để mọi instance dùng chung một pool.
        /// </summary>
        private GameObject GetPoolKey(GameObject prefabOrClone)
        {
            if (!prefabOrClone) return null;
            return _clones.TryGetValue(prefabOrClone, out var info) && info.PrefabKey
                ? info.PrefabKey
                : prefabOrClone;
        }

        private void RestoreDefaultScale(GameObject clone)
        {
            if (!clone) return;
            if (_clones.TryGetValue(clone, out var info)) clone.transform.localScale = info.DefaultLocalScale;
        }

        private static bool IsUiTransform(Transform target)
        {
            return target is RectTransform;
        }

        private static void SetParentByContext(Transform child, Transform parent, bool worldPositionStays)
        {
            if (!child || !parent) return;
            var shouldUseLocalSpace = IsUiTransform(child) || IsUiTransform(parent);
            child.SetParent(parent, !shouldUseLocalSpace && worldPositionStays);
        }

        private static void ResetSpawnedTransform(Transform spawnedTransform, bool hasParent)
        {
            if (!spawnedTransform || !hasParent) return;

            if (spawnedTransform is RectTransform rectTransform)
            {
                rectTransform.anchoredPosition3D = Vector3.zero;
                rectTransform.localRotation = Quaternion.identity;
                return;
            }

            spawnedTransform.localPosition = Vector3.zero;
            spawnedTransform.localRotation = Quaternion.identity;
        }

        #endregion
    }
}
