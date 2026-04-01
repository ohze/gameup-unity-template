using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameUp.Core.UI
{
    public class UIScreen : UIBaseView
    {
        private const string ScreenDataResourcePath = "Data/ScreenData";
        private static readonly Func<bool> AlwaysTruePredicate = () => true;

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
        public static UIScreen currentScreen;

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
            if (HistoryView.Count != 0)
            {
                OpenView(HistoryView.Pop(), false, isUseTransition);
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
            if (HistoryView.Count != 0)
            {
                OpenView(HistoryView.Pop());
            }
        }

        private static void OpenView(UIScreen view, bool remember = false, bool isUseTransition = false)
        {
            if (currentScreen)
            {
                if (remember)
                {
                    HistoryView.Push(currentScreen);
                }

                currentScreen.Close();
                view.Open();
            }
            else
            {
                view.Open();
            }
            currentScreen = view;
        }

        protected static UIScreen GetOrCreateScreen(Type type, UIScreen prefab)
        {
            if (Screens.TryGetValue(type, out var screen))
            {
                return screen;
            }

            var instance = Instantiate(prefab, ScreenHolder);
            Screens[type] = instance;
            return instance;
        }

        protected static void OpenScreenWithInstance(Type type, UIScreen ins, bool remember = true)
        {
            if (currentScreen != null && currentScreen.GetType() == type)
            {
                return;
            }
            else
            {
                if (currentScreen)
                {
                    if (remember)
                    {
                        HistoryView.Push(currentScreen);
                    }

                    if (currentScreen != null) currentScreen.OnClose();
                }

                ins.Open();
                currentScreen = ins;
            }
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
            ScreenData.GetScreenAsync(type);
        }

        #endregion

    }

    public class UIScreen<T> : UIScreen where T : UIScreen
    {
        private static bool _isRequestingLoad;
        private static Action<T> _pendingOnComplete;
        private static readonly Func<bool> IsLoaded = () => currentScreen is T;

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
            if (Screens.TryGetValue(typeof(T), out var screen))
            {
                screen.Close();
            }
        }

        public static T GetView()
        {
            if (Screens.TryGetValue(typeof(T), out var screen))
            {
                return screen as T;
            }

            return null;
        }

        public static AsyncOperationHandle<UIScreen> PreloadView()
        {
            return ScreenData.GetScreenAsync<T>();
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