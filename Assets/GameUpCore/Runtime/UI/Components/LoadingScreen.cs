using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameUp.Core;

namespace GameUp.UI
{
    /// <summary>
    /// Loading screen with progress bar and percentage text.
    /// Singleton access for easy show/hide from anywhere.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        [SerializeField] CanvasGroup _canvasGroup;
        [SerializeField] Slider _progressBar;
        [SerializeField] TextMeshProUGUI _progressText;
        [SerializeField] float _fadeDuration = 0.3f;

        static LoadingScreen _instance;

        public static LoadingScreen Instance => _instance;

        void Awake()
        {
            _instance = this;
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Show the loading screen.</summary>
        public void Show()
        {
            SetProgress(0f);
            gameObject.SetActive(true);
            if (_canvasGroup != null)
                CoroutineRunner.Run(FadeIn());
        }

        /// <summary>Hide the loading screen with fade.</summary>
        public void Hide()
        {
            if (_canvasGroup != null)
                CoroutineRunner.Run(FadeOutAndDisable());
            else
                gameObject.SetActive(false);
        }

        /// <summary>Update the progress (0-1).</summary>
        public void SetProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (_progressBar != null) _progressBar.value = progress;
            if (_progressText != null) _progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
        }

        IEnumerator FadeIn()
        {
            float t = 0f;
            _canvasGroup.alpha = 0f;
            while (t < _fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = t / _fadeDuration;
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        IEnumerator FadeOutAndDisable()
        {
            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - (t / _fadeDuration);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }
    }
}
