using System;
using UnityEngine;
#if LEVELPLAY_DEPENDENCIES_INSTALLED
using Unity.Services.LevelPlay;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// UnityAds wrapper for ironSource (LevelPlay) Mediation. Manages ads through the LevelPlay SDK,
    /// logs ads_unity_* events to Firebase, and ensures callbacks run on the main thread.
    /// </summary>
    public class UnityAds : MonoBehaviour, IAds
    {
        [Header("LevelPlay App Key (optional - set via code)")]
        [SerializeField] private string levelPlayAppKey;

        [Header("Ad Unit IDs")]
        [SerializeField] private string bannerAdUnitId;
        [SerializeField] private string interstitialAdUnitId;
        [SerializeField] private string rewardedVideoAdUnitId;

        public int OrderExecute { get; set; }

        public event Action OnInterstitialLoaded;
        public event Action<string> OnInterstitialLoadFailed;
        public event Action OnRewardedLoaded;
        public event Action<string> OnRewardedLoadFailed;

        public void SetLevelPlayConfig(string appKey, string bannerId, string interstitialId, string rewardedId)
        {
            levelPlayAppKey = appKey;
            bannerAdUnitId = bannerId;
            interstitialAdUnitId = interstitialId;
            rewardedVideoAdUnitId = rewardedId;
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private bool _initialized;
        private bool _bannerLoaded;
        private LevelPlayBannerAd _bannerAd;
        private LevelPlayInterstitialAd _interstitialAd;
        private LevelPlayRewardedAd _rewardedAd;

        public void Initialize()
        {
            if (_initialized)
            {
                Debug.Log("[CtySDK] UnityAds already initialized.");
                return;
            }

            if (string.IsNullOrEmpty(levelPlayAppKey))
            {
                Debug.LogWarning("[CtySDK] UnityAds: LevelPlay App Key not set.");
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
                Debug.Log("[CtySDK] UnityAds (LevelPlay) initialized.");
            });
        }

        private void OnLevelPlayInitFailed(LevelPlayInitError error)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                _initialized = true;
                LevelPlay.OnInitSuccess -= OnLevelPlayInitSuccess;
                LevelPlay.OnInitFailed -= OnLevelPlayInitFailed;
                Debug.Log("[CtySDK] UnityAds LevelPlay init failed: " + error);
            });
        }

        private void CreateAdUnits()
        {
            if (!string.IsNullOrEmpty(bannerAdUnitId))
                _bannerAd = new LevelPlayBannerAd(bannerAdUnitId);
            if (!string.IsNullOrEmpty(interstitialAdUnitId))
                _interstitialAd = new LevelPlayInterstitialAd(interstitialAdUnitId);
            if (!string.IsNullOrEmpty(rewardedVideoAdUnitId))
                _rewardedAd = new LevelPlayRewardedAd(rewardedVideoAdUnitId);
        }

        private void SubscribeToAdEvents()
        {
            if (_bannerAd != null)
            {
                _bannerAd.OnAdLoaded += (info) => MainThreadDispatcher.Enqueue(() => _bannerLoaded = true);
                _bannerAd.OnAdLoadFailed += (error) => MainThreadDispatcher.Enqueue(() => _bannerLoaded = false);
            }
            if (_interstitialAd != null)
            {
                _interstitialAd.OnAdLoaded += (info) => MainThreadDispatcher.Enqueue(() => OnInterstitialLoaded?.Invoke());
                _interstitialAd.OnAdLoadFailed += (error) => MainThreadDispatcher.Enqueue(() =>
                    OnInterstitialLoadFailed?.Invoke(error?.ErrorMessage ?? error?.ErrorCode.ToString() ?? "unknown"));
                _interstitialAd.OnAdDisplayFailed += (info, error) => MainThreadDispatcher.Enqueue(() => { });
            }
            if (_rewardedAd != null)
            {
                _rewardedAd.OnAdLoaded += (info) => MainThreadDispatcher.Enqueue(() => OnRewardedLoaded?.Invoke());
                _rewardedAd.OnAdLoadFailed += (error) => MainThreadDispatcher.Enqueue(() =>
                    OnRewardedLoadFailed?.Invoke(error?.ErrorMessage ?? error?.ErrorCode.ToString() ?? "unknown"));
                _rewardedAd.OnAdDisplayFailed += (info, error) => MainThreadDispatcher.Enqueue(() => { });
                _rewardedAd.OnAdRewarded += (info, reward) => MainThreadDispatcher.Enqueue(() => { });
            }
        }

        public void SetAfterCheckGDPR()
        {
            LevelPlay.SetConsent(true);
            Debug.Log("[CtySDK] UnityAds SetAfterCheckGDPR (consent set).");
        }

        public void RequestBanner()
        {
            if (_bannerAd == null) { Debug.Log("[CtySDK] UnityAds RequestBanner: banner ad unit not configured."); return; }
            _bannerAd.LoadAd();
        }

        public void RequestInterstitial() { _interstitialAd?.LoadAd(); }
        public void RequestRewardedVideo() { _rewardedAd?.LoadAd(); }
        public void RequestAppOpenAds() { }

        public bool IsBannerAvailable() => _bannerAd != null && _bannerLoaded;
        public bool IsInterstitialAvailable() => _interstitialAd != null && _interstitialAd.IsAdReady();
        public bool IsRewardedVideoAvailable() => _rewardedAd != null && _rewardedAd.IsAdReady();
        public bool IsAppOpenAdsAvailable() => false;

        public void ShowBanner(string where)
        {
            if (_bannerAd == null) { Debug.LogWarning("[CtySDK] UnityAds ShowBanner: banner not configured."); return; }
            if (!_bannerLoaded) { Debug.Log("[CtySDK] UnityAds ShowBanner: banner not loaded yet."); return; }
            _bannerAd.ShowAd();
        }

        public void HideBanner(string where) { _bannerAd?.HideAd(); }

        public void ShowInterstitial(string where, Action onSuccess, Action onFail)
        {
            if (_interstitialAd == null || !_interstitialAd.IsAdReady())
            {
                Debug.Log("[CtySDK] UnityAds ShowInterstitial: ad not ready.");
                onFail?.Invoke(); return;
            }
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
            if (_rewardedAd == null || !_rewardedAd.IsAdReady())
            {
                Debug.Log("[CtySDK] UnityAds ShowRewardedVideo: ad not ready.");
                onFail?.Invoke(); return;
            }
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

        public void ShowAppOpenAds(string where, Action onSuccess, Action onFail)
        {
            Debug.Log("[CtySDK] UnityAds ShowAppOpenAds: not supported by LevelPlay.");
            onFail?.Invoke();
        }

        private void OnDestroy()
        {
            _bannerLoaded = false;
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
