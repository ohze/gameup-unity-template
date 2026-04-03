#if DOTween__DEPENDENCIES_INSTALLED
using DG.Tweening;
#endif
using UnityEngine;

namespace GameUp.Core.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIFadeAnimation : UIBaseAnimation
    {
        public float fadeTime = 0.25f;

        public override IAnimation OnStart()
        {
#if DOTween__DEPENDENCIES_INSTALLED
            mainSequence?.Kill();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 0;
            mainSequence = DOTween.Sequence()
                .Append(canvasGroup.DOFade(1, fadeTime))
                .OnComplete(() =>
                {
                    OnStartCompleteCallback?.Invoke();
                    OnStartCompleteCallback = null;
                });
            mainSequence.Restart();
#else
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            OnStartCompleteCallback?.Invoke();
            OnStartCompleteCallback = null;
#endif
            return this;
        }

        public override IAnimation OnReverse()
        {
#if DOTween__DEPENDENCIES_INSTALLED
            mainSequence?.Kill();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 1;
            mainSequence = DOTween.Sequence()
                .Append(canvasGroup.DOFade(0, fadeTime)).OnComplete(() =>
                {
                    OnReverseCompleteCallback?.Invoke();
                    OnReverseCompleteCallback = null;
                });
            mainSequence.Restart();
#else
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0f;
            OnReverseCompleteCallback?.Invoke();
            OnReverseCompleteCallback = null;
#endif
            return this;
        }
    }
}