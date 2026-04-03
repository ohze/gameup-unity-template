using System;
#if DOTween__DEPENDENCIES_INSTALLED
using DG.Tweening;
#endif
using UnityEngine;
using UnityEngine.Serialization;

namespace GameUp.Core.UI
{
    /// <summary>
    /// Khung chung cho mọi biến thể loading: callback, tự đóng, fade + thu nhỏ khi đóng, trả pool.
    /// Phần mở (intro) do lớp con định nghĩa qua <see cref="PlayIntro"/> / <see cref="StopIntroTweens"/>.
    /// </summary>
    public abstract class LoadingOverlayBase : MonoBehaviour
    {
        private const float CloseDuration = 0.2f;

        [FormerlySerializedAs("group")]
        [SerializeField] private CanvasGroup canvasGroup;

#if DOTween__DEPENDENCIES_INSTALLED
        private Tween _closeFadeTween;
        private Tween _closeScaleTween;
        protected Tween _autoCloseTween;
#else
        private Coroutine _autoCloseRoutine;
#endif

        private RectTransform _rootRect;
        private Vector2 _defaultRootSizeDelta;
        private Vector2 _defaultRootAnchoredPosition;
        private bool _rootDefaultsCached;

        protected Action OnOpened;
        protected Action OnClosed;

        protected CanvasGroup OverlayGroup => canvasGroup;

        private void EnsureRootLayoutDefaultsCached()
        {
            if (_rootDefaultsCached)
                return;

            _rootDefaultsCached = true;
            _rootRect = transform as RectTransform;
            if (_rootRect != null)
            {
                _defaultRootSizeDelta = _rootRect.sizeDelta;
                _defaultRootAnchoredPosition = _rootRect.anchoredPosition;
            }
        }

        public void Open(bool autoClose, float autoCloseTime, Action onOpened, Action onClosed)
        {
            OnOpened = onOpened;
            OnClosed = onClosed;

            StopIntroTweens();
            KillCloseTweens();

            gameObject.Show();
            ResetTransformForSpawn();
            PlayIntro();

            if (autoClose)
            {
#if DOTween__DEPENDENCIES_INSTALLED
                _autoCloseTween = DOVirtual.DelayedCall(autoCloseTime, Close)
                    .SetUpdate(true);
#else
                _autoCloseRoutine = StartCoroutine(CloseAfterDelay(autoCloseTime));
#endif
            }
        }

        public virtual void Close()
        {
            StopIntroTweens();
#if DOTween__DEPENDENCIES_INSTALLED
            _autoCloseTween?.Kill();
            _autoCloseTween = null;

            KillCloseTweens();

            if (canvasGroup != null)
            {
                _closeFadeTween = canvasGroup.DOFade(0f, CloseDuration)
                    .SetUpdate(true);
            }

            _closeScaleTween = transform.DOScale(0.95f, CloseDuration)
                .SetUpdate(true)
                .OnComplete(FinishClose);
#else
            if (_autoCloseRoutine != null)
            {
                StopCoroutine(_autoCloseRoutine);
                _autoCloseRoutine = null;
            }

            FinishClose();
#endif
        }

        /// <summary>
        /// Đưa root về trạng thái spawn: <c>localPosition</c> zero, scale 1, <c>sizeDelta</c>/<c>anchoredPosition</c> như prefab
        /// (tránh tích lũy sau pool / tween đóng).
        /// </summary>
        private void ResetTransformForSpawn()
        {
            ApplyRootSpawnDefaults();
        }

        /// <summary>Gọi khi trả pool: dọn transform để lần spawn sau không cộng dồn.</summary>
        protected virtual void ResetVisualStateForPool()
        {
            ApplyRootSpawnDefaults();
        }

        private void ApplyRootSpawnDefaults()
        {
            EnsureRootLayoutDefaultsCached();

            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            transform.localPosition = Vector3.zero;

            if (_rootRect != null)
            {
                _rootRect.sizeDelta = _defaultRootSizeDelta;
                _rootRect.anchoredPosition = _defaultRootAnchoredPosition;
            }
        }

        /// <summary>Animation phần mở (spinner, mask, …).</summary>
        protected abstract void PlayIntro();

        /// <summary>Dừng mọi tween chỉ dùng cho intro — không gồm tween đóng của base.</summary>
        protected abstract void StopIntroTweens();

        protected void FinishClose()
        {
            OnClosed?.Invoke();
            GUPool.DeSpawn(this);
            DisposeAllTweens();
        }

        protected void KillCloseTweens()
        {
#if DOTween__DEPENDENCIES_INSTALLED
            _closeFadeTween?.Kill();
            _closeScaleTween?.Kill();
            _closeFadeTween = null;
            _closeScaleTween = null;
#endif
        }

        private void DisposeAllTweens()
        {
            StopIntroTweens();
#if DOTween__DEPENDENCIES_INSTALLED
            _autoCloseTween?.Kill();
            _autoCloseTween = null;
#else
            if (_autoCloseRoutine != null)
            {
                StopCoroutine(_autoCloseRoutine);
                _autoCloseRoutine = null;
            }
#endif
            KillCloseTweens();
        }

#if !DOTween__DEPENDENCIES_INSTALLED
        private System.Collections.IEnumerator CloseAfterDelay(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            Close();
        }
#endif

        protected virtual void OnDisable()
        {
            DisposeAllTweens();
            ResetVisualStateForPool();
        }
    }
}
