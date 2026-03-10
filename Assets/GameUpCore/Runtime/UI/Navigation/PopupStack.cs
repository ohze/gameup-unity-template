using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameUp.UI
{
    /// <summary>
    /// LIFO popup manager. Manages display order and overlay creation.
    /// </summary>
    public sealed class PopupStack
    {
        readonly Stack<UIPopup> _stack = new();
        readonly Transform _popupLayer;

        public int Count => _stack.Count;
        public bool HasActivePopup => _stack.Count > 0;

        public event Action<UIPopup> OnPopupShown;
        public event Action<UIPopup> OnPopupDismissed;

        public PopupStack(Transform popupLayer)
        {
            _popupLayer = popupLayer;
        }

        /// <summary>Show a popup on top of the stack.</summary>
        public void Show(UIPopup popup)
        {
            if (popup == null) return;

            _stack.Push(popup);
            popup.OnDismissed += () => OnPopupDismissedInternal(popup);
            popup.Show(_popupLayer);
            OnPopupShown?.Invoke(popup);
        }

        /// <summary>Dismiss the topmost popup.</summary>
        public bool DismissTop()
        {
            if (_stack.Count == 0) return false;
            var popup = _stack.Peek();
            popup.Dismiss();
            return true;
        }

        /// <summary>Dismiss all active popups.</summary>
        public void DismissAll()
        {
            while (_stack.Count > 0)
            {
                var popup = _stack.Pop();
                popup.Dismiss();
            }
        }

        void OnPopupDismissedInternal(UIPopup popup)
        {
            if (_stack.Count > 0 && _stack.Peek() == popup)
                _stack.Pop();

            OnPopupDismissed?.Invoke(popup);
        }
    }
}
