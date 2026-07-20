using GameUp.Core;
using System;
using UnityEngine;

namespace GameUp.SDK
{
    public class IronSourceNetwork : MonoBehaviour, IAdNetwork
    {
        [SerializeField] private string levelPlayAppKey;

        [Header("Ad Unit Configs (Placements)")]
        public AdUnitConfig interstitialConfig;

        public AdUnitConfig rewardedConfig;
        public AdUnitConfig bannerConfig;

        public Action<IAdNetwork> OnInitialized { get; set; }
        public MediationProvider MediationProvider { get; set; } = MediationProvider.IronSource;
        public bool IsInitialized { get; private set; }
        public IInterstitialAd InterstitialAd { get; private set; }
        public IRewardedAd RewardedAd { get; private set; }
        public IAppOpenAd AppOpenAd { get; private set; }
        public IBannerAd BannerAd { get; private set; }

        public INativeFullScreenAd NativeFullScreenAd { get; private set; }

        public void Initialize()
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            if (IsInitialized || string.IsNullOrEmpty(levelPlayAppKey)) return;

            Unity.Services.LevelPlay.LevelPlay.OnInitSuccess += (config) =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    IsInitialized = true;
                    GULogger.Log("[GameUp] IronSourceNetwork Initialized.");

                    InterstitialAd = new IronSourceInterstitialAd(interstitialConfig);
                    RewardedAd = new IronSourceRewardedAd(rewardedConfig);
                    BannerAd = new IronSourceBannerAd(bannerConfig);
                    AppOpenAd = new DummyAppOpenAd();
                    NativeFullScreenAd = new DummyNativeFullscreenAd();
                    // LevelPlay không có AppOpenAd, gán null hoặc tạo 1 class Dummy trả về false

                    Unity.Services.LevelPlay.LevelPlay.OnImpressionDataReady += OnImpression;
                    AppOpenAd.LoadAll();
                    NativeFullScreenAd.LoadAll();
                    BannerAd.LoadAll();

                    InterstitialAd.LoadAll();
                    RewardedAd.LoadAll();
                    
                    OnInitialized?.Invoke(this);
                });
            };
            Unity.Services.LevelPlay.LevelPlay.Init(levelPlayAppKey);
#endif
        }
        
        public void SetConsent(bool isConsent)
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            Unity.Services.LevelPlay.LevelPlay.SetConsent(isConsent);
#endif
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private void OnImpression(Unity.Services.LevelPlay.LevelPlayImpressionData data)
        {
            if (data == null || !data.Revenue.HasValue) return;
            var impression = new AdImpressionData
            {
                AdNetwork = data.AdNetwork,
                AdUnit = data.MediationAdUnitName ?? data.MediationAdUnitId,
                InstanceName = data.InstanceName,
                AdFormat = data.AdFormat,
                Revenue = data.Revenue.Value
            };
            MainThreadDispatcher.Enqueue(() => AdsEvent.RaiseImpressionDataReady(impression));
        }
#endif
    }
}