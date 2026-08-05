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
                    InvokeStartComplete();
                });
            mainSequence.Restart();
#else
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            InvokeStartComplete();
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
                    InvokeReverseComplete();
                });
            mainSequence.Restart();
#else
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0f;
            InvokeReverseComplete();
#endif
            return this;
        }
    }
}