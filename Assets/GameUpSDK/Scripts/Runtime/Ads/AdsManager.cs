using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GameUp.Core;

namespace GameUp.SDK
{
    /// <summary>
    /// Kích thước Banner tiêu chuẩn. Áp dụng lúc Initialize; không thay đổi được sau init.
    /// </summary>
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

    /// <summary>
    /// Interface cho các ad network hỗ trợ cấu hình kích thước Banner.
    /// AdsManager gọi SetBannerSize trước Initialize để truyền lựa chọn từ Inspector.
    /// </summary>
    public interface IBannerSizeConfig
    {
        void SetBannerSize(BannerSize size);
    }

    /// <summary>
    /// Mediator for all ad networks. Initializes networks by OrderExecute, uses waterfall for show (first available wins).
    /// Logs ads_request, ads_available, ads_show_success, ads_show_fail to Firebase with ad_type and placement.
    /// </summary>
    public class AdsManager : MonoSingleton<AdsManager>
    {
        public enum PrimaryMediation
        {
            LevelPlay,
            AdMob
        }

        [Header("Ad behaviours (auto collected from children on Awake)")]
        [SerializeField] private List<IronSourceAds> levelPlayAdsBehaviours = new List<IronSourceAds>();
        [SerializeField] private List<AdmobAds> admobAdsBehaviours = new List<AdmobAds>();

        [Header("Banner sau Initialize")]
        [Tooltip("Chỉ có hiệu lực khi enable_banner (Remote Config) = true. enable_banner ưu tiên cao hơn.")]
        [SerializeField] private bool showBannerAfterInit = true;
        [SerializeField] private string showBannerPlacementAfterInit = "main";
        [Tooltip("Thời gian chờ (giây) sau Initialize rồi mới ShowBanner, để network kịp request/load.")]
        [SerializeField] private float showBannerDelaySeconds = 2f;
        [Tooltip("Kích thước Banner. Áp dụng khi Initialize – không thay đổi được sau init.")]
        [SerializeField] private BannerSize bannerSize = BannerSize.Large;

        private List<IAds> _ads = new List<IAds>();
        private bool _initialized;

        // Tái dùng để tránh GC allocation mỗi lần log (FirebaseUtils._LogEvents tiêu thụ đồng bộ, không giữ reference)
        private readonly Dictionary<object, object> _logParamCache = new Dictionary<object, object>(2);
        private readonly Dictionary<string, string> _afParamCache = new Dictionary<string, string>(1);

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
            CollectAdsFromChildren();
            BuildAdsList();
            Initialize();
        }

        /// <summary>
        /// Auto collect ad behaviours từ object con (không gồm chính GameObject chứa AdsManager).
        /// Giúp chuyển đổi mediation dễ dàng chỉ bằng việc đổi primaryMediation.
        /// </summary>
        private void CollectAdsFromChildren()
        {
            levelPlayAdsBehaviours.Clear();
            foreach (var a in GetComponentsInChildren<IronSourceAds>(true))
            {
                if (a.transform == transform)
                    continue;
                levelPlayAdsBehaviours.Add(a);
            }

            admobAdsBehaviours.Clear();
            foreach (var a in GetComponentsInChildren<AdmobAds>(true))
            {
                if (a.transform == transform)
                    continue;
                admobAdsBehaviours.Add(a);
            }
        }

        private void OnDestroy()
        {
            AdsEvent.OnImpressionDataReady -= GameUpAnalytics.LogAdImpression;
        }

        private void Update()
        {
            MainThreadDispatcher.ProcessQueue();
        }

        private void BuildAdsList()
        {
            // Mediation chính do Scripting Define Symbols (GameUp SDK/Setup Dependencies) — ổn khi SDK cài dạng package.
#if GAMEUP_PRIMARY_MEDIATION_ADMOB
            IEnumerable<IAds> preferred = admobAdsBehaviours.Where(a => a != null).Cast<IAds>();
            IEnumerable<IAds> fallback = levelPlayAdsBehaviours.Where(a => a != null).Cast<IAds>();
#else
            IEnumerable<IAds> preferred = levelPlayAdsBehaviours.Where(a => a != null).Cast<IAds>();
            IEnumerable<IAds> fallback = admobAdsBehaviours.Where(a => a != null).Cast<IAds>();
#endif

            var list = preferred.ToList();
            if (list.Count == 0)
                list = fallback.ToList();

            _ads = list
                .OrderBy(a => a.OrderExecute)
                .ToList();
        }

        /// <summary>
        /// Register ad networks (e.g. AdmobAds, IronSourceAds). Call before Initialize.
        /// </summary>
        public void SetAds(IEnumerable<IAds> ads)
        {
            _ads = ads?.OrderBy(a => a.OrderExecute).ToList() ?? new List<IAds>();
        }

        /// <summary>
        /// Initialize all networks in OrderExecute order and subscribe to load events for logging.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                Debug.Log("[GameUp] AdsManager already initialized.");
                return;
            }

            AdsEvent.OnImpressionDataReady -= GameUpAnalytics.LogAdImpression;
            AdsEvent.OnImpressionDataReady += GameUpAnalytics.LogAdImpression;

            foreach (var ad in _ads)
            {
                try
                {
                    if (ad is IBannerSizeConfig sizeConfig)
                        sizeConfig.SetBannerSize(bannerSize);
                    ad.OnInterstitialLoaded += () => LogAdsEvent(AdsEvent.InterCompleteLoad, null, null);
                    ad.OnInterstitialLoadFailed += (error) => LogAdsEvent(AdsEvent.InterLoadFail, null, error ?? "unknown");
                    ad.OnRewardedLoaded += () => LogAdsEvent(AdsEvent.RewardCompleteLoad, null, null);
                    ad.OnRewardedLoadFailed += (error) => LogAdsEvent(AdsEvent.RewardLoadFail, null, error ?? "unknown");
                    ad.Initialize();
                }
                catch (Exception e)
                {
                    Debug.LogError("[GameUp] AdsManager Init failed for " + ad.GetType().Name + ": " + e);
                }
            }
            _initialized = true;
            SetAfterCheckGDPR();

            Debug.Log("[GameUp] AdsManager Initialize called for " + _ads.Count + " networks.");

            // Luôn RequestAll để preload (banner chỉ hiện khi gọi ShowBanner, không tự hiện nhờ SetDisplayOnLoad(false) ở LevelPlay).
            RequestAll();
            // enable_banner (Remote Config) ưu tiên cao hơn showBannerAfterInit: chỉ auto-show khi cả hai cho phép.
            if (showBannerAfterInit && AdsRules.IsBannerEnabled())
                StartCoroutine(ShowBannerAfterInitCoroutine());
        }

        private IEnumerator ShowBannerAfterInitCoroutine()
        {
            if (showBannerDelaySeconds > 0f)
                yield return new WaitForSeconds(showBannerDelaySeconds);
            ShowBanner(showBannerPlacementAfterInit);
        }

        /// <summary>
        /// Call after GDPR/consent flow. Forwards to all networks.
        /// </summary>
        public void SetAfterCheckGDPR()
        {
            foreach (var ad in _ads)
            {
                try
                {
                    ad.SetAfterCheckGDPR();
                }
                catch (Exception e)
                {
                    Debug.LogError("[GameUp] AdsManager SetAfterCheckGDPR failed for " + ad.GetType().Name + ": " + e);
                }
            }
        }

        /// <summary>
        /// Returns true if all registered networks have been initialized (no runtime check of SDK state).
        /// </summary>
        public bool CheckInitialized()
        {
            return _initialized && _ads.Count > 0;
        }

        /// <summary>
        /// Centralized logging: Firebase + AppsFlyer (when appsFlyerEventName is set). Maps 'where' to af_level for AppsFlyer.
        /// </summary>
        private void LogAdsEvent(string eventName, string paramWhere = null, string paramSource = null, string appsFlyerEventName = null)
        {
            if (!string.IsNullOrEmpty(paramWhere))
                FirebaseUtils.LogEvent(eventName, AdsEvent.ParamWhere, paramWhere);
            else if (!string.IsNullOrEmpty(paramSource))
                FirebaseUtils.LogEvent(eventName, AdsEvent.ParamSource, paramSource);
            else
                FirebaseUtils.LogEvent(eventName, null, null);

            if (!string.IsNullOrEmpty(appsFlyerEventName) && !string.IsNullOrEmpty(paramWhere))
            {
                _afParamCache[AdsEvent.ParamAfLevel] = paramWhere;
                AppsFlyerUtils.LogEvents(appsFlyerEventName, _afParamCache);
            }
        }

        /// <summary>
        /// Log ad show_complete với where + level (ad_inter_show_complete, ad_rewarded_show_complete).
        /// </summary>
        private void LogAdsEventWithLevel(string eventName, string where, int level, string appsFlyerEventName = null)
        {
            _logParamCache[AdsEvent.ParamWhere] = where ?? "";
            _logParamCache[AdsEvent.ParamLevel] = level.ToString();
            FirebaseUtils.LogEventsAPI(eventName, _logParamCache);
            if (!string.IsNullOrEmpty(appsFlyerEventName))
            {
                _afParamCache[AdsEvent.ParamAfLevel] = level.ToString();
                AppsFlyerUtils.LogEvents(appsFlyerEventName, _afParamCache);
            }
        }

        private void LogAdsEventManager(string eventName, string adType, string placement)
        {
            _logParamCache[AdsEvent.ParamAdType] = adType;
            _logParamCache[AdsEvent.ParamPlacement] = placement ?? "";
            FirebaseUtils.LogEventsAPI(eventName, _logParamCache);
        }

        // ---- Show with waterfall ----

        public void ShowBanner(string where)
        {
            if (!AdsRules.IsBannerEnabled())
            {
                Debug.Log("[GameUp] AdsManager ShowBanner: disabled by Remote Config (enable_banner).");
                return;
            }
            LogAdsEventManager(AdsEvent.AdsRequest, AdsEvent.AdTypeBanner, where);
            var network = _ads.FirstOrDefault(a => a.IsBannerAvailable());
            if (network == null)
            {
                Debug.Log("[GameUp] AdsManager ShowBanner: no network available.");
                LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeBanner, where);
                return;
            }
            LogAdsEventManager(AdsEvent.AdsAvailable, AdsEvent.AdTypeBanner, where);
            try
            {
                network.ShowBanner(where);
                LogAdsEventManager(AdsEvent.AdsShowSuccess, AdsEvent.AdTypeBanner, where);
            }
            catch (Exception e)
            {
                Debug.LogError("[GameUp] AdsManager ShowBanner: " + e);
                LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeBanner, where);
            }
        }

        public void HideBanner(string where)
        {
            var network = _ads.FirstOrDefault(a => a.IsBannerAvailable());
            network?.HideBanner(where);
        }

        /// <summary>Show Interstitial (no level check: only time capping from AdsRules).</summary>
        public void ShowInterstitial(string where, Action onSuccess = null, Action onFail = null)
        {
            ShowInterstitial(where, int.MaxValue, onSuccess, onFail);
        }

        /// <summary>Show Interstitial với level hiện tại: kiểm tra inter_start_level và inter_capping_time qua AdsRules.</summary>
        public void ShowInterstitial(string where, int currentLevel, Action onSuccess = null, Action onFail = null)
        {
            if (!AdsRules.CanShowInterstitial(currentLevel))
            {
                Debug.Log("[GameUp] AdsManager ShowInterstitial: blocked by AdsRules (level or capping).");
                onFail?.Invoke();
                return;
            }
            LogAdsEventManager(AdsEvent.AdsRequest, AdsEvent.AdTypeInterstitial, where);
            var network = _ads.FirstOrDefault(a => a.IsInterstitialAvailable());
            if (network == null)
            {
                Debug.Log("[GameUp] AdsManager ShowInterstitial: no network available.");
                LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeInterstitial, where);
                onFail?.Invoke();
                return;
            }
            LogAdsEventManager(AdsEvent.AdsAvailable, AdsEvent.AdTypeInterstitial, where);
            try
            {
                LogAdsEvent(AdsEvent.InterShow, where, null, AdsEvent.AfInterShow);
                Action wrappedSuccess = () =>
                {
                    AdsRules.RecordInterstitialShown();
                    LogAdsEventWithLevel(AdsEvent.InterShowComplete, where, currentLevel, AdsEvent.AfInterDisplayed);
                    onSuccess?.Invoke();
                };
                network.ShowInterstitial(where, wrappedSuccess, onFail);
            }
            catch (Exception e)
            {
                Debug.LogError("[GameUp] AdsManager ShowInterstitial: " + e);
                LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeInterstitial, where);
                onFail?.Invoke();
            }
        }

        /// <summary>Show Rewarded Video (level = 0 nếu không truyền).</summary>
        public void ShowRewardedVideo(string where, Action onSuccess = null, Action onFail = null)
        {
            ShowRewardedVideo(where, 0, onSuccess, onFail);
        }

        /// <summary>Show Rewarded Video với level hiện tại (log ad_rewarded_show_complete kèm param level).</summary>
        public void ShowRewardedVideo(string where, int currentLevel, Action onSuccess = null, Action onFail = null)
        {
            LogAdsEventManager(AdsEvent.AdsRequest, AdsEvent.AdTypeRewardedVideo, where);
            var network = _ads.FirstOrDefault(a => a.IsRewardedVideoAvailable());
            if (network == null)
            {
                Debug.Log("[GameUp] AdsManager ShowRewardedVideo: no network available.");
                LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeRewardedVideo, where);
                onFail?.Invoke();
                return;
            }
            LogAdsEventManager(AdsEvent.AdsAvailable, AdsEvent.AdTypeRewardedVideo, where);
            try
            {
                LogAdsEvent(AdsEvent.RewardShow, where, null, AdsEvent.AfRewardShow);
                Action wrappedSuccess = () =>
                {
                    LogAdsEventWithLevel(AdsEvent.RewardShowComplete, where, currentLevel, AdsEvent.AfRewardDisplayed);
                    onSuccess?.Invoke();
                };
                // onFail khi không có network, display failed, hoặc user thoát quảng cáo không xem hết (không nhận reward)
                Action wrappedFail = () =>
                {
                    LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeRewardedVideo, where);
                    onFail?.Invoke();
                };
                network.ShowRewardedVideo(where, wrappedSuccess, wrappedFail);
            }
            catch (Exception e)
            {
                Debug.LogError("[GameUp] AdsManager ShowRewardedVideo: " + e);
                LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeRewardedVideo, where);
                onFail?.Invoke();
            }
        }

        public void ShowAppOpenAds(string where, Action onSuccess = null, Action onFail = null)
        {
            LogAdsEventManager(AdsEvent.AdsRequest, AdsEvent.AdTypeAppOpen, where);
            var network = _ads.FirstOrDefault(a => a.IsAppOpenAdsAvailable());
            if (network == null)
            {
                Debug.Log("[GameUp] AdsManager ShowAppOpenAds: no network available.");
                LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeAppOpen, where);
                onFail?.Invoke();
                return;
            }
            LogAdsEventManager(AdsEvent.AdsAvailable, AdsEvent.AdTypeAppOpen, where);
            try
            {
                network.ShowAppOpenAds(where,
                    () =>
                    {
                        LogAdsEventManager(AdsEvent.AdsShowSuccess, AdsEvent.AdTypeAppOpen, where);
                        onSuccess?.Invoke();
                    },
                    () =>
                    {
                        LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeAppOpen, where);
                        onFail?.Invoke();
                    });
            }
            catch (Exception e)
            {
                Debug.LogError("[GameUp] AdsManager ShowAppOpenAds: " + e);
                LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeAppOpen, where);
                onFail?.Invoke();
            }
        }

        /// <summary>
        /// Reset inter capping timer về 0, cho phép inter hiển thị ngay lập tức. Chỉ dùng khi test.
        /// </summary>
        public void ResetInterstitialCappingForTest()
        {
            AdsRules.ResetInterstitialCappingForTest();
        }

        /// <summary>
        /// Request/load all ad formats on all networks (e.g. after init or after consent).
        /// Logs InterStartLoad / RewardStartLoad before each request.
        /// </summary>
        public void RequestAll()
        {
            foreach (var ad in _ads)
            {
                try
                {
                    ad.RequestBanner();
                    LogAdsEvent(AdsEvent.InterStartLoad, null, null);
                    ad.RequestInterstitial();
                    LogAdsEvent(AdsEvent.RewardStartLoad, null, null);
                    ad.RequestRewardedVideo();
                    ad.RequestAppOpenAds();
                }
                catch (Exception e)
                {
                    Debug.LogError("[GameUp] AdsManager RequestAll failed for " + ad.GetType().Name + ": " + e);
                }
            }
        }
    }
}
