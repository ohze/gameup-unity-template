namespace GameUp.Core
{
    /// <summary>
    /// Abstract convenience base for states. Override only the methods you need.
    /// </summary>
    public abstract class BaseState : IState
    {
        public virtual void OnEnter() { }
        public virtual void OnUpdate() { }
        public virtual void OnFixedUpdate() { }
        public virtual void OnExit() { }
    }
}
