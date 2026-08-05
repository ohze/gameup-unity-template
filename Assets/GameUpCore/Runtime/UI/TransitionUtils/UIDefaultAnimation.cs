using System;
#if DOTween__DEPENDENCIES_INSTALLED
using DG.Tweening;
#endif
using UnityEngine;

namespace GameUp.Core.UI
{
    public class UIDefaultAnimation : MonoBehaviour, IAnimation
    {
#if DOTween__DEPENDENCIES_INSTALLED
        protected Sequence mainSequence;
#endif

        public Action OnReverseCompleteCallback { get; set; }
        public Action OnStartCompleteCallback { get; set; }

        /// <summary>
        /// Xóa callback trước khi gọi: nếu trong callback lại đăng ký callback mới
        /// (ví dụ Close ngay khi Open xong) thì callback mới không bị ghi đè thành null.
        /// </summary>
        protected void InvokeStartComplete()
        {
            var callback = OnStartCompleteCallback;
            OnStartCompleteCallback = null;
            callback?.Invoke();
        }

        /// <inheritdoc cref="InvokeStartComplete"/>
        protected void InvokeReverseComplete()
        {
            var callback = OnReverseCompleteCallback;
            OnReverseCompleteCallback = null;
            callback?.Invoke();
        }

        public virtual IAnimation OnStart()
        {
#if DOTween__DEPENDENCIES_INSTALLED
            mainSequence?.Kill();
            mainSequence = DOTween.Sequence().OnComplete(InvokeStartComplete);
#else
            InvokeStartComplete();
#endif
            return this;
        }

        public virtual IAnimation OnReverse()
        {
#if DOTween__DEPENDENCIES_INSTALLED
            mainSequence?.Kill();
            mainSequence = DOTween.Sequence().OnComplete(InvokeReverseComplete);
#else
            InvokeReverseComplete();
#endif
            return this;
        }

        public virtual IAnimation OnStop()
        {
#if DOTween__DEPENDENCIES_INSTALLED
            mainSequence?.Pause();
#endif
            return this;
        }

        public IAnimation SetStartCompleteCallback(Action a)
        {
            OnStartCompleteCallback = a;
            return this;
        }

        public IAnimation SetReverseCompleteCallback(Action a)
        {
            OnReverseCompleteCallback = a;
            return this;
        }
    }
}
