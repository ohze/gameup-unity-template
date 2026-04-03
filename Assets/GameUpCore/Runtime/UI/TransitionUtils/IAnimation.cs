using System;

namespace GameUp.Core.UI
{
    public interface IAnimation
    {
        Action OnReverseCompleteCallback { get; set; }
        Action OnStartCompleteCallback { get; set; }
        IAnimation OnStart();
        IAnimation OnReverse();
        IAnimation OnStop();
        IAnimation SetStartCompleteCallback(Action a);
        IAnimation SetReverseCompleteCallback(Action a);
    }
}