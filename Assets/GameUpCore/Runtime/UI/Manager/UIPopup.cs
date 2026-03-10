using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameUp.UI
{
    /// <summary>
    /// Base class for popup panels. Supports overlay background and close-on-overlay-click.
    /// Managed by <see cref="PopupStack"/> within <see cref="UIManager"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIPopup : MonoBehaviour
    {
        [SerializeField] bool _closeOnOverlayClick = true;

        CanvasGroup _canvasGroup;
        GameObject _overlay;

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

        public event Action OnDismissed;

        /// <summary>Show the popup with overlay.</summary>
        public void Show(Transform overlayParent, Action onComplete = null)
        {
            CreateOverlay(overlayParent);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            IsVisible = true;
            OnShow();
            onComplete?.Invoke();
        }

        /// <summary>Dismiss the popup.</summary>
        public void Dismiss(Action onComplete = null)
        {
            IsVisible = false;
            OnHide();
            DestroyOverlay();
            gameObject.SetActive(false);
            OnDismissed?.Invoke();
            onComplete?.Invoke();
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }

        /// <summary>Call this from a close button to dismiss via PopupStack.</summary>
        public void RequestDismiss()
        {
            Dismiss();
        }

        void CreateOverlay(Transform parent)
        {
            _overlay = new GameObject("PopupOverlay");
            _overlay.transform.SetParent(parent, false);
            _overlay.transform.SetSiblingIndex(transform.GetSiblingIndex());

            var rt = _overlay.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            var image = _overlay.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.5f);
            image.raycastTarget = true;

            if (_closeOnOverlayClick)
            {
                var button = _overlay.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(RequestDismiss);
            }
        }

        void DestroyOverlay()
        {
            if (_overlay != null)
            {
                Destroy(_overlay);
                _overlay = null;
            }
        }
    }
}
