# Native Overlay — AdMob Unity C# API

`AdUnitType.NativeOverlay` is a separate format from `NativeAd` (native fullscreen)
and `BannerFormatType.NativeOverlay` (the existing native banner bridge).
It uses `GoogleMobileAds.Api.NativeOverlayAd` directly. GameUp's Java and
Objective-C bridges are not involved. AdMob still renders a platform-native view;
this is not a Unity Canvas/GameObject ad.

## Setup

1. Install/enable the AdMob dependency and include AdMob in mediation priority.
   The implementation is compiled against Google Mobile Ads Unity 10.7.0.
2. Open **GameUp > SDK > Setup > AdMob**.
3. Fill **Native Overlay (AdMob Unity API)** IDs for Android/iOS. This configuration
   is independent of the old native and banner IDs. Placement IDs and optional
   High → Medium → All waterfall work as with the other unit configurations.
4. Set `nativeOverlayOptions`: Small/Medium template, screen anchor, background.
5. Save Configuration. Existing projects default to empty overlay IDs, so no new
   ad request is made until this format is configured.

Use Google's native test units during development:

- Android: `ca-app-pub-3940256099942544/2247696110`
- iOS: `ca-app-pub-3940256099942544/3986624511`

The existing AdMob initialization/consent flow is reused; there is no second
`MobileAds.Initialize` call. Overlay placements preload after network initialization,
without displaying. Load requests before AdMob initialization are not queued.

## Game API

```csharp
using GameUp.SDK;

// Call after AdMob has initialized, or rely on the initial preload.
AdsManager.Instance.LoadNativeOverlay("result_screen");

// Call while the intended screen is active. If unavailable, load and try again
// on a later UI action or subscribe to the adapter's OnAdLoaded event.
if (AdsManager.Instance.IsNativeOverlayAvailable("result_screen"))
{
    AdsManager.Instance.ShowNativeOverlay(
        where: "result_screen",
        options: new NativeOverlayOptions
        {
            template = NativeOverlayTemplate.Medium,
            position = NativeOverlayPosition.Bottom
        },
        onSuccess: () => UnityEngine.Debug.Log("Overlay render requested"),
        onFail: () => UnityEngine.Debug.Log("Overlay unavailable or blocked"));
}

// Leave the screen: hide and retain the cached ad.
AdsManager.Instance.HideNativeOverlay();

// Release the cached ad and invalidate any in-flight load for its unit IDs.
AdsManager.Instance.DestroyNativeOverlay("result_screen");
```

Omit `options` to use saved configuration. Only one overlay is visible at a time.
`ShowNativeOverlay` checks Remove All Ads, fullscreen activity and conditions for
`AdUnitType.NativeOverlay`. Failure invokes `onFail`; no delayed show is queued.
When no ad is ready, Show requests a load. After a waterfall is exhausted, a later
explicit Load/Show retries; this format does not run an endless background retry.

An overlay is temporarily hidden during other fullscreen ads and restored when
they close, provided rules still allow it. Calling Hide/Destroy while suspended
cancels restoration. An overlay itself does not pause capping; only fullscreen
content opened by its click does. Native overlay uses no GameUp CTA click-rate override.

Rules with changing runtime values can call
`AdsManager.Instance.RefreshNativeOverlayVisibility()` to hide a now-blocked view.
Newly added rules are checked immediately. IsAvailable only checks cache/removal,
not all show conditions. Shared unit IDs share cache; Destroy invalidates that
unit for every placement using it. Hidden cached ads expire after one hour.

## Events and tracking

Get the optional capability from an initialized network:

```csharp
if (AdsManager.Instance.Networks.TryGetValue(MediationProvider.Admob, out var network)
    && network is INativeOverlayNetwork provider
    && provider.NativeOverlayAd != null)
{
    INativeOverlayAd overlay = provider.NativeOverlayAd;
    // Subscribe/unsubscribe using your own stable handlers:
    // overlay.OnAdLoaded += OnLoaded;
    // overlay.OnAdClicked += OnClicked;
    // overlay.OnAdImpressionRecorded += OnImpression;
}
```

`onSuccess`/`OnAdDisplayed` means the render call returned, not a confirmed
impression or reward. `OnAdImpressionRecorded` comes from AdMob.
`OnAdClosed` means the overlay was hidden; `OnFullScreenClosed` concerns the
fullscreen content opened by an ad click. Call the manager APIs for normal game
usage so show rules and lifecycle state are respected.

Manager tracking uses `native_overlay`. Paid callbacks pass revenue in currency
units to the existing `AdsEvent.OnImpressionDataReady` pipeline. Callbacks are
marshalled through `MainThreadDispatcher`; destroyed/disposed entries ignore late
callbacks, and late successful loads are destroyed. SDK teardown releases caches.

## Device verification

Verify on both Android and iOS with test ads: Small/Medium rendering and anchors,
hide/show, screen transitions while loading, fullscreen open/close, Remove Ads,
impression/click/paid callbacks and teardown. C# compilation and simulated lifecycle
tests do not verify native rendering or live ad delivery.

Official reference: https://developers.google.com/admob/unity/native-overlay
