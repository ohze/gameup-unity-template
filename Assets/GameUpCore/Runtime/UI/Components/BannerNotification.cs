using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using GameUp.Core;

namespace GameUp.UI
{
    /// <summary>
    /// Banner notification that slides in from the top of the screen and auto-hides.
    /// Queue-based for multiple notifications.
    /// </summary>
    public sealed class BannerNotification : MonoBehaviour
    {
        [SerializeField] RectTransform _bannerRect;
        [SerializeField] CanvasGroup _canvasGroup;
        [SerializeField] TextMeshProUGUI _messageText;
        [SerializeField] float _slideDuration = 0.3f;
        [SerializeField] float _displayDuration = 3f;
        [SerializeField] float _hiddenYOffset = 200f;

        static BannerNotification _instance;

        readonly Queue<string> _queue = new();
        bool _isShowing;
        Vector2 _shownPosition;
        Vector2 _hiddenPosition;

        public static BannerNotification Instance => _instance;

        void Awake()
        {
            _instance = this;
            if (_bannerRect == null) _bannerRect = GetComponent<RectTransform>();
            _shownPosition = _bannerRect.anchoredPosition;
            _hiddenPosition = _shownPosition + new Vector2(0f, _hiddenYOffset);
            _bannerRect.anchoredPosition = _hiddenPosition;
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Enqueue a banner notification message.</summary>
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

                // Slide in
                yield return SlideToPosition(_hiddenPosition, _shownPosition);

                // Hold
                yield return new WaitForSecondsRealtime(_displayDuration);

                // Slide out
                yield return SlideToPosition(_shownPosition, _hiddenPosition);

                gameObject.SetActive(false);
            }
            _isShowing = false;
        }

        IEnumerator SlideToPosition(Vector2 from, Vector2 to)
        {
            float t = 0f;
            while (t < _slideDuration)
            {
                t += Time.unscaledDeltaTime;
                _bannerRect.anchoredPosition = Vector2.Lerp(from, to, t / _slideDuration);
                yield return null;
            }
            _bannerRect.anchoredPosition = to;
        }
    }
}
