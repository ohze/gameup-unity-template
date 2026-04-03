using DG.Tweening;
using UnityEngine;

namespace GameUp.Core.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIBaseAnimation : UIDefaultAnimation
    {
        public RectTransform content;
        [SerializeField] protected CanvasGroup canvasGroup;

        private void OnValidate()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public override IAnimation OnStart()
        {
            mainSequence?.Kill();
            canvasGroup.blocksRaycasts = true;
            mainSequence = DOTween.Sequence().OnComplete(() =>
            {
                OnReverseCompleteCallback?.Invoke();
                OnStartCompleteCallback = null;
            });
            return this;
        }

        public override IAnimation OnReverse()
        {
            canvasGroup.blocksRaycasts = false;
            mainSequence?.Kill();
            mainSequence = DOTween.Sequence().OnComplete(() =>
            {
                OnReverseCompleteCallback?.Invoke();
                OnReverseCompleteCallback = null;
            });
            return this;
        }

        public override IAnimation OnStop()
        {
            mainSequence?.Pause();
            return this;
        }
    }
}