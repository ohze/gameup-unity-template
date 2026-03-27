using System;
using System.Collections.Generic;
using GameUp.Core;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameUp.Core.UI
{
    [CreateAssetMenu(fileName = "PopupData", menuName = "Data/UI/PopupData", order = 0)]
    public class PopupData : ScriptableObject
    {
        [ReadOnlyInInspector]
        [SerializeField] private string pathPopup = "Assets/_MainProject/Prefabs/UI/Popups";
        [SerializeField] private List<PopupInfo> popups = new();

        private string PathPopup
        {
            get
            {
                if (string.IsNullOrEmpty(pathPopup)) return pathPopup;

                const string assetsPrefix = "Assets/";
                if (pathPopup.StartsWith(assetsPrefix, StringComparison.Ordinal))
                    return pathPopup[assetsPrefix.Length..];

                return pathPopup;
            }
        }
        private readonly Dictionary<string, AsyncOperationHandle<UIPopup>> _cacheOperators = new();
        public AsyncOperationHandle<UIPopup> GetPopupAsync<T>() where T : UIPopup
        {
            var tName = typeof(T).Name;
            if (_cacheOperators.TryGetValue(tName, out var cachedHandle)) return cachedHandle;

            var p = popups.Find(s => s.typeName == tName);
            if (p == null || p.popupRef == null)
            {
                GULogger.Error("PopupData", $"Cannot find PopupInfo for type {tName}");
                return default;
            }

            _cacheOperators.Add(tName, p.popupRef.LoadAssetAsync());

            return _cacheOperators[tName];
        }

        public void SetupPathPopup(string path)
        {
            pathPopup = path;
            GameUtils.SaveAssets(this);
        }

        [Button]
        public void SetUp()
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(PathPopup))
            {
                GULogger.Error("PopupData", "PathPopup is not set");
                return;
            }

            var prefabs = GameUtils.GetPrefabAssetsWithComponent<UIPopup>(PathPopup);

            if (prefabs.Count == 0)
            {
                GULogger.Error("PopupData", "No popup found");
                return;
            }

            popups = new List<PopupInfo>();
            foreach (var prefab in prefabs)
            {
                var popupComponent = prefab.GetComponentInChildren<UIPopup>(true);
                if (popupComponent == null) continue;

                popups.Add(new PopupInfo
                {
                    name = prefab.name,
                    typeName = popupComponent.GetType().Name,
                    popupRef = new UIPopupReference(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)))
                });
            }
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
#endif
        }
    }

    [Serializable]
    public class PopupInfo
    {
        public string name;
        public string typeName;
        public UIPopupReference popupRef;
    }
}