using System;
using UnityEngine;

namespace GameUp.UI
{
    /// <summary>
    /// Base class for full-screen UI panels. Managed by <see cref="UIManager"/>.
    /// Override OnShow/OnHide for custom logic.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        CanvasGroup _canvasGroup;

        public CanvasGroup CanvasGroup
        {
            get
            {
                if (_canvasGroup == null)
                    _canvasGroup = GetComponent<CanvasGroup>();
                return _canvasGroup;
            }
        }

        public bool IsVisible { get; private set; }

        /// <summary>Show the screen. Called by ScreenNavigator.</summary>
        public void Show(Action onComplete = null)
        {
            gameObject.SetActive(true);
            IsVisible = true;
            OnShow();
            onComplete?.Invoke();
        }

        /// <summary>Hide the screen. Called by ScreenNavigator.</summary>
        public void Hide(Action onComplete = null)
        {
            IsVisible = false;
            OnHide();
            gameObject.SetActive(false);
            onComplete?.Invoke();
        }

        /// <summary>Called when the screen becomes visible.</summary>
        protected virtual void OnShow() { }

        /// <summary>Called when the screen is hidden.</summary>
        protected virtual void OnHide() { }
    }
}
