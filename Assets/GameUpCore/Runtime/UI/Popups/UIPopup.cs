using System;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameUp.Core.UI
{
    public class UIPopup : UIBaseView
    {
        private static PopupData _popupData;

        protected static readonly Dictionary<Type, UIPopup> Popups = new();
        private static readonly List<UIPopup> ActivePopups = new List<UIPopup>();

        public static bool IsPopupOn => ActivePopups.Count > 0;

        protected static PopupData PopupData
        {
            get
            {
                if (!_popupData) _popupData = Resources.Load<PopupData>("Data/PopupData");
                return _popupData;
            }
        }

        public override void OnOpen()
        {
            gameObject.Show();
            transform.SetAsLastSibling();
            if (!ActivePopups.Contains(this)) ActivePopups.Add(this);
            base.OnOpen();
        }

        public override void OnClose(Action callbackClose = null)
        {
            _anim.OnStop();
            _anim.SetReverseCompleteCallback(() =>
            {
                ActionClose(callbackClose);
            })
            .OnReverse();
        }

        public void ActionClose(Action callbackClose = null)
        {
            GULogger.Log("UIPopup", $"Close View: {name}");
            gameObject.Hide();
            callbackClose?.Invoke();
            ActivePopups.Remove(this);
        }

        public static void CloseAllPopup()
        {
            foreach (var popup in Popups) popup.Value.Close();
        }

        protected static UIPopup GetOrCreatePopup(Type type, UIPopup prefab)
        {
            if (Popups.TryGetValue(type, out var popup))
            {
                return popup;
            }

            if (prefab == null)
            {
                GULogger.Error("UIPopup", $"Popup prefab is null for type {type?.Name}");
                return null;
            }

            var popupHolder = ObjectFinder.GetObject(ObjectID.PopupHolder);
            var instance = Instantiate(prefab, popupHolder);
            if (instance.gameObject.activeSelf)
            {
                instance.Hide();
            }

            Popups[type] = instance;
            return instance;
        }

        public static void PreloadPopupByTypeAsync(Type type, Action<UIPopup> onComplete = null)
        {
            if (type == null)
            {
                GULogger.Error("UIPopup", "PreloadPopupByTypeAsync called with null type");
                return;
            }

            if (!typeof(UIPopup).IsAssignableFrom(type))
            {
                GULogger.Error("UIPopup", $"Type {type.Name} is not a UIPopup");
                return;
            }

            if (Popups.TryGetValue(type, out var cachedPopup))
            {
                onComplete?.Invoke(cachedPopup);
                return;
            }

            var loadAsync = PopupData.GetPopupAsync(type);
            if (!loadAsync.IsValid())
            {
                GULogger.Error("UIPopup", $"Popup handle is invalid for type {type.Name}");
                return;
            }

            if (loadAsync.IsDone)
            {
                var popup = GetOrCreatePopup(type, loadAsync.Result);
                onComplete?.Invoke(popup);
                return;
            }

            loadAsync.Completed += handle =>
            {
                if (!handle.IsValid() || handle.Result == null)
                {
                    GULogger.Error("UIPopup", $"Failed to preload popup {type.Name}");
                    return;
                }

                var popup = GetOrCreatePopup(type, handle.Result);
                onComplete?.Invoke(popup);
            };
        }

        public static void PreloadPopupByTypesAsync(params Type[] types)
        {
            if (types == null || types.Length == 0)
            {
                return;
            }

            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type == null) continue;
                PreloadPopupByTypeAsync(type);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (EditorApplication.isPlaying) return;

            var type = GetType();
            if (type == typeof(UIPopup)) return;

            var desiredName = type.Name;
            if (gameObject.name == desiredName) return;

            ApplyNameChange(desiredName);
        }

        private void ApplyNameChange(string desiredName)
        {
            Undo.RecordObject(gameObject, "Rename UIPopup");
            gameObject.name = desiredName;

            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(gameObject);

            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
            }

            if (gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(gameObject);
            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                var prefabRoot = gameObject.transform.root.gameObject;
                EditorUtility.SetDirty(prefabRoot);
                EditorApplication.delayCall += () =>
                {
                    if (!prefabRoot) return;
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    if (AssetDatabase.GetAssetPath(prefabRoot) != assetPath) return;

                    PrefabUtility.SavePrefabAsset(prefabRoot);
                };
            }
        }
#endif
    }

    public class UIPopup<T> : UIPopup where T : UIPopup
    {
        private static bool _isLoadingPopup;
        private static bool _isPreloadingPopup;
        private static event Action<T> OnPopupLoaded;
        private static event Action<T> OnPopupPreloaded;

        public static void OpenViewAsync(Action<T> onComplete = null)
        {
            if (Popups.TryGetValue(typeof(T), out var cachedPopup))
            {
                OpenPopupWithInstance(cachedPopup);
                onComplete?.Invoke(cachedPopup.Cast<T>());
                return;
            }

            var loadAsync = PopupData.GetPopupAsync<T>();
            if (!loadAsync.IsValid())
            {
                GULogger.Error("UIPopup", $"Popup handle is invalid for type {typeof(T).Name}");
                return;
            }

            if (loadAsync.IsDone)
            {
                var ins = CreateAndCacheInstance(loadAsync.Result);
                if (!ins)
                    return;

                OpenPopupWithInstance(ins);
                onComplete?.Invoke(ins.Cast<T>());
            }
            else
            {
                if (onComplete != null) OnPopupLoaded += onComplete;
                if (_isLoadingPopup) return;
                _isLoadingPopup = true;

                loadAsync.Completed += handle =>
                {
                    _isLoadingPopup = false;

                    var callbacks = OnPopupLoaded;
                    OnPopupLoaded = null;

                    if (!handle.IsValid() || handle.Result == null)
                    {
                        GULogger.Error("UIPopup", $"Failed to load popup {typeof(T).Name}");
                        return;
                    }

                    var ins = CreateAndCacheInstance(handle.Result);
                    if (!ins)
                        return;

                    OpenPopupWithInstance(ins);
                    callbacks?.Invoke(ins.Cast<T>());
                };
            }
        }

        private static UIPopup CreateAndCacheInstance(UIPopup prefab)
        {
            return GetOrCreatePopup(typeof(T), prefab);
        }

        private static void OpenPopupWithInstance(UIPopup ins)
        {
            ins.Open();
        }

        public static void CloseView()
        {
            if (Popups.ContainsKey(typeof(T))) Popups[typeof(T)].Close();
        }

        public static void PreloadViewAsync(Action<T> onComplete = null)
        {
            if (Popups.TryGetValue(typeof(T), out var cachedPopup))
            {
                onComplete?.Invoke(cachedPopup.Cast<T>());
                return;
            }

            var loadAsync = PopupData.GetPopupAsync<T>();
            if (!loadAsync.IsValid())
            {
                GULogger.Error("UIPopup", $"Popup handle is invalid for type {typeof(T).Name}");
                return;
            }

            if (loadAsync.IsDone)
            {
                var ins = CreateAndCacheInstance(loadAsync.Result);
                onComplete?.Invoke(ins.Cast<T>());
                return;
            }

            if (onComplete != null)
            {
                OnPopupPreloaded += onComplete;
            }

            if (_isPreloadingPopup) return;
            _isPreloadingPopup = true;

            loadAsync.Completed += handle =>
            {
                _isPreloadingPopup = false;

                var callbacks = OnPopupPreloaded;
                OnPopupPreloaded = null;

                if (!handle.IsValid() || handle.Result == null)
                {
                    GULogger.Error("UIPopup", $"Failed to preload popup {typeof(T).Name}");
                    return;
                }

                var ins = CreateAndCacheInstance(handle.Result);
                callbacks?.Invoke(ins.Cast<T>());
            };
        }
    }
}