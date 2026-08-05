using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameUp.Core
{
    /// <summary>
    /// Load scene bất đồng bộ có tiến độ và có kiểm soát thời điểm kích hoạt.
    ///
    /// Không phụ thuộc tầng UI: màn Loading chỉ cần lắng nghe
    /// <see cref="OnLoadStarted"/> / <see cref="OnProgress"/> / <see cref="OnLoadCompleted"/>.
    /// </summary>
    public static class GUSceneLoader
    {
        private const string Tag = "SceneLoader";

        /// <summary>Unity chỉ chạy tới 0.9 khi allowSceneActivation = false.</summary>
        private const float ActivationThreshold = 0.9f;

        public static bool IsLoading { get; private set; }

        /// <summary>Tên scene đang được load, rỗng nếu không load gì.</summary>
        public static string LoadingSceneName { get; private set; } = string.Empty;

        /// <summary>Tiến độ 0..1 đã chuẩn hoá (0.9 của Unity được quy về 1).</summary>
        public static float Progress { get; private set; }

        public static event Action<string> OnLoadStarted;
        public static event Action<float> OnProgress;
        public static event Action<string> OnLoadCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsLoading = false;
            LoadingSceneName = string.Empty;
            Progress = 0f;
            OnLoadStarted = null;
            OnProgress = null;
            OnLoadCompleted = null;
        }

        /// <summary>
        /// Load scene theo tên.
        /// </summary>
        /// <param name="minDuration">
        /// Thời gian tối thiểu (giây, unscaled) giữ màn Loading. Đặt &gt; 0 để tránh loading nhấp nháy
        /// khi scene nhẹ, và để animation mở màn Loading kịp chạy hết.
        /// </param>
        public static void LoadAsync(string sceneName, Action onCompleted = null, float minDuration = 0f,
            LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                GULogger.Error(Tag, "LoadAsync called with an empty scene name");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                GULogger.Error(Tag, $"Scene '{sceneName}' không có trong Build Settings.");
                return;
            }

            if (IsLoading)
            {
                GULogger.Warning(Tag, $"Đang load '{LoadingSceneName}', bỏ qua yêu cầu load '{sceneName}'.");
                return;
            }

            CoroutineRunner.RunCoroutineWithoutReturn(
                LoadRoutine(sceneName, () => SceneManager.LoadSceneAsync(sceneName, mode), onCompleted, minDuration));
        }

        /// <summary>Load scene theo build index.</summary>
        public static void LoadAsync(int buildIndex, Action onCompleted = null, float minDuration = 0f,
            LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                GULogger.Error(Tag, $"Build index {buildIndex} nằm ngoài Build Settings.");
                return;
            }

            if (IsLoading)
            {
                GULogger.Warning(Tag, $"Đang load '{LoadingSceneName}', bỏ qua yêu cầu load index {buildIndex}.");
                return;
            }

            CoroutineRunner.RunCoroutineWithoutReturn(
                LoadRoutine($"#{buildIndex}", () => SceneManager.LoadSceneAsync(buildIndex, mode), onCompleted, minDuration));
        }

        /// <summary>Load lại scene đang active.</summary>
        public static void ReloadCurrent(Action onCompleted = null, float minDuration = 0f)
        {
            LoadAsync(SceneManager.GetActiveScene().name, onCompleted, minDuration);
        }

        private static IEnumerator LoadRoutine(string sceneName, Func<AsyncOperation> begin, Action onCompleted,
            float minDuration)
        {
            IsLoading = true;
            LoadingSceneName = sceneName;
            Report(0f);
            OnLoadStarted?.Invoke(sceneName);

            var operation = begin();
            if (operation == null)
            {
                GULogger.Error(Tag, $"Không tạo được AsyncOperation cho '{sceneName}'.");
                Finish(sceneName, onCompleted);
                yield break;
            }

            // Giữ scene chưa kích hoạt để còn kiểm soát minDuration và hiệu ứng chuyển màn.
            operation.allowSceneActivation = false;

            var elapsed = 0f;
            while (operation.progress < ActivationThreshold || elapsed < minDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                var loadRatio = Mathf.Clamp01(operation.progress / ActivationThreshold);
                var timeRatio = minDuration > 0f ? Mathf.Clamp01(elapsed / minDuration) : 1f;
                Report(Mathf.Min(loadRatio, timeRatio));

                yield return null;
            }

            Report(1f);
            operation.allowSceneActivation = true;

            while (!operation.isDone) yield return null;

            Finish(sceneName, onCompleted);
        }

        private static void Finish(string sceneName, Action onCompleted)
        {
            IsLoading = false;
            LoadingSceneName = string.Empty;

            OnLoadCompleted?.Invoke(sceneName);
            onCompleted?.Invoke();
        }

        private static void Report(float progress)
        {
            Progress = Mathf.Clamp01(progress);
            OnProgress?.Invoke(Progress);
        }
    }
}
