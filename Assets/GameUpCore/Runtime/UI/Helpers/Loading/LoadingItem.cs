#if DOTween__DEPENDENCIES_INSTALLED
using DG.Tweening;
#endif
using UnityEngine;
using UnityEngine.Serialization;

namespace GameUp.Core.UI
{
    /// <summary>
    /// Loading dạng fade + scale + icon xoay.
    /// </summary>
    public class LoadingItem : LoadingOverlayBase
    {
        private const float IntroFadeDuration = 0.2f;
        private const float IntroScaleDuration = 0.25f;

        [FormerlySerializedAs("rotateGo")]
        [SerializeField] private RectTransform rotateTarget;
        [SerializeField] private float rotateSpeed = 360f;

#if DOTween__DEPENDENCIES_INSTALLED
        private Tween _rotateTween;
        private Tween _introFadeTween;
        private Tween _introScaleTween;
#endif

        protected override void ResetVisualStateForPool()
        {
            base.ResetVisualStateForPool();
            if (rotateTarget)
                rotateTarget.localRotation = Quaternion.identity;
        }

        protected override void PlayIntro()
        {
            OverlayGroup.alpha = 0f;
            transform.localScale = Vector3.one * 1.1f;

#if DOTween__DEPENDENCIES_INSTALLED
            _introFadeTween = OverlayGroup.DOFade(1f, IntroFadeDuration)
                .SetUpdate(true);
            _introScaleTween = transform.DOScale(1f, IntroScaleDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() => OnOpened?.Invoke());

            PlayRotate();
#else
            OverlayGroup.alpha = 1f;
            transform.localScale = Vector3.one;
            OnOpened?.Invoke();
#endif
        }

        protected override void StopIntroTweens()
        {
#if DOTween__DEPENDENCIES_INSTALLED
            _rotateTween?.Kill();
            _rotateTween = null;
            _introFadeTween?.Kill();
            _introFadeTween = null;
            _introScaleTween?.Kill();
            _introScaleTween = null;
#endif
        }

#if DOTween__DEPENDENCIES_INSTALLED
        private void PlayRotate()
        {
            if (!rotateTarget)
                return;

            _rotateTween = rotateTarget
                .DORotate(new Vector3(0, 0, -360f), 360f / rotateSpeed, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1)
                .SetUpdate(true);
        }
#endif
    }
}
