using DG.Tweening;
using UnityEngine;

namespace GameUp.Core.UI
{
    public class UIScaleAnimation : UIBaseAnimation
    {
        public float startSize = 0.7f, middleSize = 1.05f, endSize = 1;
        public float firstTime = 0.2f, secondTime = 0.1f;

        public override IAnimation OnStart()
        {
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
            return this;
        }

        public override IAnimation OnReverse()
        {
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
            return this;
        }

    }
}