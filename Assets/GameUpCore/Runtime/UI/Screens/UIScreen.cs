using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameUp.Core.UI
{
    public class UIScreen : UIBaseView
    {
        private const string ScreenDataResourcePath = "Data/ScreenData";

        /// <summary>Giới hạn ngăn xếp Back để không phình vô hạn trong phiên chơi dài.</summary>
        private const int MaxHistoryDepth = 32;

        private static ScreenData _screenData;
        private static Transform _screenHolder;

        public static ScreenData ScreenData
        {
            get
            {
                if (!_screenData)
                {
                    _screenData = Resources.Load<ScreenData>(ScreenDataResourcePath);
                }

                return _screenData;
            }
        }

        protected static readonly Dictionary<Type, UIScreen> Screens = new();
        protected static readonly Stack<UIScreen> HistoryView = new Stack<UIScreen>();

        public static event Action<UIScreen> OnCurrentScreenChanged;

        private static UIScreen _currentScreen;
        public static UIScreen currentScreen
        {
            get => _currentScreen;
            set
            {
                _currentScreen = value;
                OnCurrentScreenChanged?.Invoke(value);
            }
        }

        private static Transform ScreenHolder
        {
            get
            {
                if (_screenHolder == null)
                {
                    _screenHolder = ObjectFinder.GetObject(ObjectID.ScreenHolder);
                }

                return _screenHolder;
            }
        }

        #region Static lifecycle

        private static readonly List<Type> StaleKeyBuffer = new();
        private static readonly List<UIScreen> HistoryBuffer = new();

        /// <summary>
        /// Static không tự reset khi tắt Domain Reload, và instance trong cache sẽ bị hủy theo scene.
        /// Reset ở đầu mỗi phiên Play rồi dọn theo từng lần unload scene.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Screens.Clear();
            HistoryView.Clear();
            StaleKeyBuffer.Clear();
            HistoryBuffer.Clear();

            _currentScreen = null;
            _screenHolder = null;
            _screenData = null;
            OnCurrentScreenChanged = null;

            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            PurgeDestroyed();
        }

        /// <summary>
        /// Loại bỏ mọi screen đã bị Destroy khỏi cache/history. Gọi tự động sau mỗi lần unload scene;
        /// có thể gọi thủ công nếu tự hủy screen bằng tay.
        /// </summary>
        public static void PurgeDestroyed()
        {
            StaleKeyBuffer.Clear();
            foreach (var pair in Screens)
            {
                if (!pair.Value) StaleKeyBuffer.Add(pair.Key);
            }

            for (var i = 0; i < StaleKeyBuffer.Count; i++)
            {
                Screens.Remove(StaleKeyBuffer[i]);
            }

            StaleKeyBuffer.Clear();

            if (!_currentScreen) _currentScreen = null;
            RebuildHistory(MaxHistoryDepth);
        }

        /// <summary>Đẩy screen vào history, bỏ qua entry đã hủy và cắt bớt phần cũ nhất khi vượt <see cref="MaxHistoryDepth"/>.</summary>
        private static void PushHistory(UIScreen screen)
        {
            if (!screen) return;

            HistoryView.Push(screen);
            if (HistoryView.Count > MaxHistoryDepth) RebuildHistory(MaxHistoryDepth);
        }

        /// <summary>Dựng lại stack, giữ thứ tự và chỉ giữ tối đa <paramref name="maxDepth"/> mục còn sống gần nhất.</summary>
        private static void RebuildHistory(int maxDepth)
        {
            if (HistoryView.Count == 0) return;

            HistoryBuffer.Clear();
            while (HistoryView.Count > 0)
            {
                var screen = HistoryView.Pop();
                if (!screen) continue;
                if (HistoryBuffer.Count >= maxDepth) continue;

                HistoryBuffer.Add(screen);
            }

            for (var i = HistoryBuffer.Count - 1; i >= 0; i--)
            {
                HistoryView.Push(HistoryBuffer[i]);
            }

            HistoryBuffer.Clear();
        }

        #endregion

        public override void OnOpen()
        {
            gameObject.Show();
            transform.SetAsLastSibling();
            base.OnOpen();
            currentScreen = this;
        }

        public override void OnClose(Action onComplete = null)
        {
            _anim.OnStop();
            _anim.SetReverseCompleteCallback(() =>
            {
                GULogger.Verbose("UIScreen", $"Close View: {name}");
                if (gameObject.activeSelf)
                {
                    gameObject.Hide();
                }

                onComplete?.Invoke();
            }).OnReverse();
        }

        public void ShowLast(bool isUseTransition = false)
        {
            if (TryPopHistory(out var previous))
            {
                OpenView(previous, false, isUseTransition);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (EditorApplication.isPlaying) return;

            var type = GetType();
            if (type == typeof(UIScreen)) return;

            var desiredName = type.Name;
            if (gameObject.name == desiredName) return;

            ApplyNameChange(desiredName);
        }

        private void ApplyNameChange(string desiredName)
        {
            Undo.RecordObject(gameObject, "Rename UIScreen");
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

        #region Static

        public static void OpenPrevious()
        {
            if (TryPopHistory(out var previous))
            {
                OpenView(previous);
            }
        }

        /// <summary>Lấy screen còn sống gần nhất trong history, bỏ qua các entry đã bị hủy theo scene.</summary>
        private static bool TryPopHistory(out UIScreen screen)
        {
            while (HistoryView.Count > 0)
            {
                screen = HistoryView.Pop();
                if (screen) return true;
            }

            screen = null;
            return false;
        }

        private static void OpenView(UIScreen view, bool remember = false, bool isUseTransition = false)
        {
            if (!view)
            {
                GULogger.Error("UIScreen", "OpenView called with a destroyed or null screen");
                return;
            }

            if (currentScreen)
            {
                if (remember)
                {
                    PushHistory(currentScreen);
                }

                currentScreen.Close();
            }

            view.Open();
            currentScreen = view;
        }

        protected static UIScreen GetOrCreateScreen(Type type, UIScreen prefab)
        {
            if (Screens.TryGetValue(type, out var screen))
            {
                if (screen) return screen;

                // Instance đã bị hủy cùng scene trước — bỏ entry rác rồi tạo lại.
                Screens.Remove(type);
            }

            if (prefab == null)
            {
                GULogger.Error("UIScreen", $"Screen prefab is null for type {type?.Name}");
                return null;
            }

            var instance = Instantiate(prefab, ScreenHolder);
            if (instance.gameObject.activeSelf)
            {
                instance.Hide();
            }

            Screens[type] = instance;
            return instance;
        }

        protected static void OpenScreenWithInstance(Type type, UIScreen ins, bool remember = true)
        {
            if (!ins)
            {
                GULogger.Error("UIScreen", $"Screen instance is null or destroyed for type {type?.Name}");
                return;
            }

            if (currentScreen && currentScreen.GetType() == type) return;

            if (currentScreen)
            {
                if (remember)
                {
                    PushHistory(currentScreen);
                }

                currentScreen.OnClose();
            }

            ins.Open();
            currentScreen = ins;
        }

        public static void OpenScreenByTypeAsync(Type type, bool remember = true)
        {
            var loadAsync = ScreenData.GetScreenAsync(type);
            if (!loadAsync.IsValid()) return;

            if (loadAsync.IsDone)
            {
                var ins = GetOrCreateScreen(type, loadAsync.Result);
                OpenScreenWithInstance(type, ins, remember);
            }
            else
            {
                loadAsync.Completed += handle =>
                {
                    var ins = GetOrCreateScreen(type, handle.Result);
                    OpenScreenWithInstance(type, ins, remember);
                };
            }
        }

        public static void PreloadAsyncView(Type type)
        {
            PreloadViewByTypeAsync(type);
        }

        public static void PreloadViewByTypeAsync(Type type, Action<UIScreen> onComplete = null)
        {
            if (type == null)
            {
                GULogger.Error("UIScreen", "PreloadViewByTypeAsync called with null type");
                return;
            }

            if (!typeof(UIScreen).IsAssignableFrom(type))
            {
                GULogger.Error("UIScreen", $"Type {type.Name} is not a UIScreen");
                return;
            }

            if (Screens.TryGetValue(type, out var cachedScreen) && cachedScreen)
            {
                onComplete?.Invoke(cachedScreen);
                return;
            }

            var loadAsync = ScreenData.GetScreenAsync(type);
            if (!loadAsync.IsValid())
            {
                return;
            }

            if (loadAsync.IsDone)
            {
                var screen = GetOrCreateScreen(type, loadAsync.Result);
                onComplete?.Invoke(screen);
                return;
            }

            loadAsync.Completed += handle =>
            {
                if (!handle.IsValid() || handle.Result == null)
                {
                    GULogger.Error("UIScreen", $"Failed to preload screen {type.Name}");
                    return;
                }

                var screen = GetOrCreateScreen(type, handle.Result);
                onComplete?.Invoke(screen);
            };
        }

        public static void PreloadViewByTypesAsync(params Type[] types)
        {
            if (types == null || types.Length == 0)
            {
                return;
            }

            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type == null) continue;
                PreloadViewByTypeAsync(type);
            }
        }

        #endregion

    }

    public class UIScreen<T> : UIScreen where T : UIScreen
    {
        private static bool _isRequestingLoad;
        private static Action<T> _pendingOnComplete;

        public static void OpenViewAsync(Action<T> onComplete = null, bool remember = true, bool isUseTransition = true)
        {
            if (isUseTransition)
            {
                OpenWithTransitionAsync(onComplete, remember);
            }
            else
            {
                OpenViewInternalAsync(onComplete, remember);
            }
        }

        public static void CloseView()
        {
            if (Screens.TryGetValue(typeof(T), out var screen) && screen)
            {
                screen.Close();
            }
        }

        public static T GetView()
        {
            if (Screens.TryGetValue(typeof(T), out var screen) && screen)
            {
                return screen as T;
            }

            return null;
        }

        public static AsyncOperationHandle<UIScreen> PreloadView()
        {
            return ScreenData.GetScreenAsync<T>();
        }

        public static void PreloadViewAsync(Action<T> onComplete = null)
        {
            var type = typeof(T);

            if (Screens.TryGetValue(type, out var cachedScreen) && cachedScreen)
            {
                onComplete?.Invoke(cachedScreen as T);
                return;
            }

            var loadAsync = ScreenData.GetScreenAsync<T>();
            if (!loadAsync.IsValid()) return;

            if (loadAsync.IsDone)
            {
                var ins = GetOrCreateScreen(type, loadAsync.Result);
                onComplete?.Invoke(ins as T);
                return;
            }

            loadAsync.Completed += handle =>
            {
                if (!handle.IsValid() || handle.Result == null)
                {
                    GULogger.Error("UIScreen", $"Failed to preload screen {type.Name}");
                    return;
                }

                var ins = GetOrCreateScreen(type, handle.Result);
                onComplete?.Invoke(ins as T);
            };
        }

        private static void OpenWithTransitionAsync(Action<T> onComplete = null, bool remember = true)
        {
            OpenViewInternalAsync(onComplete, remember);
        }

        private static void OpenViewInternalAsync(Action<T> onComplete = null, bool remember = true)
        {
            var type = typeof(T);
            var loadAsync = ScreenData.GetScreenAsync<T>();
            if (!loadAsync.IsValid()) return;

            if (loadAsync.IsDone)
            {
                var ins = GetOrCreateScreen(type, loadAsync.Result);
                OpenScreenWithInstance(type, ins, remember);
                onComplete?.Invoke(ins as T);
            }
            else
            {
                if (onComplete != null)
                {
                    _pendingOnComplete += onComplete;
                }

                if (_isRequestingLoad) return;

                _isRequestingLoad = true;
                loadAsync.Completed += handle =>
                {
                    _isRequestingLoad = false;
                    var ins = GetOrCreateScreen(type, handle.Result);
                    OpenScreenWithInstance(type, ins, remember);

                    var callbacks = _pendingOnComplete;
                    _pendingOnComplete = null;
                    callbacks?.Invoke(ins as T);
                };
            }
        }
    }
}