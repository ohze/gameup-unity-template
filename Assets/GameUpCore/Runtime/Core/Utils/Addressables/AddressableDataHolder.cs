using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameUp.Core
{
    [CreateAssetMenu(fileName = "AddressableHolder", menuName = "Data/Addressable/Holder", order = 0)]
    public class AddressableDataHolder : ResourcesSingleton<AddressableDataHolder>, IInitial
    {
        private const string DATA_FOLDER = "_MainProject/Data/Singletons";
        private const string RESOURCES_HOLDER_ASSET_PATH = "Assets/_MainProject/Resources/Data/Singletons/AddressableHolder.asset";
#if UNITY_EDITOR
        private const string RESOURCES_LOAD_PATH_SINGLETONS = "Data/Singletons/AddressableHolder";
        private const string RESOURCES_LOAD_PATH_SINGLETON = "Data/Singleton/AddressableHolder";
#endif
        public List<DataReferenceInfo> dataReferenceInfos;

        private readonly Dictionary<string, AsyncOperationHandle<ScriptableObject>> _cacheHandlers =
            new(StringComparer.Ordinal);

        private int _pendingLoads;
        private bool _initializing;
        public bool Initialized { get; set; }

        public void Initialize()
        {
            if (_initializing || Initialized) return;

            Initialized = false;
            _initializing = true;
            _pendingLoads = 0;
            _cacheHandlers.Clear();

            if (dataReferenceInfos == null || dataReferenceInfos.Count == 0)
            {
                Initialized = true;
                _initializing = false;
                return;
            }

            CoroutineRunner.RunCoroutineWithoutReturn(IEWaitInitialize());

            foreach (var info in dataReferenceInfos)
            {
                if (info == null) continue;
                if (string.IsNullOrWhiteSpace(info.typeName) || info.dataRef == null) continue;

                if (_cacheHandlers.ContainsKey(info.typeName))
                {
                    GULogger.Warning("AddressableDataHolder", $"Duplicate typeName '{info.typeName}'. Skipping.");
                    continue;
                }

                _pendingLoads++;
                var typeName = info.typeName;
                var load = info.dataRef.LoadAssetAsync();
                load.Completed += handle =>
                {
                    _pendingLoads = Mathf.Max(0, _pendingLoads - 1);

                    if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                    {
                        GULogger.Warning("AddressableDataHolder", $"Load failed for '{typeName}'. Status={handle.Status}");
                        return;
                    }

                    _cacheHandlers[typeName] = handle;
                };
            }
        }

        private IEnumerator IEWaitInitialize()
        {
            yield return new WaitUntil(() => _pendingLoads == 0);
            Initialized = true;
            _initializing = false;
        }

        public T GetData<T>() where T : ScriptableObject
        {
#if UNITY_EDITOR
            var temps = GameUtils.GetAssetList<ScriptableObject>(DATA_FOLDER);
            foreach (var o in temps)
                if (o is T so)
                    return so;
#endif
            if (!Initialized) Initialize();
            if (!Initialized) return null;

            if (_cacheHandlers.TryGetValue(typeof(T).Name, out var handle) && handle.IsValid() &&
                handle.Status == AsyncOperationStatus.Succeeded)
                return handle.Result as T;

            return null;
        }

#if UNITY_EDITOR

        [Button]
        public void SetUp()
        {
            Editor_RebuildReferencesFromFolder();
        }

        public void Editor_RebuildReferencesFromFolder()
        {
            var newList = new List<DataReferenceInfo>();

            var temps = GameUtils.GetAssetList<ScriptableObject>(DATA_FOLDER);
            for (var i = 0; i < temps.Count; i++)
            {
                var info = temps[i];
                if (!info) continue;

                var path = AssetDatabase.GetAssetPath(info);
                if (string.IsNullOrWhiteSpace(path)) continue;

                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrWhiteSpace(guid)) continue;

                newList.Add(new DataReferenceInfo
                {
                    typeName = info.GetType().Name,
                    dataRef = new DataReference(guid)
                });
            }

            if (!Editor_AreReferencesEqual(dataReferenceInfos, newList))
            {
                dataReferenceInfos = newList;
                GameUtils.SaveAssets(this);
            }
        }

        private static bool Editor_AreReferencesEqual(List<DataReferenceInfo> a, List<DataReferenceInfo> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            for (var i = 0; i < a.Count; i++)
            {
                var ai = a[i];
                var bi = b[i];

                if (ai == null || bi == null) return false;
                if (!string.Equals(ai.typeName, bi.typeName, StringComparison.Ordinal)) return false;
                if (ai.dataRef == null || bi.dataRef == null) return false;
                if (!string.Equals(ai.dataRef.AssetGUID, bi.dataRef.AssetGUID, StringComparison.Ordinal)) return false;
            }

            return true;
        }

        public static AddressableDataHolder Editor_LoadFromResourcesOrNull()
        {
            // Support both layouts (Data/Singletons and legacy Data/Singleton).
            var holder = Resources.Load<AddressableDataHolder>(RESOURCES_LOAD_PATH_SINGLETONS);
            if (holder) return holder;
            return Resources.Load<AddressableDataHolder>(RESOURCES_LOAD_PATH_SINGLETON);
        }

        public static AddressableDataHolder Editor_EnsureAssetExists()
        {
            // Prefer direct asset lookup to avoid ResourcesSingleton caching issues in Editor.
            var holder = AssetDatabase.LoadAssetAtPath<AddressableDataHolder>(RESOURCES_HOLDER_ASSET_PATH);
            if (holder) return holder;

            var dir = System.IO.Path.GetDirectoryName(RESOURCES_HOLDER_ASSET_PATH)?.Replace("\\", "/");
            if (!string.IsNullOrWhiteSpace(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                // Reuse editor window folder utility expectations: create recursively via AssetDatabase.
                var parts = dir.Split('/');
                var current = parts[0];
                for (var i = 1; i < parts.Length; i++)
                {
                    var next = $"{current}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            holder = ScriptableObject.CreateInstance<AddressableDataHolder>();
            holder.name = "AddressableHolder";
            AssetDatabase.CreateAsset(holder, RESOURCES_HOLDER_ASSET_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return holder;
        }

#endif

        private void OnDisable()
        {
            ReleaseAll();
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }

        private void ReleaseAll()
        {
            if (_cacheHandlers.Count == 0) return;

            foreach (var kv in _cacheHandlers)
            {
                var handle = kv.Value;
                if (!handle.IsValid()) continue;
                Addressables.Release(handle);
            }

            _cacheHandlers.Clear();
            Initialized = false;
            _initializing = false;
            _pendingLoads = 0;
        }
    }

    [Serializable]
    public class DataReferenceInfo
    {
        public string typeName;
        public DataReference dataRef;
    }
}