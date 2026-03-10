namespace GameUp.Core
{
    /// <summary>
    /// Interface for FSM states. Implement on classes representing individual states.
    /// </summary>
    public interface IState
    {
        void OnEnter();
        void OnUpdate();
        void OnFixedUpdate();
        void OnExit();
    }
}
