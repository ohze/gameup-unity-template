using System;
using System.Collections.Generic;
using UnityEngine;
#if ADMOB_DEPENDENCIES_INSTALLED
using GoogleMobileAds.Api;
#endif

namespace GameUp.SDK
{
    /// <summary>AdMob C# NativeOverlayAd. No GameUp Java/Objective-C bridge is used.
    /// One visible overlay per adapter; cache and waterfall are keyed by resolved unit ID.</summary>
    public sealed class AdmobNativeOverlayAd : INativeOverlayAd
    {
        public event Action<string> OnAdLoaded;
        public event Action<string, string> OnAdLoadFailed;
        public event Action<string> OnAdDisplayed;
        public event Action<string, string> OnAdDisplayFailed;
        public event Action<string> OnAdClosed;
        public event Action<string> OnAdClicked;
        public event Action<string> OnAdImpressionRecorded;
        public event Action<string> OnFullScreenOpened;
        public event Action<string> OnFullScreenClosed;

        private readonly AdUnitConfig _config;
        private readonly NativeOverlayOptions _defaults;
        private bool _disposed;
        private readonly Dictionary<string, object> _loads = new Dictionary<string, object>();
#if ADMOB_DEPENDENCIES_INSTALLED
        private sealed class Entry
        {
            public NativeOverlayAd Ad;
            public float LoadedAt;
            public string Where;
            public bool FullScreen;
        }
        private readonly Dictionary<string, Entry> _ads = new Dictionary<string, Entry>();
        private Entry _visible;
#endif
        private static string Placement(string where) => string.IsNullOrWhiteSpace(where) ? "default" : where;

        public AdmobNativeOverlayAd(AdUnitConfig config, NativeOverlayOptions defaults)
        {
            _config = config ?? new AdUnitConfig();
            _defaults = defaults ?? new NativeOverlayOptions();
        }

        public void LoadAll()
        {
            foreach (var where in _config.GetAllPlacements()) Load(where);
        }

        public void Load(string where = null)
        {
            if (_disposed || IsAvailable(where)) return;
            where = Placement(where);
#if ADMOB_DEPENDENCIES_INSTALLED
            // Shared default IDs must not trigger parallel requests for different placements.
            foreach (var floor in _config.GetActiveFloors())
            {
                var id = _config.ResolveUnitId(AdUnitType.NativeOverlay, where, floor);
                if (!string.IsNullOrEmpty(id) && _loads.ContainsKey(id)) return;
            }
            Request(where, 0);
#else
            OnAdLoadFailed?.Invoke(where, "admob_dependency_missing");
#endif
        }

        public bool IsAvailable(string where = null)
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            return !_disposed && Find(Placement(where)) != null;
#else
            return false;
#endif
        }

#if ADMOB_DEPENDENCIES_INSTALLED
        private Entry Find(string where)
        {
            foreach (var floor in _config.GetActiveFloors())
            {
                var id = _config.ResolveUnitId(AdUnitType.NativeOverlay, where, floor);
                if (string.IsNullOrEmpty(id) || !_ads.TryGetValue(id, out var entry)) continue;
                // An already visible ad can stay on screen; never show an expired cached ad again.
                if (entry == _visible || Time.realtimeSinceStartup - entry.LoadedAt < 3600f) return entry;
                _ads.Remove(id);
                Release(entry);
            }
            return null;
        }

        private void Request(string where, int floorIndex)
        {
            var floors = _config.GetActiveFloors();
            while (floorIndex < floors.Length)
            {
                var id = _config.ResolveUnitId(AdUnitType.NativeOverlay, where, floors[floorIndex]);
                if (string.IsNullOrWhiteSpace(id)) { floorIndex++; continue; }
                if (_loads.ContainsKey(id)) return;
                var token = new object();
                _loads[id] = token;
                int next = floorIndex + 1;
                NativeOverlayAd.Load(id, new AdRequest(), new NativeAdOptions(), (ad, error) =>
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        // Destroy or Dispose invalidates pending callbacks, including failures.
                        if (_disposed || !_loads.TryGetValue(id, out var current) || current != token)
                        {
                            ad?.Destroy();
                            return;
                        }
                        if (error != null || ad == null)
                        {
                            ad?.Destroy();
                            OnAdLoadFailed?.Invoke(where, error?.GetMessage() ?? "null_ad");
                            if (_disposed || !_loads.TryGetValue(id, out current) || current != token) return;
                            _loads.Remove(id);
                            if (next < floors.Length) Request(where, next);
                            return;
                        }
                        _loads.Remove(id);
                        var entry = new Entry { Ad = ad, LoadedAt = Time.realtimeSinceStartup, Where = where };
                        _ads[id] = entry;
                        ad.OnAdClicked += () => MainThreadDispatcher.Enqueue(() =>
                        {
                            if (IsLive(id, entry)) OnAdClicked?.Invoke(entry.Where);
                        });
                        ad.OnAdImpressionRecorded += () => MainThreadDispatcher.Enqueue(() =>
                        {
                            if (IsLive(id, entry)) OnAdImpressionRecorded?.Invoke(entry.Where);
                        });
                        ad.OnAdPaid += value => MainThreadDispatcher.Enqueue(() =>
                        {
                            if (!IsLive(id, entry) || value == null) return;
                            AdsEvent.RaiseImpressionDataReady(new AdImpressionData
                            {
                                AdNetwork = "Admob", AdUnit = id, InstanceName = entry.Where,
                                AdFormat = "native_overlay", Revenue = value.Value / 1000000d
                            });
                        });
                        ad.OnAdFullScreenContentOpened += () => MainThreadDispatcher.Enqueue(() =>
                        {
                            if (!IsLive(id, entry) || entry.FullScreen) return;
                            entry.FullScreen = true;
                            OnFullScreenOpened?.Invoke(entry.Where);
                        });
                        ad.OnAdFullScreenContentClosed += () => MainThreadDispatcher.Enqueue(() =>
                        {
                            if (!IsLive(id, entry) || !entry.FullScreen) return;
                            entry.FullScreen = false;
                            OnFullScreenClosed?.Invoke(entry.Where);
                        });
                        OnAdLoaded?.Invoke(where);
                    }));
                return;
            }
            OnAdLoadFailed?.Invoke(where, "no_unit_id");
        }

        private bool IsLive(string id, Entry entry) =>
            !_disposed && _ads.TryGetValue(id, out var current) && current == entry;

        private void Release(Entry entry)
        {
            if (_visible == entry) Hide();
            if (entry.FullScreen)
            {
                entry.FullScreen = false;
                OnFullScreenClosed?.Invoke(entry.Where);
            }
            entry.Ad.Destroy();
        }
#endif

        public void Show(string where, NativeOverlayOptions options, Action onSuccess, Action onFail)
        {
            where = Placement(where);
#if ADMOB_DEPENDENCIES_INSTALLED
            var entry = _disposed ? null : Find(where);
            if (entry != null)
            {
                var style = options ?? _defaults;
                try
                {
                    Hide();
                    entry.Where = where;
                    entry.Ad.RenderTemplate(new NativeTemplateStyle
                    {
                        TemplateId = style.template == NativeOverlayTemplate.Small ? NativeTemplateId.Small : NativeTemplateId.Medium,
                        MainBackgroundColor = style.backgroundColor
                    }, (AdPosition)Enum.Parse(typeof(AdPosition), style.position.ToString()));
                    _visible = entry;
                }
                catch (Exception error)
                {
                    entry.Ad.Hide();
                    OnAdDisplayFailed?.Invoke(where, error.Message);
                    onFail?.Invoke();
                    return;
                }
                OnAdDisplayed?.Invoke(where);
                onSuccess?.Invoke();
                return;
            }
#endif
            OnAdDisplayFailed?.Invoke(where, "not_ready");
            onFail?.Invoke();
        }

        public void Hide()
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            if (_visible == null) return;
            var entry = _visible;
            _visible = null;
            entry.Ad.Hide();
            OnAdClosed?.Invoke(entry.Where);
#endif
        }

        public void Destroy(string where)
        {
            where = Placement(where);
            foreach (var floor in _config.GetActiveFloors())
            {
                var id = _config.ResolveUnitId(AdUnitType.NativeOverlay, where, floor);
                if (string.IsNullOrEmpty(id)) continue;
                _loads.Remove(id);
#if ADMOB_DEPENDENCIES_INSTALLED
                if (!_ads.TryGetValue(id, out var entry)) continue;
                _ads.Remove(id);
                Release(entry);
#endif
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _loads.Clear();
#if ADMOB_DEPENDENCIES_INSTALLED
            foreach (var entry in _ads.Values) Release(entry);
            _ads.Clear();
#endif
        }
    }
}
