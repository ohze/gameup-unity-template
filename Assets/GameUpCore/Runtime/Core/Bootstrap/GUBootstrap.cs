using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// Chạy các bước khởi tạo theo đúng thứ tự đăng ký và báo tiến độ ra ngoài.
    /// Mỗi bước có timeout riêng: bước treo sẽ bị bỏ qua kèm log lỗi thay vì làm kẹt cả game ở màn Loading.
    ///
    /// Dùng ở scene Boot:
    /// <code>
    /// GUBootstrap.AddStep(AddressableDataHolder.Instance);
    /// GUBootstrap.AddStep("Audio", () => AudioManager.PreloadIdentities());
    /// GUBootstrap.OnProgress += (p, step) => loadingBar.Set(p, step);
    /// GUBootstrap.Run(() => GUSceneLoader.LoadAsync("MainMenu"));
    /// </code>
    /// </summary>
    public static class GUBootstrap
    {
        private const string Tag = "Bootstrap";
        private const float DefaultStepTimeout = 15f;

        public enum State
        {
            Idle,
            Running,
            Done
        }

        private class Step
        {
            public string Name;
            public Action Begin;
            public Func<bool> IsDone;
            public float Timeout;
        }

        private static readonly List<Step> Steps = new();

        public static State CurrentState { get; private set; } = State.Idle;

        /// <summary>Tiến độ 0..1 tính theo số bước đã xong.</summary>
        public static float Progress { get; private set; }

        /// <summary>Tên các bước bị timeout — dùng để báo lỗi/analytics thay vì im lặng bỏ qua.</summary>
        public static IReadOnlyList<string> FailedSteps => FailedStepNames;

        private static readonly List<string> FailedStepNames = new();

        /// <summary>(tiến độ 0..1, tên bước đang chạy)</summary>
        public static event Action<float, string> OnProgress;

        public static event Action OnCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Steps.Clear();
            FailedStepNames.Clear();
            CurrentState = State.Idle;
            Progress = 0f;
            OnProgress = null;
            OnCompleted = null;
        }

        /// <summary>Thêm một service <see cref="IInitial"/>: gọi Initialize() rồi chờ Initialized = true.</summary>
        public static void AddStep(IInitial service, string name = null, float timeout = DefaultStepTimeout)
        {
            if (service == null)
            {
                GULogger.Error(Tag, "AddStep called with a null service");
                return;
            }

            AddStep(name ?? service.GetType().Name, service.Initialize, () => service.Initialized, timeout);
        }

        /// <summary>
        /// Thêm một bước tuỳ ý. Bỏ trống <paramref name="isDone"/> nếu bước chạy đồng bộ (xong ngay sau Begin).
        /// </summary>
        public static void AddStep(string name, Action begin, Func<bool> isDone = null, float timeout = DefaultStepTimeout)
        {
            if (CurrentState == State.Running)
            {
                GULogger.Warning(Tag, $"AddStep('{name}') called while bootstrap is already running — bước này sẽ bị bỏ qua.");
                return;
            }

            Steps.Add(new Step
            {
                Name = string.IsNullOrEmpty(name) ? $"Step {Steps.Count}" : name,
                Begin = begin,
                IsDone = isDone,
                Timeout = timeout
            });
        }

        /// <summary>Chạy toàn bộ các bước đã đăng ký. Gọi lại khi đang chạy sẽ bị bỏ qua.</summary>
        public static void Run(Action onCompleted = null)
        {
            if (CurrentState == State.Running)
            {
                GULogger.Warning(Tag, "Run() called while bootstrap is already running.");
                return;
            }

            if (onCompleted != null) OnCompleted += onCompleted;

            CurrentState = State.Running;
            FailedStepNames.Clear();
            Progress = 0f;

            CoroutineRunner.RunCoroutineWithoutReturn(RunRoutine());
        }

        private static IEnumerator RunRoutine()
        {
            var total = Mathf.Max(1, Steps.Count);

            for (var i = 0; i < Steps.Count; i++)
            {
                var step = Steps[i];
                Report(i / (float)total, step.Name);

                try
                {
                    step.Begin?.Invoke();
                }
                catch (Exception e)
                {
                    // Một bước lỗi không được phép làm chết cả chuỗi khởi tạo.
                    FailedStepNames.Add(step.Name);
                    GULogger.Exception(e, Tag);
                    continue;
                }

                if (step.IsDone == null) continue;

                var elapsed = 0f;
                while (!IsStepDone(step))
                {
                    if (elapsed >= step.Timeout)
                    {
                        FailedStepNames.Add(step.Name);
                        GULogger.Error(Tag, $"Step '{step.Name}' timed out after {step.Timeout:0.#}s — bỏ qua và chạy tiếp.");
                        break;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            CurrentState = State.Done;
            Report(1f, string.Empty);

            var completed = OnCompleted;
            OnCompleted = null;
            completed?.Invoke();
        }

        private static bool IsStepDone(Step step)
        {
            try
            {
                return step.IsDone();
            }
            catch (Exception e)
            {
                GULogger.Exception(e, Tag);
                return true;
            }
        }

        private static void Report(float progress, string stepName)
        {
            Progress = Mathf.Clamp01(progress);
            OnProgress?.Invoke(Progress, stepName);
        }
    }
}
