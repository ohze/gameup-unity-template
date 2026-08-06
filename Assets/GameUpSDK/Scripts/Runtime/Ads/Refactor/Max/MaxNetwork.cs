using System;
using GameUp.Core;
using UnityEngine;

namespace GameUp.SDK
{
    public class MaxNetwork : MonoBehaviour, IAdNetwork
    {
        [Tooltip("Để trống = dùng asset GameUpAdsConfig chung của project (Resources/GameUpSDK/GameUpAdsConfig).")]
        [SerializeField] private GameUpAdsConfig configOverride;

        // --- LEGACY v1: dữ liệu cũ nằm trong prefab, chỉ còn dùng cho migrate ---
        [HideInInspector] [SerializeField] private string sdkKey;
        [HideInInspector] [SerializeField] private bool showMediationDebugger;
        [HideInInspector] [SerializeField] private AdUnitConfig rewardedConfig;
        [HideInInspector] [SerializeField] private AdUnitConfig interstitialConfig;
        [HideInInspector] [SerializeField] private AdUnitConfig bannerConfig;
        [HideInInspector] [SerializeField] private AdUnitConfig appOpenAdConfig;
        [HideInInspector] [SerializeField] private AdUnitConfig nativeAdConfig;

        public Action<IAdNetwork> OnInitialized { get; set; }
        public MediationProvider MediationProvider { get; set; } = MediationProvider.Max;
        public bool IsInitialized { get; private set; }

        // Expose Providers
        public IRewardedAd RewardedAd { get; private set; }
        public IInterstitialAd InterstitialAd { get; private set; }
        public IBannerAd BannerAd { get; private set; }
        public IAppOpenAd AppOpenAd { get; private set; }

        public INativeFullScreenAd NativeFullScreenAd { get; private set; }

        public MaxAdsSettings Settings => GameUpAdsConfig.Resolve(configOverride)?.max;

        public void Initialize()
        {
            if (IsInitialized) return;

#if MAXSDK_DEPENDENCIES_INSTALLED
            var settings = Settings;
            if (settings == null)
            {
                GULogger.Error("GameUp", "MaxNetwork: thiếu GameUpAdsConfig, bỏ qua init.");
                return;
            }

            // SetSdkKey đã deprecated (khuyến nghị đặt key trong AppLovin Integration Manager);
            // giữ lại để tương thích cấu hình key qua GameUpAdsConfig.
#pragma warning disable 0618
            if (!string.IsNullOrEmpty(settings.sdkKey)) MaxSdk.SetSdkKey(settings.sdkKey);
#pragma warning restore 0618

            MaxSdkCallbacks.OnSdkInitializedEvent += (config) =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    IsInitialized = true;
                    if (settings.showMediationDebugger)
                    {
                        MaxSdk.ShowMediationDebugger();
                    }

                    var units = settings.units;
                    AppOpenAd = new MaxAppOpenAd(units.appOpen);
                    RewardedAd = new MaxRewardedAd(units.rewarded);
                    InterstitialAd = new MaxInterstitialAd(units.interstitial);
                    BannerAd = new MaxBannerAd(units.banner);
                    NativeFullScreenAd = new DummyNativeFullscreenAd();

                    AppOpenAd.LoadAll();
                    NativeFullScreenAd.LoadAll();
                    BannerAd.LoadAll();

                    InterstitialAd.LoadAll();
                    RewardedAd.LoadAll();

                    OnInitialized?.Invoke(this);
                });
            };

            MaxSdk.InitializeSdk();
#endif
        }

        public void SetConsent(bool isConsent)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            MaxSdk.SetHasUserConsent(isConsent);
            MaxSdk.SetDoNotSell(!isConsent);
#endif
        }

#if UNITY_EDITOR
        /// <summary>Xuất dữ liệu cũ trong prefab ra dạng settings mới (chỉ dùng cho công cụ migrate).</summary>
        public MaxAdsSettings ExportLegacySettings()
        {
            return new MaxAdsSettings
            {
                sdkKey = sdkKey,
                showMediationDebugger = showMediationDebugger,
                units = new AdUnitConfigSet
                {
                    banner = bannerConfig?.CloneMigrated() ?? new AdUnitConfig(),
                    interstitial = interstitialConfig?.CloneMigrated() ?? new AdUnitConfig(),
                    rewarded = rewardedConfig?.CloneMigrated() ?? new AdUnitConfig(),
                    // v1: setup window ghi nhầm vào "appOpenConfig" trong khi field thật là "appOpenAdConfig".
                    appOpen = appOpenAdConfig?.CloneMigrated() ?? new AdUnitConfig(),
                    nativeAd = nativeAdConfig?.CloneMigrated() ?? new AdUnitConfig()
                }
            };
        }
#endif
    }
}
