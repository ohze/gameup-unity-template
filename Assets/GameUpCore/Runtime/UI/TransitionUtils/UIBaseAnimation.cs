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

        /// <summary>Lớp con phải override thay vì khai báo lại OnValidate — nếu không Unity sẽ bỏ qua bản này.</summary>
        protected virtual void OnValidate()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public override IAnimation OnStart()
        {
            canvasGroup.blocksRaycasts = true;
#if DOTween__DEPENDENCIES_INSTALLED
            mainSequence?.Kill();
            mainSequence = DOTween.Sequence().OnComplete(InvokeStartComplete);
#else
            InvokeStartComplete();
#endif
            return this;
        }

        public override IAnimation OnReverse()
        {
            canvasGroup.blocksRaycasts = false;
#if DOTween__DEPENDENCIES_INSTALLED
            mainSequence?.Kill();
            mainSequence = DOTween.Sequence().OnComplete(InvokeReverseComplete);
#else
            InvokeReverseComplete();
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