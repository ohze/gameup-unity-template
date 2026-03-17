using System;
using GameUp.Core;
using UnityEngine;

namespace GameUp.Core.UI
{
    public class UIBaseView : MonoBehaviour, IView, IAnimate
    {
        protected IAnimation _anim;

        protected virtual void Awake()
        {
            _anim = GetComponent<IAnimation>() ?? gameObject.AddComponent<UIDefaultAnimation>();
        }

        public void Open()
        {
            OnOpen();
        }

        public void Close()
        {
            OnClose();
        }

        #region IAnimate

        public virtual void OnOpen()
        {
            _anim.OnStop();
            _anim.OnStart();
        }

        public void OnStop()
        {
            _anim.OnStop();
        }

        public virtual void OnClose(Action onComplete = null)
        {
            _anim.OnStop();
            _anim.OnReverse().SetReverseCompleteCallback(() =>
            {
                gameObject.Hide();
                onComplete?.Invoke();
            });
        }

        #endregion
    }
}