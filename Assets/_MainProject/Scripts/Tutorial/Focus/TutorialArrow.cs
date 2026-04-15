using UnityEngine;
namespace GameUp.Core.Tutorial
{
    public class TutorialArrow : MonoBehaviour
    {
        [SerializeField] private RectTransform holderTrs;
        [SerializeField] private RectTransform arrowTrs;
        [SerializeField] private float offset = 40f;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private ArrowDirection defaultDirection = ArrowDirection.Up;

        private readonly Vector3[] _uiWorldCorners = new Vector3[4];
        private readonly Vector3[] _worldBoundsCorners = new Vector3[8];

        [Button]
        public void ShowView(RectTransform target)
        {
            ShowView(target, defaultDirection);
        }

        [Button]
        public void ShowView(Transform target)
        {
            ShowView(target, defaultDirection);
        }

        [Button]
        public void ShowView(RectTransform target, ArrowDirection direction)
        {
            ShowView((Transform)target, direction, offset);
        }

        [Button]
        public void ShowView(Transform target, ArrowDirection direction)
        {
            ShowView(target, direction, offset);
        }

        [Button]
        public void ShowView(RectTransform target, ArrowDirection direction, float customOffset)
        {
            ShowView((Transform)target, direction, customOffset);
        }

        [Button]
        public void ShowView(Transform target, ArrowDirection direction, float customOffset)
        {
            if (holderTrs == null || arrowTrs == null)
                return;

            if (!TryGetTargetRect(target, out var anchoredPosition, out var targetSize))
                return;

            var directionVector = GetDirectionVector(direction);
            var distance = GetDistance(targetSize, direction) + customOffset;

            arrowTrs.anchoredPosition = anchoredPosition + directionVector * distance;
            arrowTrs.localEulerAngles = GetArrowEuler(direction);
            gameObject.SetActive(true);
        }

        [Button]
        public void HideView()
        {
            gameObject.SetActive(false);
        }

        private float GetDistance(Vector2 targetSize, ArrowDirection direction)
        {
            if (direction == ArrowDirection.Left || direction == ArrowDirection.Right)
                return targetSize.x * 0.5f;

            return targetSize.y * 0.5f;
        }

        private static Vector2 GetDirectionVector(ArrowDirection direction)
        {
            switch (direction)
            {
                case ArrowDirection.Up:
                    return Vector2.up;
                case ArrowDirection.Down:
                    return Vector2.down;
                case ArrowDirection.Left:
                    return Vector2.left;
                case ArrowDirection.Right:
                    return Vector2.right;
                default:
                    return Vector2.up;
            }
        }

        private static Vector3 GetArrowEuler(ArrowDirection direction)
        {
            switch (direction)
            {
                case ArrowDirection.Up:
                    return Vector3.zero;
                case ArrowDirection.Down:
                    return new Vector3(0f, 0f, 180f);
                case ArrowDirection.Left:
                    return new Vector3(0f, 0f, 90f);
                case ArrowDirection.Right:
                    return new Vector3(0f, 0f, -90f);
                default:
                    return Vector3.zero;
            }
        }

        private bool TryGetTargetRect(Transform target, out Vector2 anchoredPosition, out Vector2 targetSize)
        {
            anchoredPosition = Vector2.zero;
            targetSize = Vector2.zero;
            if (target == null || holderTrs == null)
                return false;

            if (!TryGetScreenRect(target, out var screenRect))
                return false;

            var holderCamera = GetHolderCanvasCamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(holderTrs, screenRect.center, holderCamera, out anchoredPosition))
                return false;

            targetSize = screenRect.size;
            return targetSize.x > 0f && targetSize.y > 0f;
        }

        private bool TryGetScreenRect(Transform target, out Rect screenRect)
        {
            if (target is RectTransform rectTransform)
                return TryGetRectTransformScreenRect(rectTransform, out screenRect);

            return TryGetWorldObjectScreenRect(target, out screenRect);
        }

        private bool TryGetRectTransformScreenRect(RectTransform rectTransform, out Rect screenRect)
        {
            rectTransform.GetWorldCorners(_uiWorldCorners);
            var uiCamera = GetCanvasCamera(rectTransform);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            for (var i = 0; i < _uiWorldCorners.Length; i++)
            {
                var screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, _uiWorldCorners[i]);
                min = Vector2.Min(min, screenPoint);
                max = Vector2.Max(max, screenPoint);
            }

            screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return screenRect.width > 0f && screenRect.height > 0f;
        }

        private bool TryGetWorldObjectScreenRect(Transform target, out Rect screenRect)
        {
            screenRect = default;
            var focusCamera = worldCamera != null ? worldCamera : Camera.main;
            if (focusCamera == null || !TryGetWorldBounds(target, out var bounds))
                return false;

            BuildBoundsCorners(bounds);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var hasVisiblePoint = false;

            for (var i = 0; i < _worldBoundsCorners.Length; i++)
            {
                var screenPoint = focusCamera.WorldToScreenPoint(_worldBoundsCorners[i]);
                if (screenPoint.z <= 0f)
                    continue;

                hasVisiblePoint = true;
                min = Vector2.Min(min, screenPoint);
                max = Vector2.Max(max, screenPoint);
            }

            if (!hasVisiblePoint)
                return false;

            screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return screenRect.width > 0f && screenRect.height > 0f;
        }

        private bool TryGetWorldBounds(Transform target, out Bounds bounds)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                return true;
            }

            var colliders = target.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                bounds = colliders[0].bounds;
                for (var i = 1; i < colliders.Length; i++)
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }

                return true;
            }

            bounds = default;
            return false;
        }

        private void BuildBoundsCorners(Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            _worldBoundsCorners[0] = new Vector3(min.x, min.y, min.z);
            _worldBoundsCorners[1] = new Vector3(min.x, min.y, max.z);
            _worldBoundsCorners[2] = new Vector3(min.x, max.y, min.z);
            _worldBoundsCorners[3] = new Vector3(min.x, max.y, max.z);
            _worldBoundsCorners[4] = new Vector3(max.x, min.y, min.z);
            _worldBoundsCorners[5] = new Vector3(max.x, min.y, max.z);
            _worldBoundsCorners[6] = new Vector3(max.x, max.y, min.z);
            _worldBoundsCorners[7] = new Vector3(max.x, max.y, max.z);
        }

        private Camera GetHolderCanvasCamera()
        {
            return GetCanvasCamera(holderTrs);
        }

        private Camera GetCanvasCamera(Component component)
        {
            var canvas = component.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.worldCamera;
        }


    }

    public enum ArrowDirection
    {
        Up,
        Down,
        Left,
        Right,
    }
}