using UnityEngine;
using DG.Tweening;
using System;

namespace GameUp.Core.Tutorial
{
    public class MultiFocusItem : MonoBehaviour, ICanvasRaycastFilter
    {
        [SerializeField] private RectTransform holderTrs;
        [SerializeField] private RectTransform holeRect, holeRect1;
        [SerializeField] private float offset = 40f;
        [SerializeField] private float showDuration = 0.35f;
        [SerializeField] private float startScale = 1.6f;
        [SerializeField] private float startPadding = 80f;
        [SerializeField] private Ease showEase = Ease.OutCubic;
        [SerializeField] private Camera worldCamera;

        [Tooltip("Bật: lớp phủ nuốt mọi thao tác, chỉ vùng trong holeRect/holeRect1 mới bấm/kéo được. " +
                 "Chỉ có tác dụng khi ảnh nền (DarkPanel) bật Raycast Target.")]
        [SerializeField] private bool blockRaycastOutsideHoles = true;

        private Tween _showTween;
        private readonly Vector3[] _uiWorldCorners = new Vector3[4];
        private readonly Vector3[] _worldBoundsCorners = new Vector3[8];

        private Action _onComplete;

        /// <summary>
        /// Lọc raycast cho mọi Graphic nằm dưới object này (uGUI duyệt ngược lên cha để tìm
        /// <see cref="ICanvasRaycastFilter"/>): trong lỗ focus trả false để pointer xuyên xuống item/nút
        /// bên dưới, ngoài lỗ trả true để lớp phủ chặn thao tác. Nhờ vậy chỉ cần bật Raycast Target của
        /// ảnh nền là khoá được toàn màn hình mà vẫn kéo thả được đúng hai vùng đang hướng dẫn.
        /// </summary>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!blockRaycastOutsideHoles)
                return false;

            return !IsInsideHole(holeRect, screenPoint, eventCamera)
                && !IsInsideHole(holeRect1, screenPoint, eventCamera);
        }

        private static bool IsInsideHole(RectTransform hole, Vector2 screenPoint, Camera eventCamera)
        {
            return hole != null
                && hole.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(hole, screenPoint, eventCamera);
        }

        public void SetOnComplete(Action onComplete)
        {
            _onComplete = onComplete;
        }

        private void OnComplete()
        {
            _onComplete?.Invoke();
            _onComplete = null;
        }

        public void HideView()
        {
            _showTween?.Kill();
            _showTween = null;
            gameObject.Hide();
        }

        [Button]
        public void ShowView(RectTransform target, RectTransform target1, Action onComplete = null)
        {
            ShowView((Transform)target, (Transform)target1, onComplete);
        }

        [Button]
        public void ShowView(Transform target, Transform target1, Action onComplete = null)
        {
            if (holderTrs == null || holeRect == null || holeRect1 == null)
                return;

            if (!TryGetTargetRect(target, out var anchoredPosition0, out var targetSize0))
                return;

            if (!TryGetTargetRect(target1, out var anchoredPosition1, out var targetSize1))
                return;

            gameObject.Show();
            _showTween?.Kill();
            _showTween = ShowViews(anchoredPosition0, targetSize0, anchoredPosition1, targetSize1, onComplete);
        }

        private Tween ShowViews(Vector2 anchoredPosition0, Vector2 targetSize0, Vector2 anchoredPosition1, Vector2 targetSize1, Action onComplete = null)
        {
            var finalSize0 = targetSize0 + Vector2.one * offset;
            var finalSize1 = targetSize1 + Vector2.one * offset;
            var startSize0 = finalSize0 * startScale + Vector2.one * startPadding;
            var startSize1 = finalSize1 * startScale + Vector2.one * startPadding;

            holeRect.anchoredPosition = anchoredPosition0;
            holeRect1.anchoredPosition = anchoredPosition1;
            holeRect.sizeDelta = startSize0;
            holeRect1.sizeDelta = startSize1;

            var sequence = DOTween.Sequence();
            sequence.Join(holeRect.DOSizeDelta(finalSize0, showDuration).SetEase(showEase));
            sequence.Join(holeRect1.DOSizeDelta(finalSize1, showDuration).SetEase(showEase));
            sequence.OnComplete(() =>
            {
                onComplete?.Invoke();
            });
            return sequence;
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

        private void OnDestroy()
        {
            _showTween?.Kill();
        }
    }
}