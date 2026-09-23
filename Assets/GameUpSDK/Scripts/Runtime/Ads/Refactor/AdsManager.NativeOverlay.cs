using System;
using System.Collections.Generic;

namespace GameUp.SDK
{
    public partial class AdsManager
    {
        private const string NativeOverlayTrackingType = "native_overlay";
        private readonly HashSet<INativeOverlayAd> _nativeOverlays = new HashSet<INativeOverlayAd>();
        private string _activeNativeOverlayPlacement;
        private NativeOverlayOptions _activeNativeOverlayOptions;
        private bool _disposingNativeOverlays;

        private void WireNativeOverlay(IAdNetwork network)
        {
            if (!(network is INativeOverlayNetwork provider) || provider.NativeOverlayAd == null) return;
            var ad = provider.NativeOverlayAd;
            if (!_nativeOverlays.Add(ad)) return;
            ad.OnAdDisplayed += OnNativeOverlayDisplayed;
            ad.OnAdDisplayFailed += OnNativeOverlayDisplayFailed;
            ad.OnFullScreenOpened += OnFullscreenDisplayed;
            ad.OnFullScreenClosed += OnNativeOverlayFullScreenClosed;
        }

        private void OnNativeOverlayDisplayed(string where) =>
            _tracker.LogAdsEventManager(AdsEvent.AdsShowSuccess, NativeOverlayTrackingType, where);

        private void OnNativeOverlayDisplayFailed(string where, string error) =>
            _tracker.LogAdsEventManager(AdsEvent.AdsShowFail, NativeOverlayTrackingType, where, error);

        private void OnNativeOverlayFullScreenClosed(string where)
        {
            AdCappingManager.Instance.ResumeAllCapping();
            if (_disposingNativeOverlays) return;
            RestoreBanners();
            RestoreNativeOverlay();
            AdHistoryTracker.MarkAdClosed(AdUnitType.NativeOverlay);
        }

        public bool IsNativeOverlayAvailable(string where = "default") =>
            !IsRemoveAllAdsActive() && GetAvailableProvider(AdUnitType.NativeOverlay, where) != null;

        public void LoadNativeOverlay(string where = "default")
        {
            if (IsRemoveAllAdsActive()) return;
            foreach (var ad in _nativeOverlays) ad.Load(where);
        }

        /// <summary>Success reports a render request, not an impression. Load first and retry
        /// explicitly when ready; show requests are never replayed after leaving a screen.</summary>
        public void ShowNativeOverlay(string where = "default", NativeOverlayOptions options = null,
            Action onSuccess = null, Action onFail = null)
        {
            where = string.IsNullOrWhiteSpace(where) ? "default" : where;
            string reason = null;
            if (IsRemoveAllAdsActive()) reason = "remove_ads";
            else if (AdCappingManager.Instance.IsAnyAdShowing) reason = "fullscreen_ad_showing";
            else if (!EvaluateConditions(AdUnitType.NativeOverlay, where, out reason))
                reason = reason ?? "blocked_by_condition";
            else reason = null;
            if (reason != null)
            {
                OnNativeOverlayDisplayFailed(where, reason);
                onFail?.Invoke();
                return;
            }

            _tracker.LogAdsEventManager(AdsEvent.AdsRequest, NativeOverlayTrackingType, where);
            var provider = GetAvailableProvider(AdUnitType.NativeOverlay, where) as INativeOverlayNetwork;
            if (provider?.NativeOverlayAd == null)
            {
                OnNativeOverlayDisplayFailed(where, "no_ads_available");
                LoadNativeOverlay(where);
                onFail?.Invoke();
                return;
            }
            _tracker.LogAdsEventManager(AdsEvent.AdsAvailable, NativeOverlayTrackingType, where);
            HideNativeOverlay();
            provider.NativeOverlayAd.Show(where, options, () =>
            {
                _activeNativeOverlayPlacement = where;
                _activeNativeOverlayOptions = options;
                onSuccess?.Invoke();
            }, onFail);
        }

        public void HideNativeOverlay()
        {
            _activeNativeOverlayPlacement = null;
            _activeNativeOverlayOptions = null;
            foreach (var ad in _nativeOverlays) ad.Hide();
        }

        /// <summary>Also cancels in-flight loads. Placements sharing a unit ID share its cache.</summary>
        public void DestroyNativeOverlay(string where = "default")
        {
            HideNativeOverlay();
            foreach (var ad in _nativeOverlays) ad.Destroy(where);
        }

        public void RefreshNativeOverlayVisibility()
        {
            if (_activeNativeOverlayPlacement != null && (IsRemoveAllAdsActive() ||
                !EvaluateConditions(AdUnitType.NativeOverlay, _activeNativeOverlayPlacement, out _)))
                HideNativeOverlay();
        }

        private void SuspendNativeOverlay()
        {
            foreach (var ad in _nativeOverlays) ad.Hide();
        }

        private void RestoreNativeOverlay()
        {
            if (_disposingNativeOverlays || _activeNativeOverlayPlacement == null ||
                AdCappingManager.Instance.IsAnyAdShowing) return;
            ShowNativeOverlay(_activeNativeOverlayPlacement, _activeNativeOverlayOptions);
        }

        private void DisposeNativeOverlays()
        {
            _disposingNativeOverlays = true;
            _activeNativeOverlayPlacement = null;
            foreach (var ad in _nativeOverlays)
            {
                ad.Dispose();
                ad.OnAdDisplayed -= OnNativeOverlayDisplayed;
                ad.OnAdDisplayFailed -= OnNativeOverlayDisplayFailed;
                ad.OnFullScreenOpened -= OnFullscreenDisplayed;
                ad.OnFullScreenClosed -= OnNativeOverlayFullScreenClosed;
            }
            _nativeOverlays.Clear();
        }
    }
}
