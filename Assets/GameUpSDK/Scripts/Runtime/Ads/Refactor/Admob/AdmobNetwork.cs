using GameUp.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameUp.SDK
{
    public class AdmobNetwork : MonoBehaviour, IAdNetwork
    {
        [SerializeField] private List<string> testDevices;
        [SerializeField] private bool showMediationInspector;

        [Header("Ad Unit Configs")]
        public AdUnitConfig interstitialConfig;
        public AdUnitConfig rewardedConfig;
        public AdUnitConfig appOpenConfig;
        public AdUnitConfig bannerConfig;
        public AdUnitConfig nativeAdConfig;

        public bool IsInitialized { get; private set; }

        public Action<IAdNetwork> OnInitialized { get; set; }
        
        public MediationProvider MediationProvider { get; set; } = MediationProvider.Admob;
        public IInterstitialAd InterstitialAd { get; private set; }
        public IRewardedAd RewardedAd { get; private set; }
        public IAppOpenAd AppOpenAd { get; private set; }
        public IBannerAd BannerAd { get; private set; }
        
        public INativeFullScreenAd NativeFullScreenAd { get; private set; }

        public void Initialize()
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            if (IsInitialized) return;
            var timeInit = Time.realtimeSinceStartup;
            GULogger.Error("GameUp", $"Initializing Admob Network: {Time.realtimeSinceStartup}");
            GoogleMobileAds.Api.RequestConfiguration config = new GoogleMobileAds.Api.RequestConfiguration { TestDeviceIds = testDevices };
            GoogleMobileAds.Api.MobileAds.SetRequestConfiguration(config);
            
            GoogleMobileAds.Api.MobileAds.Initialize(initStatus =>
            {
                GULogger.Error("GameUp", $"Initialized Admob Network: {Time.realtimeSinceStartup} - Total time initialized: {Time.realtimeSinceStartup - timeInit}");
                var adapterStatusMap = initStatus.getAdapterStatusMap();
                foreach (var adapter in adapterStatusMap)
                {
                    string name = adapter.Key;
                    var status = adapter.Value;
                    
                    // In ra log: Tên mạng - Trạng thái - Thời gian trễ - Mô tả lỗi (nếu có)
                    GULogger.Log($"[AdMob Init] adapter: {name} | status: {status.InitializationState} | Độ trễ: {status.Latency}ms | Phản hồi: {status.Description}");
                }
                
                GoogleMobileAds.Common.MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    IsInitialized = true;
                    GULogger.Log("[GameUp] AdmobNetwork Initialized.");

                    InterstitialAd = new AdmobInterstitialAd(interstitialConfig);
                    RewardedAd = new AdmobRewardedAd(rewardedConfig);
                    BannerAd = new AdmobBannerDispatcher(bannerConfig);
                    AppOpenAd = new AdmobAppOpenAd(appOpenConfig);
                    NativeFullScreenAd = new AdmobNativeFullscreenAd(nativeAdConfig);
                    
                    AppOpenAd.LoadAll();
                    NativeFullScreenAd.LoadAll();
                    BannerAd.LoadAll();

                    InterstitialAd.LoadAll();
                    RewardedAd.LoadAll();
                    
                    OnInitialized?.Invoke(this);

                    if (showMediationInspector)
                    {
                        GoogleMobileAds.Api.MobileAds.OpenAdInspector(error =>
                        {
                            if (error != null)
                                GULogger.Error("GameUp", $"AdmobNetwork OpenAdInspector: {error.GetMessage()}");
                        });
                    }
                });
            });
#endif
        }
        
        public void SetConsent(bool isConsent) { }
    }
}