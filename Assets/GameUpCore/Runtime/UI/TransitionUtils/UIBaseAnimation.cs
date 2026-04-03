#if DOTween__DEPENDENCIES_INSTALLED
using DG.Tweening;
#endif
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
#if DOTween__DEPENDENCIES_INSTALLED
            mainSequence?.Kill();
            canvasGroup.blocksRaycasts = true;
            mainSequence = DOTween.Sequence().OnComplete(() =>
            {
                OnReverseCompleteCallback?.Invoke();
                OnStartCompleteCallback = null;
            });
#else
            canvasGroup.blocksRaycasts = true;
            OnStartCompleteCallback?.Invoke();
            OnStartCompleteCallback = null;
#endif
            return this;
        }

        public override IAnimation OnReverse()
        {
            canvasGroup.blocksRaycasts = false;
#if DOTween__DEPENDENCIES_INSTALLED
            mainSequence?.Kill();
            mainSequence = DOTween.Sequence().OnComplete(() =>
            {
                OnReverseCompleteCallback?.Invoke();
                OnReverseCompleteCallback = null;
            });
#else
            OnReverseCompleteCallback?.Invoke();
            OnReverseCompleteCallback = null;
#endif
            return this;
        }

        public override IAnimation OnStop()
        {
#if DOTween__DEPENDENCIES_INSTALLED
            mainSequence?.Pause();
#endif
            return this;
        }
    }
}