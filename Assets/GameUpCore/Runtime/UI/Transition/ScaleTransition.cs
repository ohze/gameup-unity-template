using System;
using System.Collections;
using UnityEngine;

namespace GameUp.UI
{
    /// <summary>
    /// Scale bounce transition. Pops from 0 to 1 with overshoot.
    /// </summary>
    [CreateAssetMenu(fileName = "ScaleTransition", menuName = "GameUp/UI/Scale Transition")]
    public sealed class ScaleTransition : ScriptableObject, ITransition
    {
        [SerializeField] float _duration = 0.3f;
        [SerializeField] AnimationCurve _showCurve = new(
            new Keyframe(0f, 0f),
            new Keyframe(0.6f, 1.1f),
            new Keyframe(0.8f, 0.95f),
            new Keyframe(1f, 1f)
        );
        [SerializeField] AnimationCurve _hideCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        public IEnumerator PlayShow(RectTransform target, CanvasGroup cg, Action onComplete = null)
        {
            target.localScale = Vector3.zero;
            cg.alpha = 1f;

            float elapsed = 0f;
            while (elapsed < _duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = _showCurve.Evaluate(elapsed / _duration);
                target.localScale = Vector3.one * t;
                yield return null;
            }
            target.localScale = Vector3.one;
            onComplete?.Invoke();
        }

        public IEnumerator PlayHide(RectTransform target, CanvasGroup cg, Action onComplete = null)
        {
            float elapsed = 0f;
            while (elapsed < _duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = _hideCurve.Evaluate(elapsed / _duration);
                target.localScale = Vector3.one * t;
                yield return null;
            }
            target.localScale = Vector3.zero;
            cg.alpha = 0f;
            onComplete?.Invoke();
        }
    }
}
