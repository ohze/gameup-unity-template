using GameUp.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameUp.SDK
{
    public class AdmobNetwork : MonoBehaviour, IAdNetwork
    {
        [Tooltip("Để trống = dùng asset GameUpAdsConfig chung của project (Resources/GameUpSDK/GameUpAdsConfig).")]
        [SerializeField] private GameUpAdsConfig configOverride;

        // --- LEGACY v1: dữ liệu cũ nằm trong prefab, chỉ còn dùng cho migrate ---
        [HideInInspector] [SerializeField] private List<string> testDevices;
        [HideInInspector] [SerializeField] private bool showMediationInspector;
        [HideInInspector] [SerializeField] private AdUnitConfig interstitialConfig;
        [HideInInspector] [SerializeField] private AdUnitConfig rewardedConfig;
        [HideInInspector] [SerializeField] private AdUnitConfig appOpenConfig;
        [HideInInspector] [SerializeField] private AdUnitConfig bannerConfig;
        [HideInInspector] [SerializeField] private AdUnitConfig nativeAdConfig;

        public bool IsInitialized { get; private set; }

        public Action<IAdNetwork> OnInitialized { get; set; }

        public MediationProvider MediationProvider { get; set; } = MediationProvider.Admob;
        public IInterstitialAd InterstitialAd { get; private set; }
        public IRewardedAd RewardedAd { get; private set; }
        public IAppOpenAd AppOpenAd { get; private set; }
        public IBannerAd BannerAd { get; private set; }

        public INativeFullScreenAd NativeFullScreenAd { get; private set; }

        public AdmobAdsSettings Settings => GameUpAdsConfig.Resolve(configOverride)?.admob;

        public void Initialize()
        {
#if ADMOB_DEPENDENCIES_INSTALLED
            if (IsInitialized) return;

            var settings = Settings;
            if (settings == null)
            {
                GULogger.Error("GameUp", "AdmobNetwork: thiếu GameUpAdsConfig, bỏ qua init.");
                return;
            }

            var timeInit = Time.realtimeSinceStartup;
            GULogger.Log("GameUp", $"Initializing Admob Network: {Time.realtimeSinceStartup}");

            // Phải đặt TRƯỚC Initialize: buộc plugin raise mọi callback trên Unity main thread,
            // nếu không callback về từ thread native và mọi thao tác UnityEngine trong handler đều rủi ro.
            GoogleMobileAds.Api.MobileAds.RaiseAdEventsOnUnityMainThread = true;

            GoogleMobileAds.Api.MobileAds.SetRequestConfiguration(BuildRequestConfiguration(settings));

            GoogleMobileAds.Api.MobileAds.Initialize(initStatus =>
            {
                GULogger.Log("GameUp", $"Initialized Admob Network: {Time.realtimeSinceStartup} - Total time initialized: {Time.realtimeSinceStartup - timeInit}");
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

                    var units = settings.units;
                    InterstitialAd = new AdmobInterstitialAd(units.interstitial);
                    RewardedAd = new AdmobRewardedAd(units.rewarded);
                    BannerAd = new AdmobBannerDispatcher(units.banner);
                    AppOpenAd = new AdmobAppOpenAd(units.appOpen);
                    NativeFullScreenAd = new AdmobNativeFullscreenAd(units.nativeAd);

                    AppOpenAd.LoadAll();
                    NativeFullScreenAd.LoadAll();
                    BannerAd.LoadAll();

                    InterstitialAd.LoadAll();
                    RewardedAd.LoadAll();

                    OnInitialized?.Invoke(this);

                    if (settings.showMediationInspector)
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

#if ADMOB_DEPENDENCIES_INSTALLED
        /// <summary>
        /// Ngoài test devices, RequestConfiguration còn mang các cờ tuân thủ: COPPA
        /// (TagForChildDirectedTreatment), GDPR (TagForUnderAgeOfConsent) và mức nội dung tối đa.
        /// Unspecified = không khai báo, giữ nguyên mặc định của AdMob.
        /// </summary>
        private static GoogleMobileAds.Api.RequestConfiguration BuildRequestConfiguration(AdmobAdsSettings settings)
        {
            var config = new GoogleMobileAds.Api.RequestConfiguration
            {
                TestDeviceIds = settings.testDevices
            };

            switch (settings.tagForChildDirectedTreatment)
            {
                case ChildDirectedTreatment.True:
                    config.TagForChildDirectedTreatment = GoogleMobileAds.Api.TagForChildDirectedTreatment.True;
                    break;
                case ChildDirectedTreatment.False:
                    config.TagForChildDirectedTreatment = GoogleMobileAds.Api.TagForChildDirectedTreatment.False;
                    break;
            }

            switch (settings.tagForUnderAgeOfConsent)
            {
                case UnderAgeOfConsent.True:
                    config.TagForUnderAgeOfConsent = GoogleMobileAds.Api.TagForUnderAgeOfConsent.True;
                    break;
                case UnderAgeOfConsent.False:
                    config.TagForUnderAgeOfConsent = GoogleMobileAds.Api.TagForUnderAgeOfConsent.False;
                    break;
            }

            switch (settings.maxAdContentRating)
            {
                case AdContentRating.G: config.MaxAdContentRating = GoogleMobileAds.Api.MaxAdContentRating.G; break;
                case AdContentRating.PG: config.MaxAdContentRating = GoogleMobileAds.Api.MaxAdContentRating.PG; break;
                case AdContentRating.T: config.MaxAdContentRating = GoogleMobileAds.Api.MaxAdContentRating.T; break;
                case AdContentRating.MA: config.MaxAdContentRating = GoogleMobileAds.Api.MaxAdContentRating.MA; break;
            }

            return config;
        }
#endif

        public void SetConsent(bool isConsent) { }

#if UNITY_EDITOR
        /// <summary>Xuất dữ liệu cũ trong prefab ra dạng settings mới (chỉ dùng cho công cụ migrate).</summary>
        public AdmobAdsSettings ExportLegacySettings()
        {
            return new AdmobAdsSettings
            {
                testDevices = testDevices != null ? new List<string>(testDevices) : new List<string>(),
                showMediationInspector = showMediationInspector,
                units = new AdUnitConfigSet
                {
                    banner = bannerConfig?.CloneMigrated() ?? new AdUnitConfig(),
                    interstitial = interstitialConfig?.CloneMigrated() ?? new AdUnitConfig(),
                    rewarded = rewardedConfig?.CloneMigrated() ?? new AdUnitConfig(),
                    appOpen = appOpenConfig?.CloneMigrated() ?? new AdUnitConfig(),
                    nativeAd = nativeAdConfig?.CloneMigrated() ?? new AdUnitConfig()
                }
            };
        }
#endif
    }
}
