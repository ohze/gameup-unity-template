using System;
using System.Collections.Generic;

namespace GameUp.Core
{
    /// <summary>
    /// Generic finite state machine. States are registered by type and transitioned via ChangeState.
    /// </summary>
    public sealed class StateMachine
    {
        readonly Dictionary<Type, IState> _states = new();

        public IState CurrentState { get; private set; }
        public IState PreviousState { get; private set; }

        public event Action<IState> OnStateChanged;

        public void AddState(IState state)
        {
            _states[state.GetType()] = state;
        }

        /// <summary>Transition to state of type T. Calls OnExit on current, OnEnter on new.</summary>
        public void ChangeState<T>() where T : IState
        {
            var type = typeof(T);
            if (!_states.TryGetValue(type, out var next))
                throw new InvalidOperationException($"State {type.Name} not registered.");

            if (CurrentState == next) return;

            PreviousState = CurrentState;
            CurrentState?.OnExit();
            CurrentState = next;
            CurrentState.OnEnter();
            OnStateChanged?.Invoke(CurrentState);
        }

        /// <summary>Call in MonoBehaviour.Update.</summary>
        public void Update() => CurrentState?.OnUpdate();

        /// <summary>Call in MonoBehaviour.FixedUpdate.</summary>
        public void FixedUpdate() => CurrentState?.OnFixedUpdate();

        /// <summary>Check if the current state is of type T.</summary>
        public bool IsInState<T>() where T : IState
            => CurrentState != null && CurrentState.GetType() == typeof(T);

        public T GetState<T>() where T : IState
            => _states.TryGetValue(typeof(T), out var state) ? (T)state : default;
    }
}
