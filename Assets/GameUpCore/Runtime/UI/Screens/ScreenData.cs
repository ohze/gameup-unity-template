using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GameUp.Core.UI
{
    [CreateAssetMenu(fileName = "ScreenData", menuName = "Data/UI/ScreenData", order = 0)]
    public class ScreenData : ScriptableObject
    {
        [ReadOnlyInInspector]
        [SerializeField] private string pathScreen = "Assets/_MainProject/Prefabs/UI/Screens";
        [SerializeField] private List<ScreenInfo> screens = new();
        private string PathScreen
        {
            get
            {
                if (string.IsNullOrEmpty(pathScreen)) return pathScreen;

                const string assetsPrefix = "Assets/";
                if (pathScreen.StartsWith(assetsPrefix, StringComparison.Ordinal))
                    return pathScreen[assetsPrefix.Length..];

                return pathScreen;
            }
        }


        private readonly Dictionary<string, AsyncOperationHandle<UIScreen>> _cacheOperators = new();
        private readonly Dictionary<string, ScreenInfo> _screenLookupByTypeName = new(StringComparer.Ordinal);
        private bool _isLookupDirty = true;

        public AsyncOperationHandle<UIScreen> GetScreenAsync<T>() where T : UIScreen
        {
            return GetScreenAsync(typeof(T));
        }

        public AsyncOperationHandle<UIScreen> GetScreenAsync(Type type)
        {
            if (type == null)
            {
                GULogger.Error("ScreenData", "GetScreenAsync called with null type");
                return default;
            }

            var tName = type.Name;
            if (_cacheOperators.TryGetValue(tName, out var cachedHandle))
            {
                return cachedHandle;
            }

            if (!TryGetScreenInfo(tName, out var screenInfo) || screenInfo.screenRef == null)
            {
                GULogger.Error("ScreenData", $"Cannot find ScreenInfo for type {tName}");
                return default;
            }

            var handle = screenInfo.screenRef.LoadAssetAsync();
            _cacheOperators[tName] = handle;
            return handle;
        }

        private bool TryGetScreenInfo(string typeName, out ScreenInfo screenInfo)
        {
            RebuildLookupIfNeeded();
            return _screenLookupByTypeName.TryGetValue(typeName, out screenInfo);
        }

        private void RebuildLookupIfNeeded()
        {
            if (!_isLookupDirty) return;

            _screenLookupByTypeName.Clear();
            for (var i = 0; i < screens.Count; i++)
            {
                var info = screens[i];
                if (info == null || string.IsNullOrEmpty(info.typeName)) continue;

                _screenLookupByTypeName[info.typeName] = info;
            }

            _isLookupDirty = false;
        }

        [Button]
        public void SetUp()
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(PathScreen))
            {
                GULogger.Error("ScreenData", "PathScreen is not set");
                return;
            }

            var prefabs = GameUtils.GetPrefabAssetsWithComponent<UIScreen>(PathScreen);
            screens = new List<ScreenInfo>();
            foreach (var prefab in prefabs)
            {
                var screenComponent = prefab.GetComponentInChildren<UIScreen>(true);
                if (screenComponent == null) continue;

                screens.Add(new ScreenInfo
                {
                    name = prefab.name,
                    typeName = screenComponent.GetType().Name,
                    screenRef = new UIScreenReference(UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(prefab))),
                });
            }

            _cacheOperators.Clear();
            _isLookupDirty = true;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }

        private void OnEnable()
        {
            _cacheOperators.Clear();
            _isLookupDirty = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _isLookupDirty = true;
        }
#endif
    }

    [Serializable]
    public class ScreenInfo
    {
        public string name;
        public string typeName;
        public UIScreenReference screenRef;
    }
}