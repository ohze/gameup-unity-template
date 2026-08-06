using GameUp.Core;
using System;
using UnityEngine;

namespace GameUp.SDK
{
    public class IronSourceNetwork : MonoBehaviour, IAdNetwork
    {
        [Tooltip("Để trống = dùng asset GameUpAdsConfig chung của project (Resources/GameUpSDK/GameUpAdsConfig).")]
        [SerializeField] private GameUpAdsConfig configOverride;

        // --- LEGACY v1: dữ liệu cũ nằm trong prefab, chỉ còn dùng cho migrate ---
        [HideInInspector] [SerializeField] private string levelPlayAppKey;
        [HideInInspector] [SerializeField] private AdUnitConfig interstitialConfig;
        [HideInInspector] [SerializeField] private AdUnitConfig rewardedConfig;
        [HideInInspector] [SerializeField] private AdUnitConfig bannerConfig;

        public Action<IAdNetwork> OnInitialized { get; set; }
        public MediationProvider MediationProvider { get; set; } = MediationProvider.IronSource;
        public bool IsInitialized { get; private set; }
        public IInterstitialAd InterstitialAd { get; private set; }
        public IRewardedAd RewardedAd { get; private set; }
        public IAppOpenAd AppOpenAd { get; private set; }
        public IBannerAd BannerAd { get; private set; }

        public INativeFullScreenAd NativeFullScreenAd { get; private set; }

        public IronSourceAdsSettings Settings => GameUpAdsConfig.Resolve(configOverride)?.ironSource;

        public void Initialize()
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            if (IsInitialized) return;

            var settings = Settings;
            if (settings == null)
            {
                GULogger.Error("GameUp", "IronSourceNetwork: thiếu GameUpAdsConfig, bỏ qua init.");
                return;
            }
            if (string.IsNullOrEmpty(settings.appKey))
            {
                GULogger.Error("GameUp", "IronSourceNetwork: chưa điền appKey, bỏ qua init.");
                return;
            }

            // Trước đây chỉ đăng ký OnInitSuccess: init hỏng thì im lặng hoàn toàn — không log,
            // không retry, và OnInitialized không bao giờ bắn nên AdsManager cứ tưởng đang chờ.
            Unity.Services.LevelPlay.LevelPlay.OnInitFailed -= OnLevelPlayInitFailed;
            Unity.Services.LevelPlay.LevelPlay.OnInitFailed += OnLevelPlayInitFailed;

            Unity.Services.LevelPlay.LevelPlay.OnInitSuccess += (config) =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    IsInitialized = true;
                    GULogger.Log("[GameUp] IronSourceNetwork Initialized.");

                    var units = settings.units;
                    InterstitialAd = new IronSourceInterstitialAd(units.interstitial);
                    RewardedAd = new IronSourceRewardedAd(units.rewarded);
                    BannerAd = new IronSourceBannerAd(units.banner);
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
            Unity.Services.LevelPlay.LevelPlay.Init(settings.appKey);
#endif
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private void OnLevelPlayInitFailed(Unity.Services.LevelPlay.LevelPlayInitError error)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                Unity.Services.LevelPlay.LevelPlay.OnInitFailed -= OnLevelPlayInitFailed;
                GULogger.Error("GameUp",
                    $"IronSourceNetwork init THẤT BẠI: {error} — mạng này sẽ không phục vụ ad nào " +
                    "trong phiên. AdsManager tự rơi xuống mạng kế tiếp trong mediationPriority.");
            });
        }
#endif

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

#if UNITY_EDITOR
        /// <summary>Xuất dữ liệu cũ trong prefab ra dạng settings mới (chỉ dùng cho công cụ migrate).</summary>
        public IronSourceAdsSettings ExportLegacySettings()
        {
            return new IronSourceAdsSettings
            {
                appKey = levelPlayAppKey,
                units = new AdUnitConfigSet
                {
                    banner = bannerConfig?.CloneMigrated() ?? new AdUnitConfig(),
                    interstitial = interstitialConfig?.CloneMigrated() ?? new AdUnitConfig(),
                    rewarded = rewardedConfig?.CloneMigrated() ?? new AdUnitConfig()
                }
            };
        }
#endif
    }
}
