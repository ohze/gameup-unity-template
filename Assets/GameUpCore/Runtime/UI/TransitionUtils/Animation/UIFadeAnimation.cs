using DG.Tweening;
using UnityEngine;

namespace GameUp.Core.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIFadeAnimation : UIBaseAnimation
    {
        public float fadeTime = 0.25f;

        public override IAnimation OnStart()
        {
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
            return this;
        }

        public override IAnimation OnReverse()
        {
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
            return this;
        }
    }
}