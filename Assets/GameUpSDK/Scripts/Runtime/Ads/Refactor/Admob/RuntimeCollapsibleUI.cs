using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameUp.SDK
{
    /// <summary>
    /// Tự động sinh giao diện Nút Gập/Mở Native Ad bằng code lúc Runtime.
    /// Thiết kế: Nền đen bao quanh, thanh ngang trên đỉnh có Nút bấm vuông góc phải. Font LegacyRuntime.
    /// </summary>
    public class RuntimeCollapsibleUI : MonoBehaviour
    {
        private Action _onToggleCallback;

        private RectTransform _bgRect;
        private Text _arrowText;
        private Canvas _canvas;

        // Kích thước chuẩn chỉnh theo thiết kế
        private const float HEADER_HEIGHT = 60f;
        private const float EXTRA_PADDING_DP = 20f; // Bù hao 5dp để hở viền cho đẹp

        public static RuntimeCollapsibleUI Create(Action onToggle)
        {
            // 1. Tạo Root & Canvas
            GameObject rootObj = new GameObject("GameUp_NativeCollapsibleUI");
            DontDestroyOnLoad(rootObj);

            Canvas canvas = rootObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            CanvasScaler scaler = rootObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            rootObj.AddComponent<GraphicRaycaster>();

            // ==========================================
            // 2. TẠO KHỐI NỀN ĐEN BÊN DƯỚI (Bg_Panel)
            // ==========================================
            GameObject bgObj = new GameObject("Bg_Panel");
            bgObj.transform.SetParent(rootObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();

            bgRect.anchorMin = new Vector2(0f, 0f);
            bgRect.anchorMax = new Vector2(1f, 0f); // Tràn ngang
            bgRect.pivot = new Vector2(0.5f, 0f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);

            // ==========================================
            // 3. TẠO NÚT BẤM VUÔNG GÓC PHẢI (Btn_Toggle)
            // ==========================================
            GameObject btnObj = new GameObject("Btn_Toggle");
            btnObj.transform.SetParent(bgRect.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();

            btnRect.anchorMin = new Vector2(1f, 1f);
            btnRect.anchorMax = new Vector2(1f, 1f);
            btnRect.pivot = new Vector2(1f, 0f);

            btnRect.sizeDelta = new Vector2(80f, HEADER_HEIGHT);
            btnRect.anchoredPosition = Vector2.zero;

            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            Button btn = btnObj.AddComponent<Button>();

            // ==========================================
            // 4. TẠO TEXT MŨI TÊN
            // ==========================================
            GameObject txtObj = new GameObject("Txt_Arrow");
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            Text arrowText = txtObj.AddComponent<Text>();
            arrowText.text = "▼";
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.color = new Color(0.7f, 0.7f, 0.7f, 1f); // Màu xám nhạt
            arrowText.fontSize = 24;
            arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ==========================================
            // INIT
            // ==========================================
            RuntimeCollapsibleUI controller = rootObj.AddComponent<RuntimeCollapsibleUI>();
            controller.Init(canvas, bgRect, arrowText, onToggle);

            btn.onClick.AddListener(controller.OnClicked);

            return controller;
        }

        private void Init(Canvas canvas, RectTransform bgRect, Text arrowText, Action onToggle)
        {
            _canvas = canvas;
            _bgRect = bgRect;
            _arrowText = arrowText;
            _onToggleCallback = onToggle;
            UpdatePosition();
        }

        private void OnClicked()
        {
            UpdatePosition();
            _onToggleCallback?.Invoke();
        }

        public void SetVisible(bool isVisible)
        {
            gameObject.SetActive(isVisible);
            if (isVisible) UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (_canvas == null || _bgRect == null) return;

            _arrowText.text = "▼";

            float dpi = Screen.dpi == 0 ? 160f : Screen.dpi;
            
            float targetDP;
            float screenWidthDP = Screen.width / (dpi / 160f);

            float mediaViewHeightDP = screenWidthDP / 1.77f;
            targetDP = 110f + mediaViewHeightDP + EXTRA_PADDING_DP;

            float physicalPixels = targetDP * (dpi / 160f);
            float safeAreaBottom = Screen.safeArea.y;
            
            float nativeAdCanvasHeight = (physicalPixels + safeAreaBottom) / _canvas.scaleFactor;

            _bgRect.sizeDelta = new Vector2(0f, nativeAdCanvasHeight + HEADER_HEIGHT);
        }

        private void Update()
        {
            if (Time.frameCount % 30 == 0)
            {
                UpdatePosition();
            }
        }
    }
}