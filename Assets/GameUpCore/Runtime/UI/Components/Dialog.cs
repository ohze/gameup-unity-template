using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameUp.Core;

namespace GameUp.UI
{
    /// <summary>
    /// Reusable dialog with title, message, and confirm/cancel buttons.
    /// Supports single-button (alert) and two-button (confirm) modes.
    /// </summary>
    public sealed class Dialog : MonoBehaviour
    {
        [SerializeField] CanvasGroup _canvasGroup;
        [SerializeField] GameObject _overlay;
        [SerializeField] TextMeshProUGUI _titleText;
        [SerializeField] TextMeshProUGUI _messageText;
        [SerializeField] Button _confirmButton;
        [SerializeField] TextMeshProUGUI _confirmButtonText;
        [SerializeField] Button _cancelButton;
        [SerializeField] TextMeshProUGUI _cancelButtonText;

        static Dialog _instance;
        Action<bool> _callback;

        public static Dialog Instance => _instance;

        void Awake()
        {
            _instance = this;
            _confirmButton.onClick.AddListener(() => Close(true));
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(() => Close(false));
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Show a confirmation dialog with two buttons.</summary>
        public static void ShowConfirm(
            string title,
            string message,
            Action<bool> callback,
            string confirmText = "OK",
            string cancelText = "Cancel")
        {
            if (Instance == null) return;
            Instance.Setup(title, message, callback, confirmText, cancelText, showCancel: true);
        }

        /// <summary>Show an alert dialog with a single button.</summary>
        public static void ShowAlert(
            string title,
            string message,
            Action onConfirm = null,
            string confirmText = "OK")
        {
            if (Instance == null) return;
            Instance.Setup(title, message, result => onConfirm?.Invoke(), confirmText, "", showCancel: false);
        }

        void Setup(string title, string message, Action<bool> callback,
            string confirmText, string cancelText, bool showCancel)
        {
            if (_titleText != null) _titleText.text = title;
            if (_messageText != null) _messageText.text = message;
            if (_confirmButtonText != null) _confirmButtonText.text = confirmText;
            if (_cancelButtonText != null) _cancelButtonText.text = cancelText;
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(showCancel);
            if (_overlay != null) _overlay.SetActive(true);

            _callback = callback;
            gameObject.SetActive(true);
        }

        void Close(bool confirmed)
        {
            gameObject.SetActive(false);
            if (_overlay != null) _overlay.SetActive(false);
            _callback?.Invoke(confirmed);
            _callback = null;
        }
    }
}
