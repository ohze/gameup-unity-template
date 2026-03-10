using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using GameUp.Core;

namespace GameUp.UI
{
    /// <summary>
    /// Toast notification system. Displays brief messages that auto-hide.
    /// Queue-based: shows one toast at a time, dequeues next when current hides.
    /// </summary>
    public sealed class Toast : MonoBehaviour
    {
        [SerializeField] CanvasGroup _canvasGroup;
        [SerializeField] TextMeshProUGUI _messageText;
        [SerializeField] float _displayDuration = 2.5f;
        [SerializeField] float _fadeDuration = 0.3f;

        static Toast _instance;

        readonly Queue<string> _queue = new();
        bool _isShowing;

        public static Toast Instance => _instance;

        void Awake()
        {
            _instance = this;
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Enqueue a toast message. Shows immediately if none active.</summary>
        public static void Show(string message)
        {
            if (Instance == null) return;
            Instance._queue.Enqueue(message);
            if (!Instance._isShowing)
                CoroutineRunner.Run(Instance.ShowNext());
        }

        IEnumerator ShowNext()
        {
            while (_queue.Count > 0)
            {
                _isShowing = true;
                string msg = _queue.Dequeue();

                if (_messageText != null) _messageText.text = msg;
                gameObject.SetActive(true);

                // Fade in
                yield return Fade(0f, 1f);

                // Hold
                yield return new WaitForSecondsRealtime(_displayDuration);

                // Fade out
                yield return Fade(1f, 0f);

                gameObject.SetActive(false);
            }
            _isShowing = false;
        }

        IEnumerator Fade(float from, float to)
        {
            if (_canvasGroup == null) yield break;
            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, t / _fadeDuration);
                yield return null;
            }
            _canvasGroup.alpha = to;
        }
    }
}
