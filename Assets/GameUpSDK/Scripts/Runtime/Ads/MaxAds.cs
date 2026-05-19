using System;
using UnityEngine;
using GameUp.Core;
using System.Collections.Generic;

namespace GameUp.SDK
{
    /// <summary>
    /// AppLovin MAX implementation of IAds. Handles Banner, Interstitial, Rewarded, and App Open.
    /// </summary>
    public class MaxAds : MonoBehaviour, IAds, IPlacementAwareAds, IAdUnitIdResolver, IConsentAwareAds
    {
        [Header("SDK Settings")]
        [Tooltip("AppLovin SDK Key. Náº¿u Ä‘Ã£ cáº¥u hÃ¬nh trong AppLovin Integration Manager thÃ¬ cÃ³ thá»ƒ bá» trá»‘ng.")]
        [SerializeField]
        private string sdkKey = "";
        
        [Header("Test Device")]
        [SerializeField]
        private List<string> testDevices = new List<string>();

        [Header("Ad Unit IDs")]
        [Tooltip("Báº­t Ä‘á»ƒ dÃ¹ng nhiá»u Ad Unit theo placement key (where). Táº¯t = dÃ¹ng 1 ID/format.")]
        [SerializeField]
        private bool useMultiAdUnitIds;

        [Tooltip("Danh sÃ¡ch mapping Android: (AdType, NameId=where, Id=ad unit id).")] [SerializeField]
        private System.Collections.Generic.List<AdUnitIdEntry> adUnitIdsAndroid =
            new System.Collections.Generic.List<AdUnitIdEntry>();

        [Tooltip("Danh sÃ¡ch mapping iOS: (AdType, NameId=where, Id=ad unit id).")] [SerializeField]
        private System.Collections.Generic.List<AdUnitIdEntry> adUnitIdsIOS =
            new System.Collections.Generic.List<AdUnitIdEntry>();

        [Header("Single IDs (Fallback)")] [SerializeField]
        private string bannerAdUnitIdAndroid;

        [SerializeField] private string bannerAdUnitIdIOS;

        [SerializeField] private string interstitialAdUnitIdAndroid;
        [SerializeField] private string interstitialAdUnitIdIOS;

        [SerializeField] private string rewardedAdUnitIdAndroid;
        [SerializeField] private string rewardedAdUnitIdIOS;

        [SerializeField] private string appOpenAdUnitIdAndroid;
        [SerializeField] private string appOpenAdUnitIdIOS;

        public int OrderExecute { get; set; }

        public event Action OnInterstitialLoaded;
        public event Action<string> OnInterstitialLoadFailed;
        public event Action OnRewardedLoaded;
        public event Action<string> OnRewardedLoadFailed;
        public event Action<string> OnBannerShown;
        public event Action<string> OnBannerShowFailed;

        private bool _initialized;
        private string _bannerPlacementForShow = "main";

        private static string Safe(string value) => string.IsNullOrEmpty(value) ? "null" : value;

        private void LogAdTrace(string phase, AdUnitType type, string unitId, string where = null, string extra = null)
        {
            var message = $"[GameUp] MaxAds {phase} | type={type} | where={Safe(where)} | unitId={Safe(unitId)}";
            if (!string.IsNullOrEmpty(extra)) message += " | " + extra;
            Debug.Log(message);
        }

        public void Initialize()
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (_initialized) return;

            if (!string.IsNullOrEmpty(sdkKey))
            {
                MaxSdk.SetSdkKey(sdkKey);
            }

            MaxSdk.SetTestDeviceAdvertisingIdentifiers(testDevices.ToArray());
            MaxSdk.InitializeSdk();
            MaxSdkCallbacks.OnSdkInitializedEvent += (MaxSdkBase.SdkConfiguration sdkConfiguration) =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    _initialized = true;
                    GULogger.Log("GameUp", "MaxAds Initialized.");

                    SetupCallbacks();

                    // Preload ads
                    RequestBanner();
                    RequestInterstitial();
                    RequestRewardedVideo();
                    RequestAppOpenAds();
                    MaxSdk.ShowMediationDebugger();
                });
            };
#else
            _initialized = true;
            GULogger.Log("GameUp", "MaxAds skipped (not mobile platform or dependency missing).");
#endif
        }

#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            
        private int _retryInterstitialAttempt = 0;
        private int _retryRewardedAttempt = 0;
        private void SetupCallbacks()
        {
            // Interstitial Callbacks
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (adUnitId, adInfo) =>
            {
                _retryInterstitialAttempt = 0;
                MainThreadDispatcher.Enqueue(() => OnInterstitialLoaded?.Invoke());
            };
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (adUnitId, errorInfo) =>
            {
                _retryInterstitialAttempt++;
                double retryDelay = Math.Pow(2, Math.Min(6, _retryInterstitialAttempt));
                
                Invoke(nameof(RequestInterstitial), (float) retryDelay);
                MainThreadDispatcher.Enqueue(() => OnInterstitialLoadFailed?.Invoke(errorInfo.Message));
            };
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent +=
                (adUnitId, adInfo) => TrackRevenue(adUnitId, adInfo, "Interstitial");

            // Rewarded Callbacks
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (adUnitId, adInfo) =>
            {
                _retryRewardedAttempt = 0;
                MainThreadDispatcher.Enqueue(() => OnRewardedLoaded?.Invoke());
            };
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (adUnitId, errorInfo) =>
            {
                _retryRewardedAttempt++;
                double retryDelay = Math.Pow(2, Math.Min(6, _retryRewardedAttempt));
                Invoke(nameof(RequestRewardedVideo), (float) retryDelay);
                MainThreadDispatcher.Enqueue(() => OnRewardedLoadFailed?.Invoke(errorInfo.Message));
            };
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent +=
                (adUnitId, adInfo) => TrackRevenue(adUnitId, adInfo, "Rewarded");

            // Banner Callbacks
            MaxSdkCallbacks.Banner.OnAdLoadedEvent += (adUnitId, adInfo) =>
                MainThreadDispatcher.Enqueue(() => OnBannerShown?.Invoke(_bannerPlacementForShow));
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += (adUnitId, errorInfo) =>
                MainThreadDispatcher.Enqueue(() => OnBannerShowFailed?.Invoke(_bannerPlacementForShow));
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent +=
                (adUnitId, adInfo) => TrackRevenue(adUnitId, adInfo, "Banner");

            // App Open Callbacks
            MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent +=
                (adUnitId, adInfo) => TrackRevenue(adUnitId, adInfo, "AppOpen");
        }

        private void TrackRevenue(string adUnitId, MaxSdkBase.AdInfo adInfo, string format)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                var data = new AdImpressionData
                {
                    AdNetwork = adInfo.NetworkName,
                    AdUnit = adUnitId,
                    InstanceName = adInfo.NetworkPlacement,
                    AdFormat = format,
                    Revenue = adInfo.Revenue
                };
                AdsEvent.RaiseImpressionDataReady(data);
            });
        }
#endif

        public void SetAfterCheckGDPR() => SetAfterCheckGDPR(true);

        public void SetAfterCheckGDPR(bool isConsent)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            GULogger.Log("GameUp", "MaxAds SetAfterCheckGDPR called. consent=" + isConsent);
            MaxSdk.SetHasUserConsent(isConsent);
            MaxSdk.SetDoNotSell(!isConsent);
#endif
        }

        // ---- REQUESTS ----
        public void RequestBanner()
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!_initialized) return;
            string unitId = ResolveUnitId(AdUnitType.Banner, null);
            if (string.IsNullOrEmpty(unitId)) return;

            MaxSdk.CreateBanner(unitId, MaxSdkBase.BannerPosition.BottomCenter);
            MaxSdk.SetBannerExtraParameter(unitId, "adaptive_banner", "true");
#endif
        }

        public void RequestCollapsibleBanner(string where,
            CollapsibleBannerPlacement placement = CollapsibleBannerPlacement.Bottom)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!_initialized) return;
            string unitId = ResolveUnitId(AdUnitType.Banner, where);
            if (string.IsNullOrEmpty(unitId)) return;

            var pos = placement == CollapsibleBannerPlacement.Top
                ? MaxSdkBase.BannerPosition.TopCenter
                : MaxSdkBase.BannerPosition.BottomCenter;
            MaxSdk.CreateBanner(unitId, pos);
#endif
        }

        public void RequestInterstitial()
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!_initialized) return;
            ExecuteForAllUnits(AdUnitType.Interstitial, (id) => MaxSdk.LoadInterstitial(id));
#endif
        }

        public void RequestRewardedVideo()
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!_initialized) return;
            ExecuteForAllUnits(AdUnitType.RewardedVideo, (id) => MaxSdk.LoadRewardedAd(id));
#endif
        }

        public void RequestAppOpenAds()
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!_initialized) return;
            ExecuteForAllUnits(AdUnitType.AppOpen, (id) => MaxSdk.LoadAppOpenAd(id));
#endif
        }

        // ---- SHOWS ----
        public void ShowBanner(string where)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            _bannerPlacementForShow = string.IsNullOrEmpty(where) ? "main" : where;
            string unitId = ResolveUnitId(AdUnitType.Banner, where);
            if (!string.IsNullOrEmpty(unitId)) MaxSdk.ShowBanner(unitId);
#endif
        }

        public void ShowCollapsibleBanner(string where,
            CollapsibleBannerPlacement placement = CollapsibleBannerPlacement.Bottom)
        {
            ShowBanner(where);
        }

        public void HideBanner(string where)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            string unitId = ResolveUnitId(AdUnitType.Banner, where);
            if (!string.IsNullOrEmpty(unitId)) MaxSdk.HideBanner(unitId);
#endif
        }

        public void ShowInterstitial(string where, Action onSuccess, Action onFail)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            string unitId = ResolveUnitId(AdUnitType.Interstitial, where);
            if (MaxSdk.IsInterstitialReady(unitId))
            {
                MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += HandleHidden;
                MaxSdk.ShowInterstitial(unitId, where);
            }
            else
            {
                onFail?.Invoke();
                MaxSdk.LoadInterstitial(unitId);
            }

            void HandleHidden(string id, MaxSdkBase.AdInfo info)
            {
                if (id != unitId) return;
                MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= HandleHidden;
                MainThreadDispatcher.Enqueue(() => onSuccess?.Invoke());
                MaxSdk.LoadInterstitial(unitId);
            }
#else
            onFail?.Invoke();
#endif
        }

        public void ShowRewardedVideo(string where, Action onSuccess, Action onFail)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            string unitId = ResolveUnitId(AdUnitType.RewardedVideo, where);
            bool rewardedEarned = false;

            if (MaxSdk.IsRewardedAdReady(unitId))
            {
                AdsRules.BeginInterstitialCappingPause();
                MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += HandleReward;
                MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += HandleHidden;
                MaxSdk.ShowRewardedAd(unitId, where);
            }
            else
            {
                onFail?.Invoke();
                MaxSdk.LoadRewardedAd(unitId);
            }

            void HandleReward(string id, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo info)
            {
                rewardedEarned = true;
                GULogger.Log("GameUp", $"Handle Rewarded Video reward: id {id} - unit id: {unitId}");
            }

            void HandleHidden(string id, MaxSdkBase.AdInfo info)
            {
                MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= HandleReward;
                MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= HandleHidden;
                GULogger.Log("GameUp", $"Handle Rewarded Video Hidden: {id}");

                MainThreadDispatcher.Enqueue(() =>
                {
                    AdsRules.EndInterstitialCappingPause();
                    if (rewardedEarned)
                    {
                        onSuccess?.Invoke();
                        GULogger.Log("GameUp", $"Rewarded Video Earned");
                    }
                    else
                    {
                        onFail?.Invoke();
                        GULogger.Log("GameUp", $"Rewarded Video Failed");
                    }
                    
                    MaxSdk.LoadRewardedAd(unitId);
                    GULogger.Log("GameUp", $"Load new Video Rewarded");
                });
            }
#else
            onFail?.Invoke();
#endif
        }

        public void ShowAppOpenAds(string where, Action onSuccess, Action onFail)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            string unitId = ResolveUnitId(AdUnitType.AppOpen, where);
            if (MaxSdk.IsAppOpenAdReady(unitId))
            {
                MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += HandleHidden;
                MaxSdk.ShowAppOpenAd(unitId, where);
            }
            else
            {
                onFail?.Invoke();
                MaxSdk.LoadAppOpenAd(unitId);
            }

            void HandleHidden(string id, MaxSdkBase.AdInfo info)
            {
                if (id != unitId) return;
                MaxSdkCallbacks.AppOpen.OnAdHiddenEvent -= HandleHidden;
                MainThreadDispatcher.Enqueue(() => onSuccess?.Invoke());
                MaxSdk.LoadAppOpenAd(unitId);
            }
#else
            onFail?.Invoke();
#endif
        }

        // ---- AVAILABILITY ----
        public bool IsBannerAvailable() => _initialized;
        public bool IsCollapsibleBannerAvailable() => _initialized;

        public bool IsInterstitialAvailable()
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            return MaxSdk.IsInterstitialReady(ResolveUnitId(AdUnitType.Interstitial, null));
#endif
            return false;
        }

        public bool IsRewardedVideoAvailable()
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            return MaxSdk.IsRewardedAdReady(ResolveUnitId(AdUnitType.RewardedVideo, null));
#endif
            return false;
        }

        public bool IsAppOpenAdsAvailable()
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            return MaxSdk.IsAppOpenAdReady(ResolveUnitId(AdUnitType.AppOpen, null));
#endif
            return false;
        }

        public bool IsBannerAvailable(string where) => !string.IsNullOrEmpty(ResolveUnitId(AdUnitType.Banner, where));

        public bool IsCollapsibleBannerAvailable(string where) =>
            !string.IsNullOrEmpty(ResolveUnitId(AdUnitType.Banner, where));

        public bool IsInterstitialAvailable(string where)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            return MaxSdk.IsInterstitialReady(ResolveUnitId(AdUnitType.Interstitial, where));
#endif
            return false;
        }

        public bool IsRewardedVideoAvailable(string where)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            return MaxSdk.IsRewardedAdReady(ResolveUnitId(AdUnitType.RewardedVideo, where));
#endif
            return false;
        }

        public bool IsAppOpenAdsAvailable(string where)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            return MaxSdk.IsAppOpenAdReady(ResolveUnitId(AdUnitType.AppOpen, where));
#endif
            return false;
        }

        // ---- RESOLVERS / HELPERS ----
        private void ExecuteForAllUnits(AdUnitType type, Action<string> action)
        {
            if (!useMultiAdUnitIds)
            {
                string singleId = GetSingleUnitId(type);
                if (!string.IsNullOrEmpty(singleId)) action(singleId);
                return;
            }

            foreach (var e in GetActiveAdUnitIds())
            {
                if (e != null && e.AdType == type && e.IsValid()) action(e.Id);
            }
        }

        private string ResolveUnitId(AdUnitType type, string where)
        {
            var normalizedWhere = string.IsNullOrWhiteSpace(where) ? null : where.Trim();
            if (useMultiAdUnitIds && !string.IsNullOrEmpty(normalizedWhere))
            {
                var activeAdUnitIds = GetActiveAdUnitIds();
                for (int i = 0; i < activeAdUnitIds.Count; i++)
                {
                    var e = activeAdUnitIds[i];
                    if (e != null && e.AdType == type && e.IsValid() && string.Equals(e.NameId?.Trim(), normalizedWhere,
                            StringComparison.OrdinalIgnoreCase))
                        return e.Id;
                }
            }

            return GetSingleUnitId(type);
        }

        private string GetSingleUnitId(AdUnitType type)
        {
            bool isAndroid = GetRuntimeAdPlatform() == RuntimeAdPlatform.Android;
            return type switch
            {
                AdUnitType.Banner => isAndroid ? bannerAdUnitIdAndroid : bannerAdUnitIdIOS,
                AdUnitType.Interstitial => isAndroid ? interstitialAdUnitIdAndroid : interstitialAdUnitIdIOS,
                AdUnitType.RewardedVideo => isAndroid ? rewardedAdUnitIdAndroid : rewardedAdUnitIdIOS,
                AdUnitType.AppOpen => isAndroid ? appOpenAdUnitIdAndroid : appOpenAdUnitIdIOS,
                _ => null
            };
        }

        private enum RuntimeAdPlatform
        {
            Android,
            IOS
        }

        private static RuntimeAdPlatform GetRuntimeAdPlatform()
        {
#if UNITY_ANDROID
            return RuntimeAdPlatform.Android;
#elif UNITY_IOS || UNITY_IPHONE
            return RuntimeAdPlatform.IOS;
#elif UNITY_EDITOR
            return UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS ? RuntimeAdPlatform.IOS : RuntimeAdPlatform.Android;
#else
            return RuntimeAdPlatform.Android;
#endif
        }

        private System.Collections.Generic.List<AdUnitIdEntry> GetActiveAdUnitIds()
        {
            bool isAndroid = GetRuntimeAdPlatform() == RuntimeAdPlatform.Android;
            var preferred = isAndroid ? adUnitIdsAndroid : adUnitIdsIOS;
            return preferred ?? new System.Collections.Generic.List<AdUnitIdEntry>();
        }

        public bool TryResolve(int intId, out AdUnitType adType, out string nameId)
        {
            adType = AdUnitType.Interstitial;
            nameId = null;
            var activeAdUnitIds = GetActiveAdUnitIds();
            if (!useMultiAdUnitIds || activeAdUnitIds == null) return false;

            for (int i = 0; i < activeAdUnitIds.Count; i++)
            {
                var e = activeAdUnitIds[i];
                if (e != null && e.intId == intId && e.IsValid())
                {
                    adType = e.AdType;
                    nameId = e.NameId;
                    return true;
                }
            }

            return false;
        }
    }
}