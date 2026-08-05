using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameUp.Core.UI
{
    public class UIPopup : UIBaseView
    {
        private const string PopupDataResourcePath = "Data/PopupData";

        private static PopupData _popupData;

        protected static readonly Dictionary<Type, UIPopup> Popups = new();
        private static readonly List<UIPopup> ActivePopups = new List<UIPopup>();

        public static bool IsPopupOn => ActivePopups.Count > 0;

        #region Static lifecycle

        private static readonly List<Type> StaleKeyBuffer = new();
        private static readonly List<UIPopup> CloseBuffer = new();

        /// <summary>
        /// Static không tự reset khi tắt Domain Reload, và instance trong cache sẽ bị hủy theo scene.
        /// Reset ở đầu mỗi phiên Play rồi dọn theo từng lần unload scene.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Popups.Clear();
            ActivePopups.Clear();
            StaleKeyBuffer.Clear();
            CloseBuffer.Clear();

            _popupData = null;

            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            PurgeDestroyed();
        }

        /// <summary>
        /// Loại bỏ mọi popup đã bị Destroy khỏi cache và danh sách đang mở.
        /// Gọi tự động sau mỗi lần unload scene; có thể gọi thủ công nếu tự hủy popup bằng tay.
        /// </summary>
        public static void PurgeDestroyed()
        {
            StaleKeyBuffer.Clear();
            foreach (var pair in Popups)
            {
                if (!pair.Value) StaleKeyBuffer.Add(pair.Key);
            }

            for (var i = 0; i < StaleKeyBuffer.Count; i++)
            {
                Popups.Remove(StaleKeyBuffer[i]);
            }

            StaleKeyBuffer.Clear();

            for (var i = ActivePopups.Count - 1; i >= 0; i--)
            {
                if (!ActivePopups[i]) ActivePopups.RemoveAt(i);
            }
        }

        #endregion

        protected static PopupData PopupData
        {
            get
            {
                if (!_popupData) _popupData = Resources.Load<PopupData>(PopupDataResourcePath);
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

        /// <summary>Chỉ đóng những popup đang thực sự mở — duyệt bản sao vì <see cref="ActionClose"/> sẽ sửa danh sách.</summary>
        public static void CloseAllPopup()
        {
            if (ActivePopups.Count == 0) return;

            CloseBuffer.Clear();
            CloseBuffer.AddRange(ActivePopups);

            for (var i = 0; i < CloseBuffer.Count; i++)
            {
                var popup = CloseBuffer[i];
                if (popup) popup.Close();
            }

            CloseBuffer.Clear();
        }

        protected static UIPopup GetOrCreatePopup(Type type, UIPopup prefab)
        {
            if (Popups.TryGetValue(type, out var popup))
            {
                if (popup) return popup;

                // Instance đã bị hủy cùng scene trước — bỏ entry rác rồi tạo lại.
                Popups.Remove(type);
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
            ResolvePopupAsync(type, onComplete);
        }

        /// <summary>
        /// Đường duy nhất để lấy instance của một popup: trả cache nếu có, nếu chưa thì load Addressable rồi tạo.
        /// Mọi API Open/Preload đều đi qua đây nên hành vi luôn giống nhau.
        /// </summary>
        protected static void ResolvePopupAsync(Type type, Action<UIPopup> onResolved)
        {
            if (type == null)
            {
                GULogger.Error("UIPopup", "ResolvePopupAsync called with null type");
                return;
            }

            if (!typeof(UIPopup).IsAssignableFrom(type))
            {
                GULogger.Error("UIPopup", $"Type {type.Name} is not a UIPopup");
                return;
            }

            if (Popups.TryGetValue(type, out var cachedPopup) && cachedPopup)
            {
                onResolved?.Invoke(cachedPopup);
                return;
            }

            if (!PopupData)
            {
                GULogger.Error("UIPopup", $"PopupData asset not found at Resources/{PopupDataResourcePath}");
                return;
            }

            AddressableLoad.WhenReady(PopupData.GetPopupAsync(type), prefab =>
            {
                var popup = GetOrCreatePopup(type, prefab);
                if (popup) onResolved?.Invoke(popup);
            }, "UIPopup", type.Name);
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
        protected override void OnValidate()
        {
            base.OnValidate();

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
        public static void OpenViewAsync(Action<T> onComplete = null)
        {
            ResolvePopupAsync(typeof(T), popup =>
            {
                popup.Open();
                onComplete?.Invoke(popup.Cast<T>());
            });
        }

        public static void CloseView()
        {
            if (!Popups.TryGetValue(typeof(T), out var popup))
            {
                return;
            }

            if (!popup || !popup.gameObject.activeSelf)
            {
                return;
            }

            popup.Close();
        }

        public static void PreloadViewAsync(Action<T> onComplete = null)
        {
            ResolvePopupAsync(typeof(T), popup => onComplete?.Invoke(popup.Cast<T>()));
        }
    }
}