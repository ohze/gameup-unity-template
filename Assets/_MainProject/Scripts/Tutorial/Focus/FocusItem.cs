using UnityEngine;
using DG.Tweening;
using System;
namespace GameUp.Core.Tutorial
{
    public class FocusItem : MonoBehaviour
    {
        [SerializeField] private GameObject blockRaycast;
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform holderTrs;
        [SerializeField] private RectTransform rightTrs, leftTrs, upTrs, downTrs;
        [SerializeField] private RectTransform focusTrs;
        [Header("Settings")]
        [SerializeField] private float showDuration = 0.4f;
        [SerializeField] private float hideDuration = 0.4f;
        [SerializeField] private float offset = 40;
        [SerializeField] private float offsetPadding = 10;
        [SerializeField] private Camera worldCamera;

        private Tween _tween;
        private readonly Vector3[] _uiWorldCorners = new Vector3[4];
        private readonly Vector3[] _worldBoundsCorners = new Vector3[8];

        private Action _onComplete;

        public void SetOnComplete(Action onComplete)
        {
            _onComplete = onComplete;
        }

        private void OnComplete()
        {
            _onComplete?.Invoke();
            _onComplete = null;
        }

        private void UpdateViewByCover()
        {
            var mainSize = holderTrs.rect;
            var w = mainSize.width / 2;
            var h = mainSize.height / 2;
            var deltaSize = focusTrs.sizeDelta;
            var focusPos = focusTrs.anchoredPosition;
            var rightWidth = w - focusPos.x - deltaSize.x / 2;
            var leftWidth = w + focusPos.x - deltaSize.x / 2;
            var upHeight = h - focusPos.y - deltaSize.y / 2;
            var downHeight = h + focusPos.y - deltaSize.y / 2;
            rightTrs.ChangeSizeX(rightWidth + offsetPadding);
            leftTrs.ChangeSizeX(leftWidth + offsetPadding);

            upTrs.SetLeft(leftWidth + offsetPadding);
            upTrs.SetRight(rightWidth + offsetPadding);
            upTrs.ChangeSizeY(upHeight + offsetPadding);

            downTrs.SetLeft(leftWidth + offsetPadding);
            downTrs.SetRight(rightWidth + offsetPadding);
            downTrs.ChangeSizeY(downHeight + offsetPadding);
        }

        [Button]
        public void HideView()
        {
            _tween?.Kill();
            _tween = canvasGroup.DOFade(0, hideDuration).OnComplete(() =>
            {
                gameObject.SetActive(false);
            });
        }

        [Button]
        public void ShowView(RectTransform target, Action onComplete = null)
        {
            ShowView((Transform)target, onComplete);
        }

        [Button]
        public void ShowView(Transform target, Action onComplete = null)
        {

            if (!TryGetTargetRect(target, out var anchoredPosition, out var targetSize))
                return;
            blockRaycast.SetActive(true);
            gameObject.SetActive(true);
            canvasGroup.alpha = 1;
            _tween?.Kill();
            _tween = ShowViews(anchoredPosition, targetSize + Vector2.one * offset, onComplete);
        }

        public Tween ShowViews(Vector2 anchoredPosition, Vector2 pivotDeltaSize, Action onComplete = null)
        {
            focusTrs.anchoredPosition = anchoredPosition;
            focusTrs.sizeDelta = holderTrs.rect.size + Vector2.one * 200;
            UpdateViewByCover();
            return focusTrs.DOSizeDelta(pivotDeltaSize, showDuration).OnUpdate(UpdateViewByCover).OnComplete(() =>
            {
                onComplete?.Invoke();
                blockRaycast.SetActive(false);
            });
        }

        private bool TryGetTargetRect(Transform target, out Vector2 anchoredPosition, out Vector2 targetSize)
        {
            anchoredPosition = Vector2.zero;
            targetSize = Vector2.zero;
            if (target == null || holderTrs == null || focusTrs == null)
                return false;

            if (!TryGetScreenRect(target, out var screenRect))
                return false;

            var holderCamera = GetHolderCanvasCamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(holderTrs, screenRect.center, holderCamera, out anchoredPosition))
                return false;

            targetSize = screenRect.size;
            return targetSize.x > 0 && targetSize.y > 0;
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
            return screenRect.width > 0 && screenRect.height > 0;
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
            return screenRect.width > 0 && screenRect.height > 0;
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

        private void OnDestroy()
        {
            _tween?.Kill();
        }
    }
}