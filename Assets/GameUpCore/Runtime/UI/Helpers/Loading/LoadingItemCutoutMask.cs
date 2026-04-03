#if DOTween__DEPENDENCIES_INSTALLED
using DG.Tweening;
#endif
using UnityEngine;
using UnityEngine.Serialization;

namespace GameUp.Core.UI
{
    /// <summary>
    /// Loading mở vòng cutout từ tâm. Có thể giữ một vùng đen tại tâm trong thời gian cấu hình rồi mới chạy tween mở mask.
    /// </summary>
    public class LoadingItemCutoutMask : LoadingOverlayBase
    {
        [SerializeField] private RectTransform cutoutMask;
        [FormerlySerializedAs("parent")]
        [SerializeField] private RectTransform boundsParent;
        [SerializeField] private float maskAnimationDuration = 1f;

        [Tooltip("Thời gian (giây) hiển thị đen tâm trước khi bắt đầu mở rộng cutout. 0 = bỏ qua.")]
        [SerializeField] private float centerBlackHoldDuration = 0.1f;

        [Tooltip("CanvasGroup của layer đen tại tâm (child UI). Để trống thì không flash đen nhưng vẫn có thể delay nếu duration > 0.")]
        [SerializeField] private CanvasGroup centerBlack;

#if DOTween__DEPENDENCIES_INSTALLED
        private Tween _maskTween;
        private Tween _centerHoldTween;
#endif
        private Vector3 DefaultCutoutLocalPosition => Vector3.zero;

        private Vector3? _pendingLocalPosition;
        private Transform _pendingStartReference;
        private Camera _pendingWorldObjectCamera;

        protected override void ResetVisualStateForPool()
        {
            base.ResetVisualStateForPool();
            if (cutoutMask)
            {
                cutoutMask.sizeDelta = Vector2.zero;
                cutoutMask.localPosition = Vector3.zero;
            }

            if (centerBlack)
                centerBlack.alpha = 0f;
        }

        /// <summary>
        /// Gọi sau spawn, trước <c>Open</c>.
        /// Ưu tiên: <paramref name="localPositionInMaskParent"/> (null = không dùng) →
        /// <paramref name="startReference"/> (null = dùng vị trí prefab).
        /// Với object 3D ngoài Canvas, truyền <paramref name="worldObjectCamera"/> (mặc định: Canvas.worldCamera / <see cref="Camera.main"/>).
        /// </summary>
        public void ApplyCutoutStartForOpen(
            Vector3? localPositionInMaskParent = null,
            Transform startReference = null,
            Camera worldObjectCamera = null)
        {
            _pendingLocalPosition = localPositionInMaskParent;
            _pendingStartReference = startReference;
            _pendingWorldObjectCamera = worldObjectCamera;
        }

        protected override void PlayIntro()
        {
#if DOTween__DEPENDENCIES_INSTALLED
            if (!cutoutMask || !boundsParent)
            {
                OverlayGroup.alpha = 1f;
                OnOpened?.Invoke();
                return;
            }

            StopIntroTweensOnly();

            cutoutMask.sizeDelta = Vector2.zero;
            cutoutMask.localPosition = ResolveCutoutStartLocalPosition();
            ClearPendingStart();

            OverlayGroup.alpha = 1f;

            if (centerBlackHoldDuration > 0f)
            {
                if (centerBlack)
                    centerBlack.alpha = 1f;

                _centerHoldTween = DOVirtual.DelayedCall(centerBlackHoldDuration, BeginCutoutExpandAfterHold)
                    .SetUpdate(true);
            }
            else
            {
                BeginCutoutExpandAfterHold();
            }
#else
            OverlayGroup.alpha = 1f;
            if (cutoutMask)
            {
                cutoutMask.sizeDelta = Vector2.zero;
                cutoutMask.localPosition = ResolveCutoutStartLocalPosition();
            }

            if (centerBlack)
                centerBlack.alpha = 0f;

            ClearPendingStart();
            OnOpened?.Invoke();
#endif
        }

#if DOTween__DEPENDENCIES_INSTALLED
        private void BeginCutoutExpandAfterHold()
        {
            _centerHoldTween = null;

            if (centerBlack)
                centerBlack.alpha = 0f;

            if (!cutoutMask || !boundsParent)
            {
                OnOpened?.Invoke();
                return;
            }

            float targetSize = GetDiagonalCoverSize(boundsParent);
            _maskTween?.Kill();
            _maskTween = cutoutMask.DOSizeDelta(new Vector2(targetSize, targetSize), maskAnimationDuration)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .OnComplete(() => OnOpened?.Invoke());
        }
#endif

        private void StopIntroTweensOnly()
        {
#if DOTween__DEPENDENCIES_INSTALLED
            _centerHoldTween?.Kill();
            _centerHoldTween = null;
            _maskTween?.Kill();
            _maskTween = null;
#endif
        }

        private void ClearPendingStart()
        {
            _pendingLocalPosition = null;
            _pendingStartReference = null;
            _pendingWorldObjectCamera = null;
        }

        private Vector3 ResolveCutoutStartLocalPosition()
        {
            if (_pendingLocalPosition.HasValue)
                return _pendingLocalPosition.Value;

            if (!_pendingStartReference)
                return DefaultCutoutLocalPosition;

            RectTransform maskParent = cutoutMask.parent as RectTransform;
            if (!maskParent)
            {
                GULogger.Warning("LoadingItemCutoutMask", "cutoutMask.parent không phải RectTransform — giữ vị trí prefab.");
                return DefaultCutoutLocalPosition;
            }

            if (_pendingStartReference is RectTransform uiRef)
                return LocalPointFromCanvasReference(maskParent, uiRef);

            return LocalPointFromWorldTransform(maskParent, _pendingStartReference, _pendingWorldObjectCamera);
        }

        private Vector3 LocalPointFromCanvasReference(RectTransform maskParent, RectTransform uiRef)
        {
            Vector3 worldCenter = uiRef.TransformPoint(uiRef.rect.center);
            Vector3 local = maskParent.InverseTransformPoint(worldCenter);
            return new Vector3(local.x, local.y, DefaultCutoutLocalPosition.z);
        }

        private Vector3 LocalPointFromWorldTransform(RectTransform maskParent, Transform worldRef, Camera worldObjectCamera)
        {
            Canvas canvas = maskParent.GetComponentInParent<Canvas>();
            Vector3 worldPoint = worldRef.position;
            Camera cam = worldObjectCamera;
            if (cam == null && canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera;
            if (cam == null)
                cam = Camera.main;

            if (cam == null)
            {
                GULogger.Warning(
                    "LoadingItemCutoutMask",
                    "Cần Camera để chiếu Transform 3D lên UI — không tìm thấy, dùng InverseTransformPoint (có thể lệch).");
                Vector3 fallback = maskParent.InverseTransformPoint(worldPoint);
                return new Vector3(fallback.x, fallback.y, DefaultCutoutLocalPosition.z);
            }

            Vector3 screen = cam.WorldToScreenPoint(worldPoint);
            float z = DefaultCutoutLocalPosition.z;

            if (screen.z <= 0f)
            {
                GULogger.Warning("LoadingItemCutoutMask", "Điểm tham chiếu nằm sau camera — dùng vị trí (0,0) local.");
                return new Vector3(0f, 0f, z);
            }

            Camera canvasEventCam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                canvasEventCam = canvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    maskParent,
                    new Vector2(screen.x, screen.y),
                    canvasEventCam,
                    out Vector2 local2d))
            {
                return new Vector3(local2d.x, local2d.y, z);
            }

            GULogger.Warning("LoadingItemCutoutMask", "ScreenPointToLocalPointInRectangle thất bại — dùng InverseTransformPoint.");
            Vector3 fb = maskParent.InverseTransformPoint(worldPoint);
            return new Vector3(fb.x, fb.y, z);
        }

        protected override void StopIntroTweens()
        {
            StopIntroTweensOnly();
            if (cutoutMask)
                cutoutMask.sizeDelta = Vector2.zero;
            if (centerBlack)
                centerBlack.alpha = 0f;
        }

        private static float GetDiagonalCoverSize(RectTransform rect)
        {
            float w = rect.rect.width;
            float h = rect.rect.height;
            return Mathf.Sqrt(w * w + h * h);
        }

        public override void Close()
        {
            StopIntroTweens();
#if DOTween__DEPENDENCIES_INSTALLED
            _autoCloseTween?.Kill();
            _autoCloseTween = null;
#endif

            KillCloseTweens();
            FinishClose();
        }
    }
}
