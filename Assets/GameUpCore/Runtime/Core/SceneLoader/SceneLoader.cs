using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameUp.Core
{
    /// <summary>
    /// Async scene loader with progress callback.
    /// Uses allowSceneActivation=false to hold at 90% until activation is requested.
    /// </summary>
    public static class SceneLoader
    {
        static AsyncOperation _currentOp;

        public static bool IsLoading => _currentOp != null && !_currentOp.isDone;

        /// <summary>
        /// Load a scene asynchronously.
        /// </summary>
        /// <param name="sceneName">Scene to load.</param>
        /// <param name="onProgress">Progress callback (0.0 to 1.0).</param>
        /// <param name="onComplete">Called when scene is fully loaded and activated.</param>
        /// <param name="autoActivate">If false, holds at 90% and waits for ActivateScene().</param>
        /// <param name="mode">Load mode (Single or Additive).</param>
        public static void LoadScene(
            string sceneName,
            Action<float> onProgress = null,
            Action onComplete = null,
            bool autoActivate = true,
            LoadSceneMode mode = LoadSceneMode.Single)
        {
            CoroutineRunner.Run(LoadSceneRoutine(sceneName, onProgress, onComplete, autoActivate, mode));
        }

        /// <summary>Activate a scene that was loaded with autoActivate=false.</summary>
        public static void ActivateScene()
        {
            if (_currentOp != null)
                _currentOp.allowSceneActivation = true;
        }

        static IEnumerator LoadSceneRoutine(
            string sceneName,
            Action<float> onProgress,
            Action onComplete,
            bool autoActivate,
            LoadSceneMode mode)
        {
            _currentOp = SceneManager.LoadSceneAsync(sceneName, mode);
            _currentOp.allowSceneActivation = autoActivate;

            while (!_currentOp.isDone)
            {
                // progress goes 0..0.9 when allowSceneActivation=false, normalize to 0..1
                float progress = autoActivate
                    ? _currentOp.progress
                    : Mathf.Clamp01(_currentOp.progress / 0.9f);

                onProgress?.Invoke(progress);

                if (!autoActivate && _currentOp.progress >= 0.9f)
                {
                    onProgress?.Invoke(1f);
                    yield break; // wait for ActivateScene() call
                }

                yield return null;
            }

            onProgress?.Invoke(1f);
            onComplete?.Invoke();
            _currentOp = null;
        }
    }
}
