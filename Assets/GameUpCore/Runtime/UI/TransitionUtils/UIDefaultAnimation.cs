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

        public virtual IAnimation OnStart()
        {
#if DOTween__DEPENDENCIES_INSTALLED
            mainSequence?.Kill();
            mainSequence = DOTween.Sequence().OnComplete(() =>
            {
                OnStartCompleteCallback?.Invoke();
                OnStartCompleteCallback = null;
            });
#else
            OnStartCompleteCallback?.Invoke();
            OnStartCompleteCallback = null;
#endif
            return this;
        }

        public virtual IAnimation OnReverse()
        {
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