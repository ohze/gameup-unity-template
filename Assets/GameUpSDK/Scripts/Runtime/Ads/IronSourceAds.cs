using System;
using UnityEngine;
using GameUp.Core;
#if LEVELPLAY_DEPENDENCIES_INSTALLED
using Unity.Services.LevelPlay;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// IronSource (LevelPlay) Mediation implementation of IAds.
    /// Chỉ cần App Key để lấy quảng cáo; AdMob và Unity Ads chạy qua mediation.
    /// Nếu không nhập Ad Unit ID, dùng placement mặc định (DefaultBanner, DefaultInterstitial, DefaultRewardedVideo).
    /// LevelPlay không hỗ trợ App Open; các method App Open no-op / return false.
    /// </summary>

    public class IronSourceAds : MonoBehaviour, IAds, IBannerSizeConfig, IPlacementAwareAds, IAdUnitIdResolver, IConsentAwareAds
    {
        [Header("LevelPlay App Key (bắt buộc - lấy từ LevelPlay dashboard)")]
        [SerializeField] private string levelPlayAppKey;

        [Header("Multi Ad Unit IDs")]
        [Tooltip("Bật để dùng nhiều Placement/Ad Unit theo placement key (where). Tắt = dùng 1 ID/format như hiện tại.")]
        [SerializeField] private bool useMultiAdUnitIds;

        [Tooltip("Danh sách mapping: (AdType, NameId=where, Id=placement id). Chỉ dùng khi useMultiAdUnitIds=true.")]
        [SerializeField] private System.Collections.Generic.List<AdUnitIdEntry> adUnitIds = new System.Collections.Generic.List<AdUnitIdEntry>();

        [Header("Ad Unit / Placement IDs (để trống = dùng placement mặc định)")]
        [SerializeField] private string bannerAdUnitId;
        [SerializeField] private string interstitialAdUnitId;
        [SerializeField] private string rewardedVideoAdUnitId;

        public int OrderExecute { get; set; }

        public event Action OnInterstitialLoaded;
        public event Action<string> OnInterstitialLoadFailed;
        public event Action OnRewardedLoaded;
        public event Action<string> OnRewardedLoadFailed;
        public event Action<string> OnBannerShown;
        public event Action<string> OnBannerShowFailed;

        private BannerSize _bannerSize = BannerSize.Large;

        /// <inheritdoc/>
        public void SetBannerSize(BannerSize size) => _bannerSize = size;

        public void SetLevelPlayConfig(string appKey, string bannerId, string interstitialId, string rewardedId)
        {
            levelPlayAppKey = appKey;
            bannerAdUnitId = bannerId;
            interstitialAdUnitId = interstitialId;
            rewardedVideoAdUnitId = rewardedId;
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private const string DefaultBannerId = "DefaultBanner";
        private const string DefaultInterstitialId = "DefaultInterstitial";
        private const string DefaultRewardedId = "DefaultRewardedVideo";

        private bool _initialized;
        private LevelPlayBannerAd _bannerAd;
        private LevelPlayInterstitialAd _interstitialAd;
        private LevelPlayRewardedAd _rewardedAd;
        private bool _bannerLoaded;

        private readonly System.Collections.Generic.Dictionary<string, LevelPlayBannerAd> _bannerByWhere = new System.Collections.Generic.Dictionary<string, LevelPlayBannerAd>();
        private readonly System.Collections.Generic.Dictionary<string, LevelPlayInterstitialAd> _interstitialByWhere = new System.Collections.Generic.Dictionary<string, LevelPlayInterstitialAd>();
        private readonly System.Collections.Generic.Dictionary<string, LevelPlayRewardedAd> _rewardedByWhere = new System.Collections.Generic.Dictionary<string, LevelPlayRewardedAd>();
        private readonly System.Collections.Generic.Dictionary<string, bool> _bannerLoadedByWhere = new System.Collections.Generic.Dictionary<string, bool>();

        public void Initialize()
        {
            if (_initialized)
            {
                GULogger.Log("GameUp", "IronSourceAds already initialized.");
                return;
            }

            if (string.IsNullOrEmpty(levelPlayAppKey))
            {
                GULogger.Warning("GameUp", "IronSourceAds: LevelPlay App Key not set.");
                _initialized = true;
                return;
            }

            LevelPlay.OnInitSuccess += OnLevelPlayInitSuccess;
            LevelPlay.OnInitFailed += OnLevelPlayInitFailed;
            LevelPlay.Init(levelPlayAppKey);
        }

        private void OnLevelPlayInitSuccess(LevelPlayConfiguration config)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                _initialized = true;
                LevelPlay.OnInitSuccess -= OnLevelPlayInitSuccess;
                LevelPlay.OnInitFailed -= OnLevelPlayInitFailed;
                CreateAdUnits();
                SubscribeToAdEvents();
                SubscribeToImpressionData();
                RequestBanner();
                RequestInterstitial();
                RequestRewardedVideo();
                GULogger.Log("GameUp", "IronSourceAds (LevelPlay) initialized.");
            });
        }

        /// <summary>
        /// Subscribe to LevelPlay impression data (fired after ad is shown with revenue). Forward to AdsEvent for GameUpAnalytics.LogAdImpression.
        /// OnImpressionDataReady runs on background thread → dispatch to main thread then raise.
        /// </summary>
        private void SubscribeToImpressionData()
        {
            LevelPlay.OnImpressionDataReady += OnLevelPlayImpressionDataReady;
        }

        private void OnLevelPlayImpressionDataReady(LevelPlayImpressionData levelPlayData)
        {
            if (levelPlayData == null || !levelPlayData.Revenue.HasValue)
                return;
            var data = new AdImpressionData
            {
                AdNetwork = levelPlayData.AdNetwork,
                AdUnit = levelPlayData.MediationAdUnitName ?? levelPlayData.MediationAdUnitId,
                InstanceName = levelPlayData.InstanceName,
                AdFormat = levelPlayData.AdFormat,
                Revenue = levelPlayData.Revenue
            };
            MainThreadDispatcher.Enqueue(() => AdsEvent.RaiseImpressionDataReady(data));
        }

        private void SubscribeToAdEvents()
        {
            if (_bannerAd != null)
                RegisterBannerEvents(_bannerAd, where: null);
            foreach (var kv in _bannerByWhere)
            {
                if (kv.Value != null)
                    RegisterBannerEvents(kv.Value, kv.Key);
            }
            if (_interstitialAd != null)
            {
                _interstitialAd.OnAdLoaded += _ => MainThreadDispatcher.Enqueue(() => OnInterstitialLoaded?.Invoke());
                _interstitialAd.OnAdLoadFailed += (error) => MainThreadDispatcher.Enqueue(() =>
                    OnInterstitialLoadFailed?.Invoke(error?.ErrorMessage ?? error?.ErrorCode.ToString() ?? "unknown"));
            }
            if (_rewardedAd != null)
            {
                _rewardedAd.OnAdLoaded += _ => MainThreadDispatcher.Enqueue(() => OnRewardedLoaded?.Invoke());
                _rewardedAd.OnAdLoadFailed += (error) => MainThreadDispatcher.Enqueue(() =>
                    OnRewardedLoadFailed?.Invoke(error?.ErrorMessage ?? error?.ErrorCode.ToString() ?? "unknown"));
            }
        }

        private void RegisterBannerEvents(LevelPlayBannerAd ad, string where)
        {
            ad.OnAdLoaded += _ => MainThreadDispatcher.Enqueue(() =>
            {
                if (!useMultiAdUnitIds || string.IsNullOrEmpty(where))
                    _bannerLoaded = true;
                else
                    _bannerLoadedByWhere[where] = true;
            });
            ad.OnAdLoadFailed += _ => MainThreadDispatcher.Enqueue(() =>
            {
                if (!useMultiAdUnitIds || string.IsNullOrEmpty(where))
                    _bannerLoaded = false;
                else
                    _bannerLoadedByWhere[where] = false;
            });
        }

        private void OnLevelPlayInitFailed(LevelPlayInitError error)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                _initialized = true;
                LevelPlay.OnInitSuccess -= OnLevelPlayInitSuccess;
                LevelPlay.OnInitFailed -= OnLevelPlayInitFailed;
                GULogger.Log("GameUp", $"IronSourceAds LevelPlay init failed: {error}");
            });
        }

        private void CreateAdUnits()
        {
            // SetDisplayOnLoad(false): không tự hiện sau khi load; chỉ hiện khi AdsManager gọi ShowBanner → ShowAd().
            var bannerConfig = new LevelPlayBannerAd.Config.Builder()
                .SetSize(GetLevelPlayAdSize(_bannerSize))
                .SetPosition(LevelPlayBannerPosition.BottomCenter)
                .SetDisplayOnLoad(false)
                .Build();

            if (!useMultiAdUnitIds)
            {
                var bannerId = string.IsNullOrEmpty(bannerAdUnitId) ? DefaultBannerId : bannerAdUnitId;
                var interId = string.IsNullOrEmpty(interstitialAdUnitId) ? DefaultInterstitialId : interstitialAdUnitId;
                var rewardId = string.IsNullOrEmpty(rewardedVideoAdUnitId) ? DefaultRewardedId : rewardedVideoAdUnitId;

                _bannerAd = new LevelPlayBannerAd(bannerId, bannerConfig);
                _interstitialAd = new LevelPlayInterstitialAd(interId);
                _rewardedAd = new LevelPlayRewardedAd(rewardId);
                return;
            }

            _bannerByWhere.Clear();
            _interstitialByWhere.Clear();
            _rewardedByWhere.Clear();
            _bannerLoadedByWhere.Clear();

            for (int i = 0; i < adUnitIds.Count; i++)
            {
                var e = adUnitIds[i];
                if (e == null || !e.IsValid()) continue;
                if (string.IsNullOrEmpty(e.NameId)) continue;

                switch (e.AdType)
                {
                    case AdUnitType.Banner:
                        if (!_bannerByWhere.ContainsKey(e.NameId))
                            _bannerByWhere[e.NameId] = new LevelPlayBannerAd(e.Id, bannerConfig);
                        break;
                    case AdUnitType.Interstitial:
                        if (!_interstitialByWhere.ContainsKey(e.NameId))
                            _interstitialByWhere[e.NameId] = new LevelPlayInterstitialAd(e.Id);
                        break;
                    case AdUnitType.RewardedVideo:
                        if (!_rewardedByWhere.ContainsKey(e.NameId))
                            _rewardedByWhere[e.NameId] = new LevelPlayRewardedAd(e.Id);
                        break;
                }
            }

            // Fallback single/default objects for callers still using old APIs.
            var bannerFallbackId = string.IsNullOrEmpty(bannerAdUnitId) ? DefaultBannerId : bannerAdUnitId;
            var interFallbackId = string.IsNullOrEmpty(interstitialAdUnitId) ? DefaultInterstitialId : interstitialAdUnitId;
            var rewardFallbackId = string.IsNullOrEmpty(rewardedVideoAdUnitId) ? DefaultRewardedId : rewardedVideoAdUnitId;
            _bannerAd = new LevelPlayBannerAd(bannerFallbackId, bannerConfig);
            _interstitialAd = new LevelPlayInterstitialAd(interFallbackId);
            _rewardedAd = new LevelPlayRewardedAd(rewardFallbackId);
        }

        private static LevelPlayAdSize GetLevelPlayAdSize(BannerSize size)
        {
            switch (size)
            {
                case BannerSize.Banner: return LevelPlayAdSize.BANNER;
                case BannerSize.Adaptive: return LevelPlayAdSize.CreateAdaptiveAdSize();
                case BannerSize.MediumRectangle: return LevelPlayAdSize.MEDIUM_RECTANGLE;
                case BannerSize.Leaderboard: return LevelPlayAdSize.LEADERBOARD;
                default: return LevelPlayAdSize.LARGE;
            }
        }

        public void SetAfterCheckGDPR()
        {
            SetAfterCheckGDPR(true);
        }

        public void SetAfterCheckGDPR(bool isConsent)
        {
            LevelPlay.SetConsent(isConsent);
            GULogger.Log("GameUp", $"IronSourceAds SetAfterCheckGDPR (consent={isConsent}).");
        }

        public void RequestBanner()
        {
            if (!useMultiAdUnitIds)
            {
                _bannerLoaded = false;
                _bannerAd?.LoadAd();
                return;
            }
            _bannerLoaded = false;
            foreach (var kv in _bannerByWhere)
            {
                _bannerLoadedByWhere[kv.Key] = false;
                kv.Value?.LoadAd();
            }
            _bannerAd?.LoadAd();
        }

        public void RequestCollapsibleBanner(string where, CollapsibleBannerPlacement placement = CollapsibleBannerPlacement.Bottom)
        {
            RequestBanner();
        }

        public void RequestInterstitial()
        {
            if (!useMultiAdUnitIds)
            {
                _interstitialAd?.LoadAd();
                return;
            }
            foreach (var kv in _interstitialByWhere)
                kv.Value?.LoadAd();
            _interstitialAd?.LoadAd();
        }

        public void RequestRewardedVideo()
        {
            if (!useMultiAdUnitIds)
            {
                _rewardedAd?.LoadAd();
                return;
            }
            foreach (var kv in _rewardedByWhere)
                kv.Value?.LoadAd();
            _rewardedAd?.LoadAd();
        }
        public void RequestAppOpenAds() { }

        public bool IsBannerAvailable() => _bannerAd != null && _bannerLoaded;
        public bool IsCollapsibleBannerAvailable() => false;
        public bool IsInterstitialAvailable() => _interstitialAd != null && _interstitialAd.IsAdReady();
        public bool IsRewardedVideoAvailable() => _rewardedAd != null && _rewardedAd.IsAdReady();
        public bool IsAppOpenAdsAvailable() => false;

        public void ShowBanner(string where)
        {
            if (useMultiAdUnitIds && !string.IsNullOrEmpty(where) && _bannerByWhere.TryGetValue(where, out var b) && b != null)
            {
                if (!_bannerLoadedByWhere.TryGetValue(where, out var isLoaded) || !isLoaded)
                {
                    OnBannerShowFailed?.Invoke(where);
                    return;
                }
                b.ShowAd();
                OnBannerShown?.Invoke(where);
                return;
            }
            if (!IsBannerAvailable())
            {
                OnBannerShowFailed?.Invoke(string.IsNullOrEmpty(where) ? "main" : where);
                return;
            }
            _bannerAd?.ShowAd();
            OnBannerShown?.Invoke(string.IsNullOrEmpty(where) ? "main" : where);
        }

        public void ShowCollapsibleBanner(string where, CollapsibleBannerPlacement placement = CollapsibleBannerPlacement.Bottom)
        {
            ShowBanner(where);
        }

        public void HideBanner(string where) { _bannerAd?.HideAd(); }

        public void ShowInterstitial(string where, Action onSuccess, Action onFail)
        {
            if (useMultiAdUnitIds && !string.IsNullOrEmpty(where) && _interstitialByWhere.TryGetValue(where, out var interMulti) && interMulti != null)
            {
                if (!interMulti.IsAdReady()) { onFail?.Invoke(); return; }
                interMulti.OnAdClosed += OnInterstitialClosedMulti;
                interMulti.OnAdDisplayFailed += OnInterstitialDisplayFailedMulti;

                void OnInterstitialClosedMulti(LevelPlayAdInfo _)
                {
                    interMulti.OnAdClosed -= OnInterstitialClosedMulti;
                    interMulti.OnAdDisplayFailed -= OnInterstitialDisplayFailedMulti;
                    MainThreadDispatcher.Enqueue(() => onSuccess?.Invoke());
                    interMulti.LoadAd();
                }

                void OnInterstitialDisplayFailedMulti(LevelPlayAdInfo _, LevelPlayAdError __)
                {
                    interMulti.OnAdClosed -= OnInterstitialClosedMulti;
                    interMulti.OnAdDisplayFailed -= OnInterstitialDisplayFailedMulti;
                    MainThreadDispatcher.Enqueue(() => onFail?.Invoke());
                    interMulti.LoadAd();
                }

                interMulti.ShowAd(where);
                return;
            }

            if (_interstitialAd == null || !_interstitialAd.IsAdReady()) { onFail?.Invoke(); return; }
            _interstitialAd.OnAdClosed += OnInterstitialClosed;
            _interstitialAd.OnAdDisplayFailed += OnInterstitialDisplayFailed;

            void OnInterstitialClosed(LevelPlayAdInfo _)
            {
                _interstitialAd.OnAdClosed -= OnInterstitialClosed;
                _interstitialAd.OnAdDisplayFailed -= OnInterstitialDisplayFailed;
                MainThreadDispatcher.Enqueue(() => onSuccess?.Invoke());
                RequestInterstitial();
            }

            void OnInterstitialDisplayFailed(LevelPlayAdInfo _, LevelPlayAdError __)
            {
                _interstitialAd.OnAdClosed -= OnInterstitialClosed;
                _interstitialAd.OnAdDisplayFailed -= OnInterstitialDisplayFailed;
                MainThreadDispatcher.Enqueue(() => onFail?.Invoke());
                RequestInterstitial();
            }

            _interstitialAd.ShowAd(where);
        }

        public void ShowRewardedVideo(string where, Action onSuccess, Action onFail)
        {
            if (useMultiAdUnitIds && !string.IsNullOrEmpty(where) && _rewardedByWhere.TryGetValue(where, out var rewardMulti) && rewardMulti != null)
            {
                if (!rewardMulti.IsAdReady()) { onFail?.Invoke(); return; }
                AdsRules.BeginInterstitialCappingPause();
                var rewardGrantedMulti = false;
                rewardMulti.OnAdClosed += OnRewardedClosedMulti;
                rewardMulti.OnAdRewarded += OnRewardedEarnedMulti;
                rewardMulti.OnAdDisplayFailed += OnRewardedDisplayFailedMulti;

                void OnRewardedClosedMulti(LevelPlayAdInfo _)
                {
                    rewardMulti.OnAdClosed -= OnRewardedClosedMulti;
                    rewardMulti.OnAdRewarded -= OnRewardedEarnedMulti;
                    rewardMulti.OnAdDisplayFailed -= OnRewardedDisplayFailedMulti;
                    AdsRules.EndInterstitialCappingPause();
                    if (!rewardGrantedMulti) MainThreadDispatcher.Enqueue(() => onFail?.Invoke());
                    rewardMulti.LoadAd();
                }

                void OnRewardedEarnedMulti(LevelPlayAdInfo _, LevelPlayReward __)
                {
                    rewardGrantedMulti = true;
                    MainThreadDispatcher.Enqueue(() => onSuccess?.Invoke());
                }

                void OnRewardedDisplayFailedMulti(LevelPlayAdInfo _, LevelPlayAdError __)
                {
                    rewardMulti.OnAdClosed -= OnRewardedClosedMulti;
                    rewardMulti.OnAdRewarded -= OnRewardedEarnedMulti;
                    rewardMulti.OnAdDisplayFailed -= OnRewardedDisplayFailedMulti;
                    AdsRules.EndInterstitialCappingPause();
                    MainThreadDispatcher.Enqueue(() => onFail?.Invoke());
                    rewardMulti.LoadAd();
                }

                rewardMulti.ShowAd(where);
                return;
            }

            if (_rewardedAd == null || !_rewardedAd.IsAdReady()) { onFail?.Invoke(); return; }
            AdsRules.BeginInterstitialCappingPause();
            var rewardGranted = false;
            _rewardedAd.OnAdClosed += OnRewardedClosed;
            _rewardedAd.OnAdRewarded += OnRewardedEarned;
            _rewardedAd.OnAdDisplayFailed += OnRewardedDisplayFailed;

            void OnRewardedClosed(LevelPlayAdInfo _)
            {
                _rewardedAd.OnAdClosed -= OnRewardedClosed;
                _rewardedAd.OnAdRewarded -= OnRewardedEarned;
                _rewardedAd.OnAdDisplayFailed -= OnRewardedDisplayFailed;
                AdsRules.EndInterstitialCappingPause();
                if (!rewardGranted) MainThreadDispatcher.Enqueue(() => onFail?.Invoke());
                RequestRewardedVideo();
            }

            void OnRewardedEarned(LevelPlayAdInfo _, LevelPlayReward __)
            {
                rewardGranted = true;
                MainThreadDispatcher.Enqueue(() => onSuccess?.Invoke());
            }

            void OnRewardedDisplayFailed(LevelPlayAdInfo _, LevelPlayAdError __)
            {
                _rewardedAd.OnAdClosed -= OnRewardedClosed;
                _rewardedAd.OnAdRewarded -= OnRewardedEarned;
                _rewardedAd.OnAdDisplayFailed -= OnRewardedDisplayFailed;
                AdsRules.EndInterstitialCappingPause();
                MainThreadDispatcher.Enqueue(() => onFail?.Invoke());
                RequestRewardedVideo();
            }

            _rewardedAd.ShowAd(where);
        }

        public void ShowAppOpenAds(string where, Action onSuccess, Action onFail) { onFail?.Invoke(); }

        private void OnDestroy()
        {
            LevelPlay.OnImpressionDataReady -= OnLevelPlayImpressionDataReady;
            _bannerAd?.DestroyAd(); _bannerAd = null;
            _interstitialAd?.DestroyAd(); _interstitialAd = null;
            _rewardedAd?.Dispose(); _rewardedAd = null;

            foreach (var kv in _bannerByWhere) kv.Value?.DestroyAd();
            foreach (var kv in _interstitialByWhere) kv.Value?.DestroyAd();
            foreach (var kv in _rewardedByWhere) kv.Value?.Dispose();
            _bannerByWhere.Clear();
            _interstitialByWhere.Clear();
            _rewardedByWhere.Clear();
        }
#else
        public void Initialize() { }
        public void SetAfterCheckGDPR() { }
        public void SetAfterCheckGDPR(bool isConsent) { }
        public void RequestBanner() { }
        public void RequestCollapsibleBanner(string where, CollapsibleBannerPlacement placement = CollapsibleBannerPlacement.Bottom) { }
        public void RequestInterstitial() { }
        public void RequestRewardedVideo() { }
        public void RequestAppOpenAds() { }
        public bool IsBannerAvailable() => false;
        public bool IsCollapsibleBannerAvailable() => false;
        public bool IsInterstitialAvailable() => false;
        public bool IsRewardedVideoAvailable() => false;
        public bool IsAppOpenAdsAvailable() => false;
        public void ShowBanner(string where) { }
        public void ShowCollapsibleBanner(string where, CollapsibleBannerPlacement placement = CollapsibleBannerPlacement.Bottom) { }
        public void HideBanner(string where) { }
        public void ShowInterstitial(string where, Action onSuccess, Action onFail) => onFail?.Invoke();
        public void ShowRewardedVideo(string where, Action onSuccess, Action onFail) => onFail?.Invoke();
        public void ShowAppOpenAds(string where, Action onSuccess, Action onFail) => onFail?.Invoke();
#endif

        bool IPlacementAwareAds.IsBannerAvailable(string where)
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            if (!useMultiAdUnitIds) return IsBannerAvailable();
            return !string.IsNullOrEmpty(where) &&
                   _bannerByWhere.ContainsKey(where) &&
                   _bannerLoadedByWhere.TryGetValue(where, out var loaded) &&
                   loaded;
#else
            return false;
#endif
        }

        bool IPlacementAwareAds.IsCollapsibleBannerAvailable(string where) => false;

        bool IPlacementAwareAds.IsInterstitialAvailable(string where)
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            if (!useMultiAdUnitIds) return IsInterstitialAvailable();
            return !string.IsNullOrEmpty(where) &&
                   _interstitialByWhere.TryGetValue(where, out var ad) &&
                   ad != null &&
                   ad.IsAdReady();
#else
            return false;
#endif
        }

        bool IPlacementAwareAds.IsRewardedVideoAvailable(string where)
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            if (!useMultiAdUnitIds) return IsRewardedVideoAvailable();
            return !string.IsNullOrEmpty(where) &&
                   _rewardedByWhere.TryGetValue(where, out var ad) &&
                   ad != null &&
                   ad.IsAdReady();
#else
            return false;
#endif
        }

        bool IPlacementAwareAds.IsAppOpenAdsAvailable(string where) => false;

        bool IAdUnitIdResolver.TryResolve(int intId, out AdUnitType adType, out string nameId)
        {
            adType = AdUnitType.Interstitial;
            nameId = null;

            if (!useMultiAdUnitIds || adUnitIds == null || adUnitIds.Count == 0)
                return false;

            for (int i = 0; i < adUnitIds.Count; i++)
            {
                var e = adUnitIds[i];
                if (e == null) continue;
                if (e.intId != intId) continue;
                if (!e.IsValid()) continue;
                adType = e.AdType;
                nameId = e.NameId;
                return !string.IsNullOrEmpty(nameId);
            }
            return false;
        }
    }
}
