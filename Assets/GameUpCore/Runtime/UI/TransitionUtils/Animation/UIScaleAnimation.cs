#if DOTween__DEPENDENCIES_INSTALLED
using DG.Tweening;
#endif
using UnityEngine;

namespace GameUp.Core.UI
{
    public class UIScaleAnimation : UIBaseAnimation
    {
        public float startSize = 0.7f, middleSize = 1.05f, endSize = 1;
        public float firstTime = 0.2f, secondTime = 0.1f;

        public override IAnimation OnStart()
        {
#if DOTween__DEPENDENCIES_INSTALLED
            mainSequence?.Kill();
            content.localScale = Vector3.one * startSize;
            canvasGroup.alpha = 0;
            mainSequence = DOTween.Sequence()
                .Append(content.DOScale(middleSize, firstTime))
                .Join(canvasGroup.DOFade(1, firstTime))
                .Append(content.DOScale(endSize, secondTime))
                .OnComplete(() =>
                {
                    OnStartCompleteCallback?.Invoke();
                    OnStartCompleteCallback = null;
                });
            ;
            mainSequence.Restart();
#else
            content.localScale = Vector3.one * endSize;
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
            content.localScale = Vector3.one * endSize;
            canvasGroup.alpha = 1;
            mainSequence = DOTween.Sequence()
                .Append(content.DOScale(middleSize, secondTime))
                .Append(content.DOScale(startSize + 0.1f, firstTime))
                .Join(canvasGroup.DOFade(0, firstTime))
                .OnComplete(() =>
                {
                    OnReverseCompleteCallback?.Invoke();
                    OnReverseCompleteCallback = null;
                });
            ;
#else
            content.localScale = Vector3.one * startSize;
            canvasGroup.alpha = 0f;
            OnReverseCompleteCallback?.Invoke();
            OnReverseCompleteCallback = null;
#endif
            return this;
        }

    }
}