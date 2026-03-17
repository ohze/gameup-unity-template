using UnityEngine;
using System.Collections.Generic;


namespace GameUp.Core
{
    public class GUPoolers : MonoSingleton<GUPoolers>
    {
        private const string SUFFIX = "_Pool";
        private readonly Dictionary<GameObject, List<GameObject>> _gameObjectPools = new();
        private readonly Dictionary<GameObject, Transform> _parentPools = new();
        /// <summary> Clone -> prefab key, dùng để GetKeyFromClone O(1) thay vì duyệt toàn bộ pool. </summary>
        private readonly Dictionary<GameObject, GameObject> _cloneToPrefabKey = new();

        private Transform _cacheTrs;

        protected override void Awake()
        {
            base.Awake();
            _cacheTrs = transform;
        }

        private void CleanNullEntries(GameObject currentKey)
        {
            if (!currentKey) return;
            if (!_gameObjectPools.TryGetValue(currentKey, out var list)) return;
            list.RemoveAll(item => !item);
            if (list.Count == 0)
            {
                _gameObjectPools.Remove(currentKey);
                if (_parentPools.TryGetValue(currentKey, out var holder))
                {
                    _parentPools.Remove(currentKey);
                    if (holder) Destroy(holder.gameObject);
                }
            }
        }

        private void RegisterClone(GameObject clone, GameObject prefabKey)
        {
            _cloneToPrefabKey[clone] = prefabKey;
        }

        private void UnregisterClone(GameObject clone)
        {
            _cloneToPrefabKey.Remove(clone);
        }

        /// <summary>
        /// Nếu truyền clone (vd objA1) thì trả về prefab gốc (objA) để mọi instance cùng một pool.
        /// Spawn(objA) và Spawn(objA1) đều dùng chung pool(objA).
        /// </summary>
        private GameObject GetPoolKey(GameObject prefabOrClone)
        {
            if (!prefabOrClone) return null;
            var key = GetKeyFromClone(prefabOrClone);
            return key ? key : prefabOrClone;
        }

        /// <summary>
        /// Lấy pool và parent đã có, hoặc tạo mới một lần — tránh tạo List/Transform thừa.
        /// </summary>
        private (List<GameObject> list, Transform poolParent) GetOrCreatePool(GameObject prefabKey)
        {
            if (_gameObjectPools.TryGetValue(prefabKey, out var list) && _parentPools.TryGetValue(prefabKey, out var poolParent))
                return (list, poolParent);

            var holder = new GameObject($"{prefabKey.name}{SUFFIX}").transform;
            holder.SetParent(_cacheTrs);
            var newList = new List<GameObject>();
            _parentPools.Add(prefabKey, holder);
            _gameObjectPools.Add(prefabKey, newList);
            return (newList, holder);
        }

        /// <summary>
        /// Tìm object đang inactive trong pool để tái sử dụng — đúng bản chất pooling.
        /// </summary>
        private static GameObject TryGetFirstInactive(List<GameObject> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o && !o.activeSelf) return o;
            }
            return null;
        }

        public T Spawn<T>(T go, Transform parent = null, bool worldPositionStays = false) where T : Component
        {
            var poolKey = GetPoolKey(go.gameObject);
            if (!poolKey) return null;
            CleanNullEntries(poolKey);
            var (list, poolParent) = GetOrCreatePool(poolKey);
            var inactive = TryGetFirstInactive(list);
            if (inactive)
            {
                inactive.transform.SetParent(parent ?? poolParent, worldPositionStays);
                inactive.Show();
                if (parent)
                {
                    inactive.transform.position = Vector3.zero;
                    inactive.transform.rotation = Quaternion.identity;
                }
                return inactive.GetComponent<T>();
            }
            var item = Instantiate(go, parent ?? poolParent, worldPositionStays);
            list.Add(item.gameObject);
            RegisterClone(item.gameObject, poolKey);
            return item;
        }

        public T Spawn<T>(T go, Vector3 position, Quaternion rotation, Transform parent = null)
            where T : Component
        {
            var poolKey = GetPoolKey(go.gameObject);
            if (!poolKey) return null;
            CleanNullEntries(poolKey);
            var (list, poolParent) = GetOrCreatePool(poolKey);
            var inactive = TryGetFirstInactive(list);
            if (inactive)
            {
                inactive.transform.SetParent(parent ?? poolParent);
                inactive.transform.position = position;
                inactive.transform.rotation = rotation;
                inactive.Show();
                return inactive.GetComponent<T>();
            }
            var item = Instantiate(go, position, rotation, parent ?? poolParent);
            list.Add(item.gameObject);
            RegisterClone(item.gameObject, poolKey);
            return item;
        }

        public GameObject Spawn(GameObject go, Transform parent = null, bool worldPositionStays = false)
        {
            var poolKey = GetPoolKey(go);
            if (!poolKey) return null;
            CleanNullEntries(poolKey);
            var (list, poolParent) = GetOrCreatePool(poolKey);
            var inactive = TryGetFirstInactive(list);
            if (inactive)
            {
                inactive.transform.SetParent(parent ?? poolParent, worldPositionStays);
                if (parent) inactive.transform.localPosition = Vector3.zero;
                inactive.Show();
                return inactive;
            }
            var item = Instantiate(go, parent ?? poolParent, worldPositionStays);
            if (parent) item.transform.localPosition = Vector3.zero;
            list.Add(item);
            RegisterClone(item, poolKey);
            return item;
        }

        public GameObject Spawn(GameObject go, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var poolKey = GetPoolKey(go);
            if (!poolKey) return null;
            CleanNullEntries(poolKey);
            var (list, poolParent) = GetOrCreatePool(poolKey);
            var inactive = TryGetFirstInactive(list);
            if (inactive)
            {
                inactive.transform.SetParent(parent ?? poolParent);
                inactive.transform.position = position;
                inactive.transform.rotation = rotation;
                inactive.Show();
                return inactive;
            }
            var item = Instantiate(go, position, rotation, parent ?? poolParent);
            list.Add(item);
            RegisterClone(item, poolKey);
            return item;
        }

        public void DeSpawn<T>(T go) where T : Component
        {
            var key = GetKeyFromClone(go.gameObject);
            if (key)
            {
                go.Hide();
                go.transform.SetParent(_parentPools[key]);
            }
            else
            {
                go.Hide();
            }
        }

        public void DeSpawn(GameObject go)
        {
            var key = GetKeyFromClone(go);
            if (key)
            {
                go.transform.SetParent(_parentPools[key]);
                go.Hide();
            }
            else
            {
                go.Hide();
            }
        }

        public void DeSpawn<T>(T go, float timeDelay) where T : Component
        {
            this.Delay(timeDelay,() => { DeSpawn(go); });
        }

        public void DeSpawn(GameObject go, float timeDelay)
        {
            if (timeDelay > 0)
                this.Delay(timeDelay,() => { DeSpawn(go); });
            else
                DeSpawn(go);
        }

        public void DeSpawnAll<T>(T go) where T : Component
        {
            var poolKey = GetPoolKey(go.gameObject);
            if (!poolKey || !_gameObjectPools.TryGetValue(poolKey, out var pool)) return;
            foreach (var component in pool)
            {
                if (component) component.Hide();
                if (component) component.transform.SetParent(_parentPools[poolKey]);
            }
        }

        public void DeSpawnAll(GameObject go)
        {
            var poolKey = GetPoolKey(go);
            if (!poolKey || !_gameObjectPools.TryGetValue(poolKey, out var pool)) return;
            for (var index = 0; index < pool.Count; index++)
            {
                var item = pool[index];
                if (item) item.Hide();
                if (item) item.transform.SetParent(_parentPools[poolKey]);
            }
        }

        public void DestroyObject(GameObject go)
        {
            var key = GetKeyFromClone(go);
            if (!key) return;
            _gameObjectPools[key].Remove(go);
            UnregisterClone(go);
            Destroy(go);
        }

        public void DestroyObject<T>(T go) where T : Component
        {
            var key = GetKeyFromClone(go.gameObject);
            if (!key) return;
            _gameObjectPools[key].Remove(go.gameObject);
            UnregisterClone(go.gameObject);
            Destroy(go);
        }

        /// <summary> O(1) tra prefab key từ clone nhờ map clone -> prefab, không duyệt toàn bộ pool. </summary>
        private GameObject GetKeyFromClone(GameObject clone)
        {
            if (!clone) return null;
            return _cloneToPrefabKey.TryGetValue(clone, out var key) ? key : null;
        }
    }
}