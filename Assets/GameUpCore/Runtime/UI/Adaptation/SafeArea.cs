using UnityEngine;

namespace GameUp.Core.UI
{
    public class SafeArea : MonoBehaviour
    {
        [SerializeField] private bool includeBottom = false;
        [SerializeField] private bool includeTop = false;
        private Vector2 _maxAnchor;
        private Vector2 _minAnchor;

        private RectTransform _rectTransform;
        private Rect _safeArea;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _safeArea = Screen.safeArea;
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