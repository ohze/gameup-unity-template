using System;
using System.Collections;
using UnityEngine;

namespace GameUp.UI
{
    /// <summary>
    /// Interface for UI transition animations.
    /// </summary>
    public interface ITransition
    {
        /// <summary>Play show animation on the target RectTransform.</summary>
        IEnumerator PlayShow(RectTransform target, CanvasGroup canvasGroup, Action onComplete = null);

        /// <summary>Play hide animation on the target RectTransform.</summary>
        IEnumerator PlayHide(RectTransform target, CanvasGroup canvasGroup, Action onComplete = null);
    }
}
