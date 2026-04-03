using System;
using UnityEngine;
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

    public class IronSourceAds : MonoBehaviour, IAds, IBannerSizeConfig
    {
        [Header("LevelPlay App Key (bắt buộc - lấy từ LevelPlay dashboard)")]
        [SerializeField] private string levelPlayAppKey;

        [Header("Ad Unit / Placement IDs (để trống = dùng placement mặc định)")]
        [SerializeField] private string bannerAdUnitId;
        [SerializeField] private string interstitialAdUnitId;
        [SerializeField] private string rewardedVideoAdUnitId;

        public int OrderExecute { get; set; }

        public event Action OnInterstitialLoaded;
        public event Action<string> OnInterstitialLoadFailed;
        public event Action OnRewardedLoaded;
        public event Action<string> OnRewardedLoadFailed;

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

        public void Initialize()
        {
            if (_initialized)
            {
                Debug.Log("[CtySDK] IronSourceAds already initialized.");
                return;
            }

            if (string.IsNullOrEmpty(levelPlayAppKey))
            {
                Debug.LogWarning("[CtySDK] IronSourceAds: LevelPlay App Key not set.");
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
                Debug.Log("[CtySDK] IronSourceAds (LevelPlay) initialized.");
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

        private void OnLevelPlayInitFailed(LevelPlayInitError error)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                _initialized = true;
                LevelPlay.OnInitSuccess -= OnLevelPlayInitSuccess;
                LevelPlay.OnInitFailed -= OnLevelPlayInitFailed;
                Debug.Log("[CtySDK] IronSourceAds LevelPlay init failed: " + error);
            });
        }

        private void CreateAdUnits()
        {
            var bannerId = string.IsNullOrEmpty(bannerAdUnitId) ? DefaultBannerId : bannerAdUnitId;
            var interId = string.IsNullOrEmpty(interstitialAdUnitId) ? DefaultInterstitialId : interstitialAdUnitId;
            var rewardId = string.IsNullOrEmpty(rewardedVideoAdUnitId) ? DefaultRewardedId : rewardedVideoAdUnitId;

            // SetDisplayOnLoad(false): không tự hiện sau khi load; chỉ hiện khi AdsManager gọi ShowBanner → ShowAd().
            var bannerConfig = new LevelPlayBannerAd.Config.Builder()
                .SetSize(GetLevelPlayAdSize(_bannerSize))
                .SetPosition(LevelPlayBannerPosition.BottomCenter)
                .SetDisplayOnLoad(false)
                .Build();
            _bannerAd = new LevelPlayBannerAd(bannerId, bannerConfig);
            _interstitialAd = new LevelPlayInterstitialAd(interId);
            _rewardedAd = new LevelPlayRewardedAd(rewardId);
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
            LevelPlay.SetConsent(true);
            Debug.Log("[CtySDK] IronSourceAds SetAfterCheckGDPR (consent set).");
        }

        public void RequestBanner() { _bannerAd?.LoadAd(); }
        public void RequestInterstitial() { _interstitialAd?.LoadAd(); }
        public void RequestRewardedVideo() { _rewardedAd?.LoadAd(); }
        public void RequestAppOpenAds() { }

        public bool IsBannerAvailable() => _bannerAd != null;
        public bool IsInterstitialAvailable() => _interstitialAd != null && _interstitialAd.IsAdReady();
        public bool IsRewardedVideoAvailable() => _rewardedAd != null && _rewardedAd.IsAdReady();
        public bool IsAppOpenAdsAvailable() => false;

        public void ShowBanner(string where) { _bannerAd?.ShowAd(); }
        public void HideBanner(string where) { _bannerAd?.HideAd(); }

        public void ShowInterstitial(string where, Action onSuccess, Action onFail)
        {
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
        }
#else
        public void Initialize() { }
        public void SetAfterCheckGDPR() { }
        public void RequestBanner() { }
        public void RequestInterstitial() { }
        public void RequestRewardedVideo() { }
        public void RequestAppOpenAds() { }
        public bool IsBannerAvailable() => false;
        public bool IsInterstitialAvailable() => false;
        public bool IsRewardedVideoAvailable() => false;
        public bool IsAppOpenAdsAvailable() => false;
        public void ShowBanner(string where) { }
        public void HideBanner(string where) { }
        public void ShowInterstitial(string where, Action onSuccess, Action onFail) => onFail?.Invoke();
        public void ShowRewardedVideo(string where, Action onSuccess, Action onFail) => onFail?.Invoke();
        public void ShowAppOpenAds(string where, Action onSuccess, Action onFail) => onFail?.Invoke();
#endif
    }
}
