using System;
using DG.Tweening;
using UnityEngine;

namespace GameUp.Core.UI
{
    public class UIDefaultAnimation : MonoBehaviour, IAnimation
    {
        protected Sequence mainSequence;

        public Action OnReverseCompleteCallback { get; set; }
        public Action OnStartCompleteCallback { get; set; }

        public virtual IAnimation OnStart()
        {
            mainSequence?.Kill();
            mainSequence = DOTween.Sequence().OnComplete(() =>
            {
                OnStartCompleteCallback?.Invoke();
                OnStartCompleteCallback = null;
            });
            return this;
        }

        public virtual IAnimation OnReverse()
        {
            mainSequence?.Kill();
            mainSequence = DOTween.Sequence().OnComplete(() =>
            {
                OnReverseCompleteCallback?.Invoke();
                OnReverseCompleteCallback = null;
            });
            return this;
        }

        public virtual IAnimation OnStop()
        {
            mainSequence?.Pause();
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