using System;
using System.Collections;
using UnityEngine;

namespace GameUp.UI
{
    public enum SlideDirection { Left, Right, Top, Bottom }

    /// <summary>
    /// Slide in/out transition. Slides the RectTransform from/to off-screen.
    /// </summary>
    [CreateAssetMenu(fileName = "SlideTransition", menuName = "GameUp/UI/Slide Transition")]
    public sealed class SlideTransition : ScriptableObject, ITransition
    {
        [SerializeField] float _duration = 0.3f;
        [SerializeField] SlideDirection _direction = SlideDirection.Left;
        [SerializeField] AnimationCurve _curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public IEnumerator PlayShow(RectTransform target, CanvasGroup cg, Action onComplete = null)
        {
            var endPos = target.anchoredPosition;
            var startPos = GetOffScreenPosition(target, _direction);
            target.anchoredPosition = startPos;
            cg.alpha = 1f;

            float elapsed = 0f;
            while (elapsed < _duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = _curve.Evaluate(elapsed / _duration);
                target.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, t);
                yield return null;
            }
            target.anchoredPosition = endPos;
            onComplete?.Invoke();
        }

        public IEnumerator PlayHide(RectTransform target, CanvasGroup cg, Action onComplete = null)
        {
            var startPos = target.anchoredPosition;
            var endPos = GetOffScreenPosition(target, _direction);

            float elapsed = 0f;
            while (elapsed < _duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = _curve.Evaluate(elapsed / _duration);
                target.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, t);
                yield return null;
            }
            target.anchoredPosition = endPos;
            cg.alpha = 0f;
            onComplete?.Invoke();
        }

        static Vector2 GetOffScreenPosition(RectTransform target, SlideDirection dir)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            float width = canvas != null ? ((RectTransform)canvas.transform).rect.width : Screen.width;
            float height = canvas != null ? ((RectTransform)canvas.transform).rect.height : Screen.height;
            var pos = target.anchoredPosition;

            return dir switch
            {
                SlideDirection.Left => new Vector2(-width, pos.y),
                SlideDirection.Right => new Vector2(width, pos.y),
                SlideDirection.Top => new Vector2(pos.x, height),
                SlideDirection.Bottom => new Vector2(pos.x, -height),
                _ => pos
            };
        }
    }
}
