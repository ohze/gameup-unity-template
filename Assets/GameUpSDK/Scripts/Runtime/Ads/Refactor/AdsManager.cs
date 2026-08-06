using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameUp.Core;

namespace GameUp.SDK
{
    public enum BannerSize
    {
        /// <summary>320 × 50 – kích thước nhỏ nhất, phổ biến nhất.</summary>
        Banner,

        /// <summary>320 × 90 – lớn hơn BANNER, fill rate tốt. Mặc định.</summary>
        Large,

        /// <summary>
        /// Chiều rộng toàn màn hình, chiều cao tự điều chỉnh theo màn hình.
        /// Fill rate cao nhất – được IronSource/LevelPlay khuyến nghị.
        /// </summary>
        Adaptive,

        /// <summary>300 × 250 – Medium Rectangle (MREC), thường dùng trong content.</summary>
        MediumRectangle,

        /// <summary>728 × 90 – chỉ phù hợp trên iPad / tablet.</summary>
        Leaderboard,
    }

    public enum BannerFormatType
    {
        StandardBanner,
        NativeOverlay
    }

    [DefaultExecutionOrder(-50)]
    public class AdsManager : MonoSingleton<AdsManager>
    {
        [Tooltip("Để trống = dùng asset GameUpAdsConfig chung của project (Resources/GameUpSDK/GameUpAdsConfig).")]
        [SerializeField] private GameUpAdsConfig configOverride;

        /// <summary>
        /// Thứ tự ưu tiên mạng quảng cáo. Giá trị đọc từ GameUpAdsConfig lúc Awake;
        /// giá trị serialize ở đây chỉ là fallback khi chưa có asset config.
        /// </summary>
        [HideInInspector] public List<MediationProvider> mediationPriority = new List<MediationProvider>
            { MediationProvider.Max, MediationProvider.Admob, MediationProvider.IronSource };

        [HideInInspector] [SerializeField] [Range(0, 100)] private int nativeCtaClickRate = 30;

        private readonly HashSet<string> _activeBanners = new HashSet<string>();
        private readonly Dictionary<MediationProvider, IAdNetwork> _networkDict =
            new Dictionary<MediationProvider, IAdNetwork>();

        private AdsTracker _tracker;

        private readonly List<IAdCondition> _showConditions = new List<IAdCondition>();

        public static Action<string> OnBannerLoadedEvent = delegate { };

        private Action<bool> _onRemoveAllAdsChanged;

        public new bool IsInitialized { get; private set; }

        public Dictionary<MediationProvider, IAdNetwork> Networks => _networkDict;

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
            ApplyConfig();
            _tracker = gameObject.AddComponent<AdsTracker>();
            IAdNetwork[] foundNetworks = GetComponentsInChildren<IAdNetwork>(true);
            foreach (var provider in mediationPriority)
            {
                var network = foundNetworks.FirstOrDefault(s => s.MediationProvider == provider);
                if (network != null)
                {
                    _networkDict.Add(provider, network);
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>Chép cấu hình cũ trên prefab vào asset (chỉ dùng cho công cụ migrate).</summary>
        public void ExportLegacyInto(GameUpAdsConfig target)
        {
            if (target == null) return;
            if (mediationPriority != null && mediationPriority.Count > 0)
                target.mediationPriority = new List<MediationProvider>(mediationPriority);
            target.nativeCtaClickRate = nativeCtaClickRate;
        }
#endif

        private void ApplyConfig()
        {
            var config = GameUpAdsConfig.Resolve(configOverride);
            if (config == null) return;

            if (config.mediationPriority != null && config.mediationPriority.Count > 0)
                mediationPriority = new List<MediationProvider>(config.mediationPriority);

            nativeCtaClickRate = config.nativeCtaClickRate;
        }

        private void Start()
        {
            _onRemoveAllAdsChanged = OnRemoveAllAdsValueChanged;
            RemoveAdsSetting.Instance.IsRemoveAllAds.OnValueChange.AddListener(_onRemoveAllAdsChanged);

            // Thứ tự bắt buộc: ATT (iOS) → UMP → SetConsent → Initialize networks.
            // MAX (MaxSdk.SetHasUserConsent) và LevelPlay (LevelPlay.SetConsent) yêu cầu consent được set
            // TRƯỚC khi init SDK; init trước rồi mới set consent sẽ mất tín hiệu personalized ads.
            PrivacyManager.Instance.BeginPrivacyFlow(grantConsent =>
            {
                SetConsent(grantConsent);
                MainThreadDispatcher.Enqueue(InitializeAll);
                NativeAdConfigBridge.SetGlobalCtaClickRate(nativeCtaClickRate);
            });
        }

        private void OnDestroy()
        {
            if (_onRemoveAllAdsChanged != null)
                RemoveAdsSetting.Instance.IsRemoveAllAds.OnValueChange.RemoveListener(_onRemoveAllAdsChanged);
            AdsEvent.OnImpressionDataReady -= GameUpAnalytics.LogAdImpression;
            AdsEvent.OnBannerSwap -= OnBannerSwapped;
        }

        /// <summary>Remove Ads (IAP) của template: chặn toàn bộ ads trừ Rewarded.</summary>
        private static bool IsRemoveAllAdsActive() => RemoveAdsSetting.Instance.IsRemoveAllAds.Value;

        private static bool IsInterstitialRemoved() =>
            RemoveAdsSetting.Instance.IsRemoveInter.Value || IsRemoveAllAdsActive();

        private void OnRemoveAllAdsValueChanged(bool removeAll)
        {
            if (!removeAll) return;
            foreach (var placement in new List<string>(_activeBanners))
                HideBanner(placement);
        }

        private void Update()
        {
            MainThreadDispatcher.ProcessQueue();
        }

        private void InitializeAll()
        {
            AdsEvent.OnImpressionDataReady -= GameUpAnalytics.LogAdImpression;
            AdsEvent.OnImpressionDataReady += GameUpAnalytics.LogAdImpression;

            AdsEvent.OnBannerSwap -= OnBannerSwapped;
            AdsEvent.OnBannerSwap += OnBannerSwapped;

            foreach (var provider in mediationPriority)
            {
                if (provider == MediationProvider.None) continue;
                if (_networkDict.TryGetValue(provider, out var network))
                {
                    if (!network.IsInitialized)
                    {
                        network.OnInitialized += OnInitializedNetwork;
                        network.Initialize();
                    }
                }
            }
        }

        private void OnInitializedNetwork(IAdNetwork network)
        {
            _tracker.SubscribeToNetwork(network);
            WireUpCappingEvents(network);
            if (network.BannerAd != null)
            {
                network.BannerAd.OnAdLoaded += OnBannerLoaded;
            }
            IsInitialized = true;
        }

        private void OnBannerSwapped(string last, string current)
        {
            _activeBanners.Remove(last);
            if (!string.IsNullOrEmpty(current))
            {
                _activeBanners.Add(current);
            }
        }

        private void OnBannerLoaded(string where)
        {
            GULogger.Log($"OnBannerLoaded: {where}");
            // Banner được adapter preload ở trạng thái ẩn. Đây là cổng DUY NHẤT quyết định hiện.
            if (!IsRemoveAllAdsActive() && EvaluateConditions(AdUnitType.Banner, where, out _))
                ShowBanner(where);   // đường chuẩn: chọn network available + đánh dấu _activeBanners
            else
                HideBanner(where);

            OnBannerLoadedEvent.Invoke(where);
        }

        private void TemporarilyHideBanners()
        {
            foreach (var provider in mediationPriority)
            {
                if (provider == MediationProvider.None) continue;
                if (_networkDict.TryGetValue(provider, out var network) && network.BannerAd != null)
                {
                    foreach (var placement in _activeBanners)
                    {
                        network.BannerAd.Hide(placement);
                        GULogger.Log($"Temporarily HideBanner: {placement}");
                    }
                }
            }
        }

        private void RestoreBanners()
        {
            if (IsRemoveAllAdsActive()) return;
            foreach (var provider in mediationPriority)
            {
                if (provider == MediationProvider.None) continue;
                if (_networkDict.TryGetValue(provider, out var network) && network.BannerAd != null)
                {
                    foreach (var placement in _activeBanners)
                    {
                        network.BannerAd.Restore(placement);
                        GULogger.Log($"Temporarily RestoreBanner: {placement}");
                    }
                }
            }
        }

        public void SetConsent(bool isConsent)
        {
            foreach (var network in _networkDict.Values) network.SetConsent(isConsent);
        }

        /// <summary>Cập nhật tỉ lệ CTA của Native Ads lúc runtime (clickRate dạng 0..1, ví dụ từ Remote Config).</summary>
        public void UpdateNativeCtaClickRate(float clickRate)
        {
            nativeCtaClickRate = (int)(clickRate * 100);
            NativeAdConfigBridge.SetGlobalCtaClickRate(nativeCtaClickRate);
        }

        private void WireUpCappingEvents(IAdNetwork network)
        {
            Action<string> pauseAct = (where) =>
            {
                AdCappingManager.Instance.PauseAllCapping();
                TemporarilyHideBanners();
            };

            if (network.InterstitialAd != null)
            {
                network.InterstitialAd.OnAdDisplayed += pauseAct;
                network.InterstitialAd.OnAdClosed += (where) =>
                {
                    AdCappingManager.Instance.ResumeAllCapping();
                    AdCappingManager.Instance.ResetCapping(AdUnitType.Interstitial);
                    RestoreBanners();
                    AdHistoryTracker.MarkAdClosed(AdUnitType.Interstitial);
                };
            }

            if (network.RewardedAd != null)
            {
                network.RewardedAd.OnAdDisplayed += pauseAct;
                network.RewardedAd.OnAdClosed += (where) =>
                {
                    AdCappingManager.Instance.ResumeAllCapping();
                    AdCappingManager.Instance.ResetCapping(AdUnitType.RewardedVideo);
                    RestoreBanners();
                    AdHistoryTracker.MarkAdClosed(AdUnitType.RewardedVideo);
                };
            }

            if (network.AppOpenAd != null)
            {
                network.AppOpenAd.OnAdDisplayed += pauseAct;
                network.AppOpenAd.OnAdClosed += (where) =>
                {
                    AdCappingManager.Instance.ResumeAllCapping();
                    AdCappingManager.Instance.ResetCapping(AdUnitType.AppOpen);
                    RestoreBanners();
                    AdHistoryTracker.MarkAdClosed(AdUnitType.AppOpen);
                };
            }

            if (network.NativeFullScreenAd != null)
            {
                network.NativeFullScreenAd.OnAdDisplayed += pauseAct;
                network.NativeFullScreenAd.OnAdClosed += (where) =>
                {
                    AdCappingManager.Instance.ResumeAllCapping();
                    AdCappingManager.Instance.ResetCapping(AdUnitType.NativeAd);
                    RestoreBanners();
                    AdHistoryTracker.MarkAdClosed(AdUnitType.NativeAd);
                };
            }
        }

        public void AddCondition(IAdCondition condition)
        {
            if (_showConditions.Contains(condition)) return;
            _showConditions.Add(condition);
            // Điều kiện mới (vd HideBannerFromRemote) có thể cấm banner đang hiển thị
            // → ép đánh giá lại ngay, không chờ tới lần load kế tiếp.
            RefreshBannerVisibility();
        }

        /// <summary>
        /// Đánh giá lại toàn bộ banner đang active theo _showConditions hiện tại;
        /// ẩn placement nào không còn thoả điều kiện. Gọi khi điều kiện/Remote Config thay đổi.
        /// </summary>
        public void RefreshBannerVisibility()
        {
            if (_activeBanners.Count == 0) return;
            foreach (var placement in new List<string>(_activeBanners))
            {
                if (IsRemoveAllAdsActive() || !EvaluateConditions(AdUnitType.Banner, placement, out _))
                    HideBanner(placement);
            }
        }

        private bool EvaluateConditions(AdUnitType adType, string where, out string blockReason)
        {
            foreach (var condition in _showConditions)
            {
                if (!condition.CanShow(adType, where, out blockReason))
                {
                    return false;
                }
            }

            blockReason = string.Empty;
            return true;
        }

        public IAdNetwork GetAvailableProvider(AdUnitType adType, string where)
        {
            foreach (var provider in mediationPriority)
            {
                if (provider == MediationProvider.None) continue;

                if (_networkDict.TryGetValue(provider, out var network))
                {
                    bool isAvailable = adType switch
                    {
                        AdUnitType.RewardedVideo => network.RewardedAd != null && network.RewardedAd.IsAvailable(where),
                        AdUnitType.Interstitial => network.InterstitialAd != null &&
                                                   network.InterstitialAd.IsAvailable(where),
                        AdUnitType.AppOpen => network.AppOpenAd != null && network.AppOpenAd.IsAvailable(where),
                        AdUnitType.Banner => network.BannerAd != null && network.BannerAd.IsAvailable(where),
                        AdUnitType.NativeAd => network.NativeFullScreenAd != null &&
                                               network.NativeFullScreenAd.IsAvailable(where),
                        _ => false
                    };
                    if (isAvailable) return network;
                }
            }

            return null;
        }

        public bool IsRewardedVideoAvailable(string where = null) =>
            GetAvailableProvider(AdUnitType.RewardedVideo, where) != null;

        public void ShowRewardedVideo(string where, Action onSuccess = null, Action onFail = null) =>
            ShowRewardedVideo(where, 0, onSuccess, onFail);

        public void ShowRewardedVideo(string where, int currentLevel, Action onSuccess = null, Action onFail = null)
        {
            _tracker.LogAdsEventManager(AdsEvent.AdsRequest, AdsEvent.AdTypeRewardedVideo, where);
            var network = GetAvailableProvider(AdUnitType.RewardedVideo, where);

            if (network == null)
            {
                _tracker.LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeRewardedVideo, where,
                    "no_ads_available");
                onFail?.Invoke();
                LoadAd(AdUnitType.RewardedVideo, where);
                return;
            }

            _tracker.LogAdsEventManager(AdsEvent.AdsAvailable, AdsEvent.AdTypeRewardedVideo, where);
            _tracker.RegisterPlacementLevel(where, currentLevel);
            network.RewardedAd.Show(where, onSuccess, onFail);
        }

        public bool IsInterstitialAvailable(string where = null) =>
            !IsInterstitialRemoved() && GetAvailableProvider(AdUnitType.Interstitial, where) != null;

        public void ShowInterstitial(string where, int currentLevel, Action onSuccess = null,
            Action onFail = null)
        {
            if (IsInterstitialRemoved())
            {
                GULogger.Log("GameUp", "Interstitial blocked: remove-ads is active.");
                onSuccess?.Invoke();
                return;
            }

            if (AdCappingManager.Instance.IsAnyAdShowing) return;

            if (!EvaluateConditions(AdUnitType.Interstitial, where, out var blockReason))
            {
                GULogger.Log($"[GameUp.SDK] Interstitial block rules: {blockReason}");
                onFail?.Invoke();
                return;
            }

            _tracker.LogAdsEventManager(AdsEvent.AdsRequest, AdsEvent.AdTypeInterstitial, where);
            var network = GetAvailableProvider(AdUnitType.Interstitial, where);

            if (network == null)
            {
                _tracker.LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeInterstitial, where,
                    "no_ads_available");
                onFail?.Invoke();
                LoadAd(AdUnitType.Interstitial, where);
                return;
            }

            _tracker.LogAdsEventManager(AdsEvent.AdsAvailable, AdsEvent.AdTypeInterstitial, where);
            _tracker.RegisterPlacementLevel(where, currentLevel);
            network.InterstitialAd.Show(where, onSuccess, onFail);
        }

        public bool IsAppOpenAdAvailable(string where = "default") =>
            !IsRemoveAllAdsActive() && GetAvailableProvider(AdUnitType.AppOpen, where) != null;

        public void ShowAppOpenAds(string where = "default", Action onSuccess = null, Action onFail = null)
        {
            if (IsRemoveAllAdsActive())
            {
                GULogger.Log("GameUp", "AppOpenAd blocked: remove-ads is active.");
                onSuccess?.Invoke();
                return;
            }

            if (!EvaluateConditions(AdUnitType.AppOpen, where, out var blockReason))
            {
                GULogger.Log($"[GameUp.SDK] AppOpenAd block rules: {blockReason}");
                onFail?.Invoke();
                return;
            }

            _tracker.LogAdsEventManager(AdsEvent.AdsRequest, AdsEvent.AdTypeAppOpen, where);
            var network = GetAvailableProvider(AdUnitType.AppOpen, where);

            if (network == null)
            {
                _tracker.LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeAppOpen, where, "no_ads_available");
                onFail?.Invoke();
                LoadAd(AdUnitType.AppOpen, where);
                return;
            }

            _tracker.LogAdsEventManager(AdsEvent.AdsAvailable, AdsEvent.AdTypeAppOpen, where);
            network.AppOpenAd.Show(where, onSuccess, onFail);
        }

        public bool IsBannerAvailable(string where = null) => GetAvailableProvider(AdUnitType.Banner, where) != null;

        public void ShowBanner(string where)
        {
            if (IsRemoveAllAdsActive())
            {
                GULogger.Log("GameUp", "Banner blocked: remove-ads is active.");
                return;
            }

            if (!EvaluateConditions(AdUnitType.Banner, where, out var blockReason))
            {
                GULogger.Log($"[GameUp.SDK] Banner block rules: {blockReason}");
                return;
            }

            _tracker.LogAdsEventManager(AdsEvent.AdsRequest, AdsEvent.AdTypeBanner, where);

            var network = GetAvailableProvider(AdUnitType.Banner, where);
            if (network == null)
            {
                _tracker.LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeBanner, where, "no_ads_available");
                LoadAd(AdUnitType.Banner, where);
                return;
            }

            _tracker.LogAdsEventManager(AdsEvent.AdsAvailable, AdsEvent.AdTypeBanner, where);
            _activeBanners.Add(where);
            network.BannerAd.Show(where);
        }

        public void HideBanner(string where)
        {
            _activeBanners.Remove(where);
            foreach (var network in _networkDict.Values) network.BannerAd?.Hide(where);
        }

        public bool IsNativeAdAvailable(string where = null) =>
            !IsRemoveAllAdsActive() && GetAvailableProvider(AdUnitType.NativeAd, where) != null;


        public void ShowNativeAd(string where = "default", Action onSuccess = null, Action onFail = null)
        {
            if (IsRemoveAllAdsActive())
            {
                GULogger.Log("GameUp", "NativeAd blocked: remove-ads is active.");
                onSuccess?.Invoke();
                return;
            }

            if (!EvaluateConditions(AdUnitType.NativeAd, where, out var blockReason))
            {
                GULogger.Log($"[GameUp.SDK] NativeAd block rules: {blockReason}");
                onFail?.Invoke();
                return;
            }

            _tracker.LogAdsEventManager(AdsEvent.AdsRequest, AdsEvent.AdTypeNativeAd, where);
            var network = GetAvailableProvider(AdUnitType.NativeAd, where);

            if (network == null)
            {
                _tracker.LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeNativeAd, where, "network_null");
                onFail?.Invoke();
                return;
            }

            _tracker.LogAdsEventManager(AdsEvent.AdsAvailable, AdsEvent.AdTypeNativeAd, where);
            network.NativeFullScreenAd.Show(where, onSuccess, onFail);
        }

        public void HideNativeAd(string where = "default")
        {
            foreach (var network in _networkDict)
            {
                network.Value.NativeFullScreenAd.Hide();
            }
        }

        public void LoadAd(AdUnitType adType, string where = null)
        {
            switch (adType)
            {
                case AdUnitType.Banner:
                    foreach (var network in _networkDict)
                    {
                        network.Value.BannerAd?.Load(where);
                    }

                    break;
                case AdUnitType.Interstitial:
                    foreach (var network in _networkDict)
                    {
                        network.Value.InterstitialAd?.Load(where);
                    }

                    break;
                case AdUnitType.RewardedVideo:
                    foreach (var network in _networkDict)
                    {
                        network.Value.RewardedAd?.Load(where);
                    }

                    break;
                case AdUnitType.AppOpen:
                    foreach (var network in _networkDict)
                    {
                        network.Value.AppOpenAd?.Load(where);
                    }

                    break;
                case AdUnitType.NativeAd:
                    foreach (var network in _networkDict)
                    {
                        network.Value.NativeFullScreenAd?.Load(where);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(adType), adType, null);
            }
        }
    }
}