using System;
using UnityEngine;

namespace GameUp.SDK
{
    public enum NativeOverlayTemplate { Small, Medium }
    public enum NativeOverlayPosition { Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight, Center }

    [Serializable]
    public sealed class NativeOverlayOptions
    {
        public NativeOverlayTemplate template = NativeOverlayTemplate.Medium;
        public NativeOverlayPosition position = NativeOverlayPosition.Bottom;
        public Color backgroundColor = Color.white;
    }

    // Optional capability: existing networks and native formats remain independent.
    public interface INativeOverlayNetwork
    {
        INativeOverlayAd NativeOverlayAd { get; }
    }

    public interface INativeOverlayAd : IAdFormat, IDisposable
    {
        event Action<string> OnAdClicked;
        event Action<string> OnAdImpressionRecorded;
        event Action<string> OnFullScreenOpened;
        event Action<string> OnFullScreenClosed;
        // Success means RenderTemplate/Show returned, not an impression or a reward.
        void Show(string where, NativeOverlayOptions options, Action onSuccess, Action onFail);
        void Hide();
        void Destroy(string where);
    }
}
