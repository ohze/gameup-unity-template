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
            { MediationProvider.Admob, MediationProvider.Max, MediationProvider.IronSource };

        [HideInInspector] [SerializeField] [Range(0, 100)] private int nativeCtaClickRate = 30;

        private readonly HashSet<string> _activeBanners = new HashSet<string>();
        private readonly Dictionary<MediationProvider, IAdNetwork> _networkDict =
            new Dictionary<MediationProvider, IAdNetwork>();

        private AdsTracker _tracker;

        private readonly List<IAdCondition> _showConditions = new List<IAdCondition>();

        public static Action<string> OnBannerLoadedEvent = delegate { };

        private Action<bool> _onRemoveAllAdsChanged;

        /// <summary>App đã từng bị đưa xuống nền ít nhất một lần — tức là lần foreground kế tiếp
        /// là "quay lại app" thật, không phải cold start.</summary>
        private bool _hasBeenBackgrounded;

        private bool _appOpenOnColdStart;

        /// <summary>
        /// CÓ ÍT NHẤT MỘT mạng đã sẵn sàng — không phải "tất cả đã xong".
        /// Dùng <see cref="AreAllNetworksInitialized"/> nếu cần điều kiện chặt hơn.
        /// </summary>
        public new bool IsInitialized { get; private set; }

        /// <summary>Mọi mạng trong mediationPriority đều đã init xong.</summary>
        public bool AreAllNetworksInitialized
        {
            get
            {
                foreach (var network in _networkDict.Values)
                    if (!network.IsInitialized) return false;
                return _networkDict.Count > 0;
            }
        }

        /// <summary>Bắn một lần khi mạng ĐẦU TIÊN sẵn sàng. Dùng để gate UI phụ thuộc ads.</summary>
        public event Action OnAdsInitialized;

        private readonly HashSet<IAdNetwork> _wiredNetworks = new HashSet<IAdNetwork>();
        private readonly HashSet<string> _pendingBannerShows = new HashSet<string>();

        public Dictionary<MediationProvider, IAdNetwork> Networks => _networkDict;

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
            ApplyConfig();
            SanitizeMediationPriority();
            _tracker = gameObject.AddComponent<AdsTracker>();
            IAdNetwork[] foundNetworks = GetComponentsInChildren<IAdNetwork>(true);
            foreach (var provider in mediationPriority)
            {
                var network = foundNetworks.FirstOrDefault(s => s.MediationProvider == provider);
                if (network != null)
                {
                    _networkDict.TryAdd(provider, network);
                }
            }
        }

        /// <summary>
        /// Bỏ entry None và entry trùng. mediationPriority do người dùng sửa tay trong Inspector,
        /// mà một dòng trùng đủ để _networkDict ném ArgumentException ngay Awake — chết cả SDK.
        /// </summary>
        private void SanitizeMediationPriority()
        {
            if (mediationPriority == null)
            {
                mediationPriority = new List<MediationProvider>();
                return;
            }

            var seen = new HashSet<MediationProvider>();
            var cleaned = new List<MediationProvider>(mediationPriority.Count);
            foreach (var provider in mediationPriority)
            {
                if (provider == MediationProvider.None) continue;
                if (seen.Add(provider)) cleaned.Add(provider);
            }

            if (cleaned.Count != mediationPriority.Count)
            {
                GULogger.Warning("GameUp",
                    $"mediationPriority có entry None/trùng — đã dọn còn: {string.Join(", ", cleaned)}");
            }
            mediationPriority = cleaned;
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
            _appOpenOnColdStart = config.appOpenOnColdStart;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) _hasBeenBackgrounded = true;
        }

        private void Start()
        {
            _onRemoveAllAdsChanged = OnRemoveAllAdsValueChanged;
            RemoveAdsSetting.Instance.IsRemoveAllAds.OnValueChange.AddListener(_onRemoveAllAdsChanged);

            // Thứ tự bắt buộc: ATT (iOS) → UMP → SetConsent → Initialize networks.
            // MAX (MaxSdk.SetHasUserConsent) và LevelPlay (LevelPlay.SetConsent) yêu cầu consent được set
            // TRƯỚC khi init SDK; init trước rồi mới set consent sẽ mất tín hiệu personalized ads.
            PrivacyManager.Instance.BeginPrivacyFlow(OnPrivacyFlowCompleted);
        }

        private void OnPrivacyFlowCompleted(PrivacyResult privacy)
        {
            // TrackingAllowed chỉ ảnh hưởng CHẤT LƯỢNG ad (personalized hay không) — luôn truyền xuống,
            // kể cả khi bên dưới quyết định không init, để lần init sau đã có sẵn tín hiệu đúng.
            SetConsent(privacy.TrackingAllowed);

            if (!privacy.CanRequestAds)
            {
                // Đây là trường hợp HIẾM: user từ chối cả mức consent tối thiểu. Chuỗi TCF do UMP ghi ra
                // ràng buộc cả MAX/LevelPlay nên không mạng nào được miễn — chặn toàn bộ, không init.
                GULogger.Warning("GameUp",
                    "UMP: CanRequestAds=false — không init mạng quảng cáo nào. " +
                    "Cho user đổi lựa chọn qua PrivacyManager.Instance.ShowPrivacyOptionsForm(), " +
                    "rồi gọi AdsManager.Instance.RetryInitializeAfterConsent().");
                return;
            }

            MainThreadDispatcher.Enqueue(InitializeAll);
            NativeAdConfigBridge.SetGlobalCtaClickRate(nativeCtaClickRate);
        }

        /// <summary>
        /// Init lại sau khi user cấp thêm consent ở privacy options form.
        /// Không làm gì nếu UMP vẫn chưa cho phép request.
        /// </summary>
        public void RetryInitializeAfterConsent()
        {
            var privacy = PrivacyManager.Instance.Result;
            if (!privacy.CanRequestAds)
            {
                GULogger.Log("GameUp", "RetryInitializeAfterConsent: UMP vẫn trả CanRequestAds=false, bỏ qua.");
                return;
            }

            SetConsent(privacy.TrackingAllowed);
            MainThreadDispatcher.Enqueue(InitializeAll);
            NativeAdConfigBridge.SetGlobalCtaClickRate(nativeCtaClickRate);
        }

        private void OnDestroy()
        {
            if (_onRemoveAllAdsChanged != null)
                RemoveAdsSetting.Instance.IsRemoveAllAds.OnValueChange.RemoveListener(_onRemoveAllAdsChanged);
            AdsEvent.OnImpressionDataReady -= GameUpAnalytics.LogAdImpression;
            AdsEvent.OnBannerSwap -= OnBannerSwapped;

            // Các format object sống theo network chứ không theo AdsManager, nên handler còn bám lại
            // sẽ trỏ vào instance đã Destroy ở lần Play kế tiếp (khi tắt Domain Reload).
            foreach (var network in _wiredNetworks) UnwireCappingEvents(network);
            _wiredNetworks.Clear();
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
                        // -= trước +=: InitializeAll có thể chạy lại (RetryInitializeAfterConsent)
                        // trong lúc network chưa init xong.
                        network.OnInitialized -= OnInitializedNetwork;
                        network.OnInitialized += OnInitializedNetwork;
                        network.Initialize();
                    }
                }
            }
        }

        private void OnInitializedNetwork(IAdNetwork network)
        {
            if (!_wiredNetworks.Add(network)) return;

            _tracker.SubscribeToNetwork(network);
            WireUpCappingEvents(network);
            if (network.BannerAd != null)
            {
                network.BannerAd.OnAdLoaded += OnBannerLoaded;
            }

            bool first = !IsInitialized;
            IsInitialized = true;

            if (first)
            {
                FlushPendingBannerShows();
                OnAdsInitialized?.Invoke();
            }
        }

        /// <summary>
        /// Phát lại các lệnh ShowBanner được gọi trước khi có mạng nào sẵn sàng.
        /// CHỈ áp dụng cho banner: banner là UI thường trực nên hiện muộn vài giây vẫn đúng ý,
        /// còn interstitial/AppOpen phát lại sẽ bật lên lạc ngữ cảnh — với chúng, onFail ngay
        /// lúc gọi mới là hành vi đúng.
        /// </summary>
        private void FlushPendingBannerShows()
        {
            if (_pendingBannerShows.Count == 0) return;

            var pending = new List<string>(_pendingBannerShows);
            _pendingBannerShows.Clear();
            foreach (var where in pending)
            {
                GULogger.Log("GameUp", $"Phát lại ShowBanner đã xếp hàng trước khi init: {where}");
                ShowBanner(where);
            }
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

        // Toàn bộ handler dưới đây là METHOD GROUP chứ không phải lambda inline: lambda không có
        // tham chiếu ổn định nên không bao giờ gỡ được, và khi tắt Domain Reload (Enter Play Mode
        // Options) chúng chồng lên nhau qua từng lần Play — một lần đóng ad chạy N lần ResumeAllCapping.
        private void OnFullscreenDisplayed(string where)
        {
            AdCappingManager.Instance.PauseAllCapping();
            TemporarilyHideBanners();
        }

        // Display lỗi = ad KHÔNG lên màn hình, nên phải nhả pause y như lúc ad đóng.
        // Trước đây chỉ OnAdClosed mới Resume, mà display lỗi thì OnAdClosed không bao giờ bắn
        // → _pauseRequests kẹt > 0 vĩnh viễn, IsAnyAdShowing luôn true và mọi Interstitial/AppOpen
        // sau đó bị chặn hết phiên. ResumeAllCapping đã kẹp sàn 0 nên gọi thừa vẫn an toàn.
        private void OnFullscreenDisplayFailed(string where, string error)
        {
            AdCappingManager.Instance.ResumeAllCapping();
            RestoreBanners();
        }

        private void HandleFullscreenClosed(AdUnitType adType)
        {
            AdCappingManager.Instance.ResumeAllCapping();
            AdCappingManager.Instance.ResetCapping(adType);
            RestoreBanners();
            AdHistoryTracker.MarkAdClosed(adType);
        }

        private void OnInterstitialClosed(string where) => HandleFullscreenClosed(AdUnitType.Interstitial);
        private void OnRewardedClosed(string where) => HandleFullscreenClosed(AdUnitType.RewardedVideo);
        private void OnAppOpenClosed(string where) => HandleFullscreenClosed(AdUnitType.AppOpen);
        private void OnNativeFullscreenClosed(string where) => HandleFullscreenClosed(AdUnitType.NativeAd);

        private void WireUpCappingEvents(IAdNetwork network)
        {
            if (network.InterstitialAd != null)
            {
                network.InterstitialAd.OnAdDisplayed += OnFullscreenDisplayed;
                network.InterstitialAd.OnAdDisplayFailed += OnFullscreenDisplayFailed;
                network.InterstitialAd.OnAdClosed += OnInterstitialClosed;
            }

            if (network.RewardedAd != null)
            {
                network.RewardedAd.OnAdDisplayed += OnFullscreenDisplayed;
                network.RewardedAd.OnAdDisplayFailed += OnFullscreenDisplayFailed;
                network.RewardedAd.OnAdClosed += OnRewardedClosed;
            }

            if (network.AppOpenAd != null)
            {
                network.AppOpenAd.OnAdDisplayed += OnFullscreenDisplayed;
                network.AppOpenAd.OnAdDisplayFailed += OnFullscreenDisplayFailed;
                network.AppOpenAd.OnAdClosed += OnAppOpenClosed;
            }

            if (network.NativeFullScreenAd != null)
            {
                network.NativeFullScreenAd.OnAdDisplayed += OnFullscreenDisplayed;
                network.NativeFullScreenAd.OnAdDisplayFailed += OnFullscreenDisplayFailed;
                network.NativeFullScreenAd.OnAdClosed += OnNativeFullscreenClosed;
            }
        }

        private void UnwireCappingEvents(IAdNetwork network)
        {
            if (network.InterstitialAd != null)
            {
                network.InterstitialAd.OnAdDisplayed -= OnFullscreenDisplayed;
                network.InterstitialAd.OnAdDisplayFailed -= OnFullscreenDisplayFailed;
                network.InterstitialAd.OnAdClosed -= OnInterstitialClosed;
            }

            if (network.RewardedAd != null)
            {
                network.RewardedAd.OnAdDisplayed -= OnFullscreenDisplayed;
                network.RewardedAd.OnAdDisplayFailed -= OnFullscreenDisplayFailed;
                network.RewardedAd.OnAdClosed -= OnRewardedClosed;
            }

            if (network.AppOpenAd != null)
            {
                network.AppOpenAd.OnAdDisplayed -= OnFullscreenDisplayed;
                network.AppOpenAd.OnAdDisplayFailed -= OnFullscreenDisplayFailed;
                network.AppOpenAd.OnAdClosed -= OnAppOpenClosed;
            }

            if (network.NativeFullScreenAd != null)
            {
                network.NativeFullScreenAd.OnAdDisplayed -= OnFullscreenDisplayed;
                network.NativeFullScreenAd.OnAdDisplayFailed -= OnFullscreenDisplayFailed;
                network.NativeFullScreenAd.OnAdClosed -= OnNativeFullscreenClosed;
            }

            if (network.BannerAd != null) network.BannerAd.OnAdLoaded -= OnBannerLoaded;
            network.OnInitialized -= OnInitializedNetwork;
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

            if (AdCappingManager.Instance.IsAnyAdShowing)
            {
                GULogger.Log("GameUp", "Interstitial blocked: đang có ad khác hiển thị.");
                onFail?.Invoke();
                return;
            }

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

            // Guard này trước đây chỉ nằm ở code mẫu (Example.OnApplicationPause), nên project nào tự
            // viết hook lifecycle là hở: AOA chồng lên interstitial khi user quay lại từ một cú click ad
            // — vi phạm chính sách AdMob. Đưa vào SDK cho đồng bộ với ShowInterstitial.
            if (AdCappingManager.Instance.IsAnyAdShowing)
            {
                GULogger.Log("GameUp", "AppOpenAd blocked: đang có ad khác hiển thị.");
                onFail?.Invoke();
                return;
            }

            if (!_hasBeenBackgrounded && !_appOpenOnColdStart)
            {
                GULogger.Log("GameUp", "AppOpenAd blocked: cold start (bật GameUpAdsConfig.appOpenOnColdStart nếu muốn).");
                onFail?.Invoke();
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

            // Gọi trước khi mạng nào kịp init thì format object còn null, lệnh sẽ rơi vào hư không.
            // Xếp hàng để phát lại ngay khi mạng đầu tiên sẵn sàng (xem FlushPendingBannerShows).
            if (!IsInitialized)
            {
                _pendingBannerShows.Add(where);
                GULogger.Log("GameUp", $"ShowBanner('{where}') gọi trước khi init — đã xếp hàng.");
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
            // Huỷ luôn lệnh đang xếp hàng, nếu không banner vừa bị ẩn lại tự hiện khi init xong.
            _pendingBannerShows.Remove(where);
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
                network.Value.NativeFullScreenAd?.Hide();
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