using System;
using UnityEngine;

namespace GameUp.Core.UI
{
    /// <summary>
    /// Co RectTransform vào vùng an toàn của màn hình (tránh tai thỏ, thanh điều hướng...).
    /// </summary>
    /// <remarks>
    /// Mặc định đọc <see cref="Screen.safeArea"/>. Nền tảng mà Unity không biết vùng bị che (ví dụ
    /// nút menu của TikTok Mini Games trên WebGL) cắm nguồn riêng qua <see cref="SetProvider"/> —
    /// trước khi UI Awake.
    /// </remarks>
    public class SafeArea : MonoBehaviour
    {
        [SerializeField] private bool includeBottom = false;
        [SerializeField] private bool includeTop = false;
        private Vector2 _maxAnchor;
        private Vector2 _minAnchor;

        private RectTransform _rectTransform;
        private Rect _safeArea;

        private static Func<Rect> _provider;

        /// <summary>
        /// Đổi nguồn vùng an toàn (toạ độ pixel màn hình, gốc dưới-trái như <see cref="Screen.safeArea"/>).
        /// Truyền <c>null</c> để quay về <see cref="Screen.safeArea"/>.
        /// </summary>
        public static void SetProvider(Func<Rect> provider)
        {
            _provider = provider;
        }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _safeArea = _provider != null ? _provider() : Screen.safeArea;
            _minAnchor = _safeArea.position;
            _maxAnchor = _minAnchor + _safeArea.size;

            _minAnchor.x /= Screen.width;

            if (!includeBottom) _minAnchor.y /= Screen.height;
            else _minAnchor.y = 0f;

            _maxAnchor.x /= Screen.width;

            if (!includeTop) _maxAnchor.y /= Screen.height;
            else _maxAnchor.y = 1f;

            _rectTransform.anchorMin = _minAnchor;
            _rectTransform.anchorMax = _maxAnchor;
        }
    }
}
