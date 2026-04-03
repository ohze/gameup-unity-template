using System;

namespace GameUp.Core.UI
{
    public interface IAnimate
    {
        void OnOpen();
        void OnStop();
        void OnClose(Action onComplete = null);
    }
}