using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GameUp.Core;

namespace GameUp.UI
{
    /// <summary>
    /// Central UI manager. Creates root Canvas and manages UI layers.
    /// Access screens and popups through ScreenNavigator and PopupStack.
    /// </summary>
    public sealed class UIManager : PersistentSingleton<UIManager>
    {
        [SerializeField] Camera _uiCamera;
        [SerializeField] int _referenceWidth = 1080;
        [SerializeField] int _referenceHeight = 1920;

        Canvas _rootCanvas;
        CanvasScaler _canvasScaler;

        readonly Dictionary<UILayer, RectTransform> _layers = new();
        readonly Dictionary<Type, UIScreen> _screens = new();
        readonly Dictionary<Type, UIPopup> _popups = new();

        ScreenNavigator _screenNavigator;
        PopupStack _popupStack;

        public Canvas RootCanvas => _rootCanvas;
        public ScreenNavigator ScreenNavigator => _screenNavigator;
        public PopupStack PopupStack => _popupStack;

        protected override void OnSingletonAwake()
        {
            CreateRootCanvas();
            CreateLayers();
            _screenNavigator = new ScreenNavigator();
            _popupStack = new PopupStack(GetLayerTransform(UILayer.Popup));
        }

        /// <summary>Register a screen instance for type-based lookup.</summary>
        public void RegisterScreen<T>(T screen) where T : UIScreen
        {
            _screens[typeof(T)] = screen;
            screen.transform.SetParent(GetLayerTransform(UILayer.Screen), false);
            screen.gameObject.SetActive(false);
        }

        /// <summary>Register a popup instance for type-based lookup.</summary>
        public void RegisterPopup<T>(T popup) where T : UIPopup
        {
            _popups[typeof(T)] = popup;
            popup.transform.SetParent(GetLayerTransform(UILayer.Popup), false);
            popup.gameObject.SetActive(false);
        }

        public T GetScreen<T>() where T : UIScreen
            => _screens.TryGetValue(typeof(T), out var s) ? (T)s : null;

        public T GetPopup<T>() where T : UIPopup
            => _popups.TryGetValue(typeof(T), out var p) ? (T)p : null;

        public RectTransform GetLayerTransform(UILayer layer)
            => _layers.TryGetValue(layer, out var rt) ? rt : null;

        void CreateRootCanvas()
        {
            var go = new GameObject("[UICanvas]");
            go.transform.SetParent(transform);

            _rootCanvas = go.AddComponent<Canvas>();
            _rootCanvas.renderMode = _uiCamera != null
                ? RenderMode.ScreenSpaceCamera
                : RenderMode.ScreenSpaceOverlay;
            if (_uiCamera != null)
                _rootCanvas.worldCamera = _uiCamera;
            _rootCanvas.sortingOrder = 0;

            _canvasScaler = go.AddComponent<CanvasScaler>();
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = new Vector2(_referenceWidth, _referenceHeight);
            _canvasScaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
        }

        void CreateLayers()
        {
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                var layerGo = new GameObject(layer.ToString());
                var rt = layerGo.AddComponent<RectTransform>();
                rt.SetParent(_rootCanvas.transform, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;

                var canvas = layerGo.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = (int)layer;

                layerGo.AddComponent<GraphicRaycaster>();

                _layers[layer] = rt;
            }
        }
    }
}
