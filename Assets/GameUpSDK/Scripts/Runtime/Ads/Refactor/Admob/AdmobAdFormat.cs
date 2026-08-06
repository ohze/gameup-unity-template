#if ADMOB_DEPENDENCIES_INSTALLED
using GoogleMobileAds.Api;
using UnityEngine;
#endif
using System;
using System.Collections.Generic;

namespace GameUp.SDK
{
    // ==========================================
    // INTERSTITIAL AD
    // ==========================================
    public class AdmobInterstitialAd : BaseAdFormat, IInterstitialAd
    {
#if ADMOB_DEPENDENCIES_INSTALLED
        private readonly Dictionary<string, InterstitialAd> _ads = new Dictionary<string, InterstitialAd>();
#endif
        public AdmobInterstitialAd(AdUnitConfig config) : base(config, AdUnitType.Interstitial, "Admob") { }

        public override bool IsAvailable(string where = null)
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            foreach (EcpmFloor floor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, floor);
                if (!string.IsNullOrEmpty(unitId) && _ads.TryGetValue(unitId, out var ad) && ad != null && ad.CanShowAd())
                    return true;
            }
#endif
            return false;
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            if (_ads.TryGetValue(unitId, out var oldAd) && oldAd != null) oldAd.Destroy();

            InterstitialAd.Load(unitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    HandleLoadFailed(unitId, where, floor, error?.GetMessage());
                    return;
                }
                ad.OnAdPaid += (adValue) => { if (adValue != null) TrackRevenue(unitId, where, $"Interstitial_{floor}", adValue.Value * 0.000001f); };
                _ads[unitId] = ad;
                HandleLoadSuccess(unitId, where);
            });
#endif
        }

        public void Show(string where, Action onSuccess, Action onFail)
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            foreach (var currentFloor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, currentFloor);
                if (string.IsNullOrEmpty(unitId)) continue;

                if (_ads.TryGetValue(unitId, out var ad) && ad != null && ad.CanShowAd())
                {
                    NotifyAdDisplayed(where);
                    _ads.Remove(unitId);

                    // Ad đã bị gỡ khỏi _ads nên Destroy() ở RequestAdInternal không còn với tới nó
                    // → phải tự giải phóng, nếu không mỗi impression rò một native object.
                    bool released = false;
                    void Release()
                    {
                        if (released) return;
                        released = true;
                        ad.Destroy();
                    }

                    ad.OnAdFullScreenContentClosed += () => MainThreadDispatcher.Enqueue(() =>
                    {
                        NotifyAdClosed(where);
                        Release();
                        onSuccess?.Invoke();
                        LoadByFloor(where, currentFloor);
                    });
                    ad.OnAdFullScreenContentFailed += (err) => MainThreadDispatcher.Enqueue(() =>
                    {
                        NotifyAdDisplayFailed(where, err.GetMessage());
                        Release();
                        onFail?.Invoke();
                        LoadByFloor(where, currentFloor);
                    });
                    ad.Show();
                    return;
                }
            }
            NotifyAdDisplayFailed(where, "not_ready_all_floors");
            onFail?.Invoke();
            Load(where);
#endif
        }
    }

    // ==========================================
    // REWARDED AD
    // ==========================================
    public class AdmobRewardedAd : BaseAdFormat, IRewardedAd
    {
#if ADMOB_DEPENDENCIES_INSTALLED
        private readonly Dictionary<string, RewardedAd> _ads = new Dictionary<string, RewardedAd>();
#endif
        public AdmobRewardedAd(AdUnitConfig config) : base(config, AdUnitType.RewardedVideo, "Admob") { }

        public override bool IsAvailable(string where = null)
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            foreach (EcpmFloor floor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, floor);
                if (!string.IsNullOrEmpty(unitId) && _ads.TryGetValue(unitId, out var ad) && ad != null && ad.CanShowAd())
                    return true;
            }
#endif
            return false;
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            if (_ads.TryGetValue(unitId, out var oldAd) && oldAd != null) oldAd.Destroy();

            RewardedAd.Load(unitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    HandleLoadFailed(unitId, where, floor, error?.GetMessage());
                    return;
                }
                ad.OnAdPaid += (adValue) => { if (adValue != null) TrackRevenue(unitId, where, $"Rewarded_{floor}", adValue.Value * 0.000001f); };
                _ads[unitId] = ad;
                HandleLoadSuccess(unitId, where);
            });
#endif
        }

        public void Show(string where, Action onSuccess, Action onFail)
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            foreach (var currentFloor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, currentFloor);
                if (string.IsNullOrEmpty(unitId)) continue;

                if (_ads.TryGetValue(unitId, out var ad) && ad != null && ad.CanShowAd())
                {
                    NotifyAdDisplayed(where);
                    _ads.Remove(unitId);
                    bool earned = false;

                    // Xem ghi chú ở AdmobInterstitialAd.Show: ad đã rời _ads nên phải tự Destroy().
                    bool released = false;
                    void Release()
                    {
                        if (released) return;
                        released = true;
                        ad.Destroy();
                    }

                    ad.OnAdFullScreenContentClosed += () => MainThreadDispatcher.Enqueue(() =>
                    {
                        NotifyAdClosed(where);
                        Release();
                        if (!earned) onFail?.Invoke();
                        LoadByFloor(where, currentFloor);
                    });
                    ad.OnAdFullScreenContentFailed += (err) => MainThreadDispatcher.Enqueue(() =>
                    {
                        NotifyAdDisplayFailed(where, err.GetMessage());
                        Release();
                        onFail?.Invoke();
                        LoadByFloor(where, currentFloor);
                    });
                    ad.Show((reward) =>
                    {
                        earned = true;
                        MainThreadDispatcher.Enqueue(() => onSuccess?.Invoke());
                    });
                    return;
                }
            }
            NotifyAdDisplayFailed(where, "not_ready_all_floors");
            onFail?.Invoke();
            Load(where);
#endif
        }
    }

    // ==========================================
    // APP OPEN AD
    // ==========================================
    public class AdmobAppOpenAd : BaseAdFormat, IAppOpenAd
    {
#if ADMOB_DEPENDENCIES_INSTALLED
        private readonly Dictionary<string, AppOpenAd> _ads = new Dictionary<string, AppOpenAd>();
#endif
        private readonly Dictionary<string, DateTime> _expireTimes = new Dictionary<string, DateTime>();

        public AdmobAppOpenAd(AdUnitConfig config) : base(config, AdUnitType.AppOpen, "Admob") { }

        public override bool IsAvailable(string where = null)
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            foreach (EcpmFloor floor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, floor);
                if (!string.IsNullOrEmpty(unitId) && _ads.TryGetValue(unitId, out var ad) && ad != null && ad.CanShowAd() &&
                    _expireTimes.TryGetValue(unitId, out var exp) && DateTime.Now < exp)
                    return true;
            }
#endif
            return false;
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            if (_ads.TryGetValue(unitId, out var oldAd) && oldAd != null) oldAd.Destroy();

            AppOpenAd.Load(unitId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    HandleLoadFailed(unitId, where, floor, error?.GetMessage());
                    return;
                }
                ad.OnAdPaid += (adValue) => { if (adValue != null) TrackRevenue(unitId, where, $"AppOpen_{floor}", adValue.Value * 0.000001f); };
                _ads[unitId] = ad;
                _expireTimes[unitId] = DateTime.Now.AddHours(4);
                HandleLoadSuccess(unitId, where);
            });
#endif
        }

        public void Show(string where, Action onSuccess, Action onFail)
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            foreach (var currentFloor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, currentFloor);
                if (string.IsNullOrEmpty(unitId)) continue;

                if (_ads.TryGetValue(unitId, out var ad) && ad != null && ad.CanShowAd() &&
                    _expireTimes.TryGetValue(unitId, out var exp) && DateTime.Now < exp)
                {
                    NotifyAdDisplayed(where);
                    _ads.Remove(unitId);
                    _expireTimes.Remove(unitId);

                    // Xem ghi chú ở AdmobInterstitialAd.Show: ad đã rời _ads nên phải tự Destroy().
                    bool released = false;
                    void Release()
                    {
                        if (released) return;
                        released = true;
                        ad.Destroy();
                    }

                    ad.OnAdFullScreenContentClosed += () => MainThreadDispatcher.Enqueue(() =>
                    {
                        NotifyAdClosed(where);
                        Release();
                        onSuccess?.Invoke();
                        LoadByFloor(where, currentFloor);
                    });
                    ad.OnAdFullScreenContentFailed += (err) => MainThreadDispatcher.Enqueue(() =>
                    {
                        NotifyAdDisplayFailed(where, err.GetMessage());
                        Release();
                        onFail?.Invoke();
                        LoadByFloor(where, currentFloor);
                    });
                    ad.Show();
                    return;
                }
            }
            NotifyAdDisplayFailed(where, "not_ready_or_expired");
            onFail?.Invoke();
            Load(where);
#endif
        }
    }

    // ==========================================
    // BANNER AD & DISPATCHER (Chỉ load 1 tầng)
    // ==========================================
    public class AdmobBannerAd : BaseAdFormat, IBannerAd
    {
#if ADMOB_DEPENDENCIES_INSTALLED
        private readonly Dictionary<string, BannerView> _banners = new Dictionary<string, BannerView>();
#endif
        private readonly Dictionary<string, bool> _isLoaded = new Dictionary<string, bool>();

        /// <summary>
        /// Ad unit đã có lệnh Show nhưng chưa có banner sẵn — dùng để phát ĐÚNG một request.
        /// AdsManager.OnBannerLoaded sẽ gọi lại Show() khi load xong, lúc đó banner đã có và được hiện.
        /// </summary>
        private readonly HashSet<string> _pendingShow = new HashSet<string>();

        public AdmobBannerAd(AdUnitConfig config) : base(config, AdUnitType.Banner, "Admob") { }

        // OVERRIDE Tắt Waterfall: Banner chỉ Load duy nhất tầng All
        public override void Load(string where = null) => LoadByFloor(where, EcpmFloor.All);

        public override bool IsAvailable(string where = null)
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            string unitId = _config.ResolveUnitId(_adType, where, EcpmFloor.All);
            return _isLoaded.TryGetValue(unitId, out var isLoaded) && isLoaded;
#else
            return false;
#endif
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            var entry = _config.GetEntry(_adType, where, floor);
            MainThreadDispatcher.Enqueue(() =>
            {
                if (_banners.TryGetValue(unitId, out var oldBanner) && oldBanner != null) oldBanner.Destroy();
                _isLoaded[unitId] = false;

                var pos = entry.CollapsiblePlacement == CollapsibleBannerPlacement.Top ? AdPosition.Top : AdPosition.Bottom;
                var banner = new BannerView(unitId, GetAdMobBannerSize(entry.BannerSize), pos);
                _banners[unitId] = banner;
                banner.Hide();

                banner.OnBannerAdLoaded += () => MainThreadDispatcher.Enqueue(() =>
                {
                    // AdMob TỰ hiển thị BannerView ngay khi load xong. Gọi Hide() trước LoadAd() không
                    // có tác dụng bền → phải ẩn lại NGAY trong callback loaded để banner giữ trạng thái ẩn,
                    // chỉ hiện khi AdsManager chủ động gọi Show() qua cổng điều kiện (enable_banner...).
                    banner.Hide();
                    _isLoaded[unitId] = true;
                    HandleLoadSuccess(unitId, where);
                });
                banner.OnBannerAdLoadFailed += (err) => MainThreadDispatcher.Enqueue(() =>
                {
                    _isLoaded[unitId] = false;
                    // Mở lại cổng để lệnh Show kế tiếp được phép phát request mới
                    // (retry tự động của BaseAdFormat vẫn chạy song song).
                    _pendingShow.Remove(unitId);
                    banner.Destroy();
                    _banners.Remove(unitId);
                    HandleLoadFailed(unitId, where, floor, err?.GetMessage());
                });
                banner.OnAdPaid += (adValue) => { if (adValue != null) TrackRevenue(unitId, where, "Banner", adValue.Value * 0.000001f); };

                var request = new AdRequest();
                if (entry.CollapsiblePlacement != CollapsibleBannerPlacement.None)
                {
                    request.Extras.Add("collapsible", entry.CollapsiblePlacement == CollapsibleBannerPlacement.Top ? "top" : "bottom");
                    request.Extras.Add("collapsible_request_id", System.Guid.NewGuid().ToString());
                }
                banner.LoadAd(request);
            });
#endif
        }

        public void Show(string where)
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            MainThreadDispatcher.Enqueue(() =>
            {
                string unitId = _config.ResolveUnitId(_adType, where, EcpmFloor.All);
                if (string.IsNullOrEmpty(unitId)) return;

                // Đã có banner sẵn (kể cả collapsible) thì hiện luôn. TUYỆT ĐỐI không load lại ở đây:
                // Show() được gọi từ AdsManager.OnBannerLoaded, nên load lại sẽ tạo vòng lặp
                // load → loaded → Show → load … khiến banner không bao giờ hiện được.
                if (_isLoaded.TryGetValue(unitId, out bool loaded) && loaded
                    && _banners.TryGetValue(unitId, out var banner) && banner != null)
                {
                    _pendingShow.Remove(unitId);
                    NotifyAdDisplayed(where);
                    banner.Show();
                    return;
                }

                // Chưa có banner: phát đúng một request. Khi load xong, AdsManager.OnBannerLoaded
                // gọi lại Show() và nhánh trên sẽ hiện banner.
                if (_pendingShow.Add(unitId)) Load(where);
            });
#endif
        }

        public void Hide(string where)
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            string unitId = _config.ResolveUnitId(_adType, where, EcpmFloor.All);
            MainThreadDispatcher.Enqueue(() => { if (_banners.TryGetValue(unitId, out var banner) && banner != null) banner.Hide(); });
#endif
        }

        public void Restore(string where) => Show(where);

#if ADMOB_DEPENDENCIES_INSTALLED
        private AdSize GetAdMobBannerSize(BannerSize size)
        {
            switch (size)
            {
                case BannerSize.Banner: return AdSize.Banner;
                case BannerSize.Adaptive: return AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
                case BannerSize.MediumRectangle: return AdSize.MediumRectangle;
                case BannerSize.Leaderboard: return AdSize.Leaderboard;
                default: return new AdSize(320, 100);
            }
        }
#endif
    }

    public class AdmobBannerDispatcher : IBannerAd
    {
        private readonly AdmobBannerAd _standardBanner;
        private readonly AdmobNativeBannerBridge _nativeExpandBanner;
        private readonly AdUnitConfig _config;

        public AdmobBannerDispatcher(AdUnitConfig config)
        {
            _config = config;
            _standardBanner = new AdmobBannerAd(config);
            _nativeExpandBanner = new AdmobNativeBannerBridge(config);
            WireUpEvents(_standardBanner);
            WireUpEvents(_nativeExpandBanner);
            _nativeExpandBanner.OnCollapsedNativeBanner += OnCollapsedNativeBanner;
        }

        private void OnCollapsedNativeBanner(string where)
        {
            var wheres = _config.GetAllWhere();
            foreach (var w in wheres)
            {
                if (_standardBanner.IsAvailable(w))
                {
                    _standardBanner.Show(w);
                    AdsEvent.RaiseBannerSwap(where, w);
                    return;
                }
            }
            AdsEvent.RaiseBannerSwap(where, string.Empty);
        }

        public bool IsAvailable(string where = null) => GetTarget(where).IsAvailable(where);
        public event Action<string> OnAdLoaded;
        public event Action<string, string> OnAdLoadFailed;
        public event Action<string> OnAdDisplayed;
        public event Action<string, string> OnAdDisplayFailed;
        public event Action<string> OnAdClosed;
        private void WireUpEvents(IAdFormat adFormat)
        {
            if (adFormat == null) return;
            adFormat.OnAdLoaded += (where) => OnAdLoaded?.Invoke(where);
            adFormat.OnAdLoadFailed += (where, err) => OnAdLoadFailed?.Invoke(where, err);
            adFormat.OnAdDisplayed += (where) => OnAdDisplayed?.Invoke(where);
            adFormat.OnAdDisplayFailed += (where, err) => OnAdDisplayFailed?.Invoke(where, err);
            adFormat.OnAdClosed += (where) => OnAdClosed?.Invoke(where);
        }

        public void Load(string where = "default") => GetTarget(where).Load(where);
        public void LoadAll() { foreach (var p in _config.GetAllPlacements()) GetTarget(p).Load(p); }
        public void Show(string where = "default") => GetTarget(where).Show(where);
        public void Hide(string where = "default") => GetTarget(where).Hide(where);
        public void Restore(string where) => GetTarget(where).Restore(where);

        private IBannerAd GetTarget(string where)
        {
            var entry = _config.GetEntry(AdUnitType.Banner, where, EcpmFloor.All);
            return entry != null && entry.BannerFormat == BannerFormatType.NativeOverlay ? (IBannerAd)_nativeExpandBanner : _standardBanner;
        }
    }

    public class AdmobNativeFullscreenAd : BaseAdFormat, INativeFullScreenAd
    {
        // Dictionary để lưu lại ánh xạ unitId -> where khi gọi RequestAd
        private readonly Dictionary<string, string> _unitIdToWhere = new Dictionary<string, string>();

        public AdmobNativeFullscreenAd(AdUnitConfig config) : base(config, AdUnitType.NativeAd, "Admob")
        {
            // Đăng ký các sự kiện từ Manager
            FullScreenNativeAdManager.Instance.OnAdLoadedEvent += OnNativeAdLoaded;
            FullScreenNativeAdManager.Instance.OnAdLoadFailedEvent += OnNativeAdLoadFailed;
            FullScreenNativeAdManager.Instance.OnAdDisplayedEvent += OnNativeAdDisplayed;
            FullScreenNativeAdManager.Instance.OnAdClosedEvent += OnNativeAdClosed;
            FullScreenNativeAdManager.Instance.OnAdPaidEvent += OnNativeAdPaid;
        }

        public override bool IsAvailable(string where = null)
        {
            foreach (EcpmFloor floor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, floor);
                if (!string.IsNullOrEmpty(unitId) && FullScreenNativeAdManager.Instance.IsAdReady(unitId))
                {
                    return true;
                }
            }
            return false;
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)
        {
            // Lưu lại vết "where" của "unitId" này trước khi đẩy cho Manager tải
            _unitIdToWhere[unitId] = where;
            FullScreenNativeAdManager.Instance.RequestAd(unitId);
        }

        public void Show(string where, Action onSuccess, Action onFail)
        {
            foreach (var currentFloor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, currentFloor);
                if (string.IsNullOrEmpty(unitId)) continue;

                if (FullScreenNativeAdManager.Instance.IsAdReady(unitId))
                {
                    FullScreenNativeAdManager.Instance.ShowFullScreenAd(unitId, where);
                    return;
                }
            }

            onFail?.Invoke();
            Load(where);
        }

        public void Hide()
        {
            FullScreenNativeAdManager.Instance.ForceCloseAd();
        }

        // =========================================================================
        // CALLBACKS TỪ MANAGER
        // =========================================================================

        private void OnNativeAdPaid(string unitId, string where, double value)
        {
            TrackRevenue(unitId, where, "NativeFullscreen", value);
        }

        private void OnNativeAdClosed(string unitId, string where)
        {
            NotifyAdClosed(where);
            Load(where);
        }

        private void OnNativeAdLoaded(string unitId)
        {
            // Lấy lại vị trí "where" từ Dictionary đã lưu
            _unitIdToWhere.TryGetValue(unitId, out string where);
            HandleLoadSuccess(unitId, where ?? "default");
        }

        private void OnNativeAdLoadFailed(string unitId, string error)
        {
            // Lấy lại vị trí "where" từ Dictionary đã lưu
            _unitIdToWhere.TryGetValue(unitId, out string where);
            HandleLoadFailed(unitId, where ?? "default", EcpmFloor.All, error);
        }

        private void OnNativeAdDisplayed(string unitId, string where)
        {
            NotifyAdDisplayed(where);
        }
    }
}