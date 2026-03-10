using System;
using System.Collections;
using UnityEngine;

namespace GameUp.UI
{
    /// <summary>
    /// Fade in/out transition using CanvasGroup alpha.
    /// </summary>
    [CreateAssetMenu(fileName = "FadeTransition", menuName = "GameUp/UI/Fade Transition")]
    public sealed class FadeTransition : ScriptableObject, ITransition
    {
        [SerializeField] float _duration = 0.3f;
        [SerializeField] AnimationCurve _curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public IEnumerator PlayShow(RectTransform target, CanvasGroup cg, Action onComplete = null)
        {
            float elapsed = 0f;
            cg.alpha = 0f;
            while (elapsed < _duration)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = _curve.Evaluate(elapsed / _duration);
                yield return null;
            }
            cg.alpha = 1f;
            onComplete?.Invoke();
        }

        public IEnumerator PlayHide(RectTransform target, CanvasGroup cg, Action onComplete = null)
        {
            float elapsed = 0f;
            cg.alpha = 1f;
            while (elapsed < _duration)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = 1f - _curve.Evaluate(elapsed / _duration);
                yield return null;
            }
            cg.alpha = 0f;
            onComplete?.Invoke();
        }
    }
}
