using UnityEngine;
using GameUp.Core;

namespace GameUp.Samples
{
    /// <summary>
    /// Sample bootstrap demonstrating Core systems: EventBus, FSM, Pool, Save/Load, Audio, Timer.
    /// Attach to a GameObject in CoreDemoScene.
    /// </summary>
    public sealed class CoreDemoBootstrap : MonoBehaviour
    {
        struct OnDemoEvent : IEvent
        {
            public string Message;
        }

        class IdleState : BaseState
        {
            public override void OnEnter() => GLogger.Log("FSM", "Entered Idle");
            public override void OnExit() => GLogger.Log("FSM", "Exited Idle");
        }

        class PlayState : BaseState
        {
            public override void OnEnter() => GLogger.Log("FSM", "Entered Play");
        }

        EventBinding<OnDemoEvent> _eventBinding;
        StateMachine _fsm;
        ObjectPool<PoolItem> _pool;
        Timer _timer;

        void Start()
        {
            GLogger.Log("CoreDemo", "=== Core Demo Started ===");

            // EventBus
            _eventBinding = EventBus<OnDemoEvent>.Subscribe(e =>
                GLogger.Log("EventBus", $"Received: {e.Message}"));
            EventBus<OnDemoEvent>.Publish(new OnDemoEvent { Message = "Hello from CoreDemo!" });

            // FSM
            _fsm = new StateMachine();
            _fsm.AddState(new IdleState());
            _fsm.AddState(new PlayState());
            _fsm.ChangeState<IdleState>();
            _fsm.ChangeState<PlayState>();

            // Object Pool
            _pool = new ObjectPool<PoolItem>(preWarm: 5);
            var item = _pool.Get();
            GLogger.Log("Pool", $"Got item, inactive count: {_pool.CountInactive}");
            _pool.Release(item);
            GLogger.Log("Pool", $"Released item, inactive count: {_pool.CountInactive}");

            // Save/Load
            var saveSystem = new JsonSaveSystem();
            var data = new DemoSaveData { PlayerName = "Demo", HighScore = 9999 };
            saveSystem.Save("demo_save", data);
            var loaded = saveSystem.Load<DemoSaveData>("demo_save");
            GLogger.Log("Save", $"Loaded: {loaded.PlayerName}, Score: {loaded.HighScore}");
            saveSystem.Delete("demo_save");

            // Timer
            _timer = new Timer(3f);
            _timer.OnCompleted += () => GLogger.Log("Timer", "3-second timer completed!");
            _timer.OnTick += elapsed => GLogger.Verbose("Timer", $"Tick: {elapsed:F1}s");
            _timer.Start();
        }

        void Update()
        {
            _fsm?.Update();
            _timer?.Tick(Time.deltaTime);
        }

        void OnDestroy()
        {
            _eventBinding?.Dispose();
        }

        [System.Serializable]
        class DemoSaveData
        {
            public string PlayerName;
            public int HighScore;
        }

        class PoolItem : IPoolable
        {
            public void OnSpawn() => GLogger.Verbose("Pool", "Item spawned");
            public void OnDespawn() => GLogger.Verbose("Pool", "Item despawned");
        }
    }
}
