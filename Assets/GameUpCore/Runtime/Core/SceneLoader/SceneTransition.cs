using System;
using System.Collections;
using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// High-level scene transition with loading screen support.
    /// Shows a loading screen, loads the target scene, then hides the loading screen.
    /// </summary>
    public static class SceneTransition
    {
        /// <summary>Minimum time to display the loading screen.</summary>
        public static float MinLoadingDisplayTime { get; set; } = 1f;

        /// <summary>
        /// Transition to a scene with progress feedback.
        /// </summary>
        /// <param name="sceneName">Target scene name.</param>
        /// <param name="onProgress">Progress callback (0-1).</param>
        /// <param name="onBeforeActivate">Called when load is ready, before activation. Use for fade-out etc.</param>
        /// <param name="onComplete">Called after scene is fully loaded and activated.</param>
        public static void TransitionTo(
            string sceneName,
            Action<float> onProgress = null,
            Action onBeforeActivate = null,
            Action onComplete = null)
        {
            CoroutineRunner.Run(TransitionRoutine(sceneName, onProgress, onBeforeActivate, onComplete));
        }

        static IEnumerator TransitionRoutine(
            string sceneName,
            Action<float> onProgress,
            Action onBeforeActivate,
            Action onComplete)
        {
            float startTime = Time.unscaledTime;
            bool loadDone = false;

            SceneLoader.LoadScene(
                sceneName,
                onProgress: onProgress,
                autoActivate: false
            );

            while (SceneLoader.IsLoading)
            {
                yield return null;
                if (!SceneLoader.IsLoading)
                    break;
            }

            // Ensure minimum display time
            float elapsed = Time.unscaledTime - startTime;
            if (elapsed < MinLoadingDisplayTime)
                yield return new WaitForSecondsRealtime(MinLoadingDisplayTime - elapsed);

            onProgress?.Invoke(1f);
            onBeforeActivate?.Invoke();

            SceneLoader.ActivateScene();

            // Wait one frame for scene activation
            yield return null;
            onComplete?.Invoke();
        }
    }
}
