using NUnit.Framework;
using GameUp.Core;

namespace GameUp.Core.Tests
{
    class IdleState : BaseState
    {
        public bool Entered;
        public bool Exited;
        public override void OnEnter() => Entered = true;
        public override void OnExit() => Exited = true;
    }

    class RunState : BaseState
    {
        public bool Entered;
        public override void OnEnter() => Entered = true;
    }

    public class FSMTests
    {
        [Test]
        public void ChangeState_CallsEnterAndExit()
        {
            var fsm = new StateMachine();
            var idle = new IdleState();
            var run = new RunState();
            fsm.AddState(idle);
            fsm.AddState(run);

            fsm.ChangeState<IdleState>();
            Assert.IsTrue(idle.Entered);

            fsm.ChangeState<RunState>();
            Assert.IsTrue(idle.Exited);
            Assert.IsTrue(run.Entered);
        }

        [Test]
        public void IsInState_ReturnsCorrectly()
        {
            var fsm = new StateMachine();
            fsm.AddState(new IdleState());
            fsm.AddState(new RunState());

            fsm.ChangeState<IdleState>();
            Assert.IsTrue(fsm.IsInState<IdleState>());
            Assert.IsFalse(fsm.IsInState<RunState>());
        }

        [Test]
        public void PreviousState_TracksCorrectly()
        {
            var fsm = new StateMachine();
            fsm.AddState(new IdleState());
            fsm.AddState(new RunState());

            fsm.ChangeState<IdleState>();
            fsm.ChangeState<RunState>();

            Assert.IsTrue(fsm.PreviousState is IdleState);
            Assert.IsTrue(fsm.CurrentState is RunState);
        }

        [Test]
        public void OnStateChanged_Fires()
        {
            var fsm = new StateMachine();
            fsm.AddState(new IdleState());

            IState changed = null;
            fsm.OnStateChanged += s => changed = s;
            fsm.ChangeState<IdleState>();

            Assert.IsNotNull(changed);
            Assert.IsTrue(changed is IdleState);
        }
    }
}
