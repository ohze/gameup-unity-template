using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameUp.UI
{
    /// <summary>
    /// Button wrapper that prevents double-click by enforcing a cooldown period.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class SafeButton : MonoBehaviour
    {
        [SerializeField] float _cooldown = 0.4f;

        Button _button;
        float _lastClickTime = float.MinValue;

        public event Action OnClick;

        public float Cooldown
        {
            get => _cooldown;
            set => _cooldown = Mathf.Max(0f, value);
        }

        void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(HandleClick);
        }

        void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(HandleClick);
        }

        void HandleClick()
        {
            if (Time.unscaledTime - _lastClickTime < _cooldown) return;
            _lastClickTime = Time.unscaledTime;
            OnClick?.Invoke();
        }

        /// <summary>Add a click listener with built-in debounce.</summary>
        public void AddListener(Action callback) => OnClick += callback;

        /// <summary>Remove a click listener.</summary>
        public void RemoveListener(Action callback) => OnClick -= callback;
    }
}
