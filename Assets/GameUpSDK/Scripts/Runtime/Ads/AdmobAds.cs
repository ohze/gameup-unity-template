using System;
using UnityEngine;
using GameUp.Core;
using UnityEngine.Serialization;
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif



namespace GameUp.SDK
{
    /// <summary>
    /// AdMob (Google Mobile Ads) implementation of IAds. Handles Banner, Interstitial, Rewarded, and App Open.
    /// </summary>
    public class AdmobAds : MonoBehaviour, IAds, IPlacementAwareAds, IAdUnitIdResolver, IConsentAwareAds
    {
        [Header("Ad Unit IDs")]
        [Tooltip("Báº­t Ä‘á»ƒ dÃ¹ng nhiá»u Ad Unit theo placement key (where). Táº¯t = dÃ¹ng 1 ID/format nhÆ° hiá»‡n táº¡i.")]
        [SerializeField] private bool useMultiAdUnitIds;

        [Tooltip("Danh sÃ¡ch mapping Android: (AdType, NameId=where, Id=ad unit id). Chá»‰ dÃ¹ng khi useMultiAdUnitIds=true.")]
        [FormerlySerializedAs("adUnitIds")]
        [SerializeField] private System.Collections.Generic.List<AdUnitIdEntry> adUnitIdsAndroid = new System.Collections.Generic.List<AdUnitIdEntry>();

        [Tooltip("Danh sÃ¡ch mapping iOS: (AdType, NameId=where, Id=ad unit id). Chá»‰ dÃ¹ng khi useMultiAdUnitIds=true.")]
        [SerializeField] private System.Collections.Generic.List<AdUnitIdEntry> adUnitIdsIOS = new System.Collections.Generic.List<AdUnitIdEntry>();

        [Header("Single IDs (legacy / fallback)")]
        [FormerlySerializedAs("bannerAdUnitId")]
        [SerializeField] private string bannerAdUnitIdAndroid;
        [SerializeField] private string bannerAdUnitIdIOS;

        [FormerlySerializedAs("interstitialAdUnitId")]
        [SerializeField] private string interstitialAdUnitIdAndroid;
        [SerializeField] private string interstitialAdUnitIdIOS;

        [FormerlySerializedAs("rewardedAdUnitId")]
        [SerializeField] private string rewardedAdUnitIdAndroid;
        [SerializeField] private string rewardedAdUnitIdIOS;

        [FormerlySerializedAs("appOpenAdUnitId")]
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

#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
        private BannerView _bannerView;
        private string _bannerUnitIdActive;
        private bool _bannerShouldBeVisible;
        private bool _bannerLoaded;
        private bool _bannerIsCollapsible;
        private bool _bannerLoading;
        private bool _bannerRequestInProgress;
        private string _pendingBannerUnitId;
        private CollapsibleBannerPlacement _bannerCollapsiblePlacementActive = CollapsibleBannerPlacement.None;
        private CollapsibleBannerPlacement _pendingBannerCollapsiblePlacement = CollapsibleBannerPlacement.None;
        private string _bannerPlacementForShow;
        private AdPosition _bannerPositionActive = AdPosition.Bottom;

        private InterstitialAd _interstitialAd;
        private RewardedAd _rewardedAd;
        private AppOpenAd _appOpenAd;

        private readonly System.Collections.Generic.Dictionary<string, InterstitialAd> _interstitialByWhere = new System.Collections.Generic.Dictionary<string, InterstitialAd>();
        private readonly System.Collections.Generic.Dictionary<string, RewardedAd> _rewardedByWhere = new System.Collections.Generic.Dictionary<string, RewardedAd>();
        private readonly System.Collections.Generic.Dictionary<string, AppOpenAd> _appOpenByWhere = new System.Collections.Generic.Dictionary<string, AppOpenAd>();
        private readonly System.Collections.Generic.Dictionary<string, DateTime> _appOpenExpireByWhere = new System.Collections.Generic.Dictionary<string, DateTime>();
        private DateTime _appOpenExpireTime = DateTime.MinValue;
        private const int AppOpenTimeoutHours = 4;

        private int _retryInterstitialAttempt;
        private int _retryRewardedAttempt;
        private const int LoadRetryExponentCap = 6;
#endif

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "null" : value;
        }

        private void LogAdTrace(string phase, AdUnitType type, string unitId, string where = null, string extra = null)
        {
            var message = "[GameUp] AdmobAds " + phase +
                          " | type=" + type +
                          " | where=" + Safe(where) +
                          " | unitId=" + Safe(unitId);
            if (!string.IsNullOrEmpty(extra))
                message += " | " + extra;
            Debug.Log(message);
        }

#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
        private static string ToCollapsibleKeyword(CollapsibleBannerPlacement placement)
        {
            switch (placement)
            {
                case CollapsibleBannerPlacement.Top: return "top";
                case CollapsibleBannerPlacement.Bottom: return "bottom";
                default: return null;
            }
        }

        private static AdPosition GetBannerPosition(CollapsibleBannerPlacement placement)
        {
            return placement == CollapsibleBannerPlacement.Top ? AdPosition.Top : AdPosition.Bottom;
        }

        private string ResolveBannerUnitIdForRequest(string where)
        {
            var requestedUnitId = ResolveUnitId(AdUnitType.Banner, where);
            if (!string.IsNullOrEmpty(requestedUnitId))
                return requestedUnitId;

            if (!useMultiAdUnitIds)
                return GetSingleUnitId(AdUnitType.Banner);

            string fallbackUnitId = null;
            var activeAdUnitIds = GetActiveAdUnitIds();
            for (int i = 0; i < activeAdUnitIds.Count; i++)
            {
                var e = activeAdUnitIds[i];
                if (e == null || e.AdType != AdUnitType.Banner || !e.IsValid()) continue;
                if (string.Equals(e.NameId, "main", StringComparison.OrdinalIgnoreCase))
                    return e.Id;
                if (fallbackUnitId == null)
                    fallbackUnitId = e.Id;
            }

            return fallbackUnitId;
        }

        private static AdRequest BuildBannerRequest(CollapsibleBannerPlacement placement)
        {
            var request = new AdRequest();
            var collapsibleKeyword = ToCollapsibleKeyword(placement);
            if (!string.IsNullOrEmpty(collapsibleKeyword))
                request.Extras.Add("collapsible", collapsibleKeyword);
            return request;
        }

        private static bool TryReadIsCollapsible(BannerView bannerView, out bool isCollapsible)
        {
            isCollapsible = false;
            if (bannerView == null)
                return false;

            try
            {
                var method = typeof(BannerView).GetMethod("IsCollapsible", Type.EmptyTypes);
                if (method == null || method.ReturnType != typeof(bool))
                    return false;

                isCollapsible = (bool)method.Invoke(bannerView, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ShowBannerInternal(string where, CollapsibleBannerPlacement placement)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                _bannerShouldBeVisible = true;
                _bannerPlacementForShow = string.IsNullOrEmpty(where) ? "main" : where;

                var unitId = ResolveBannerUnitIdForRequest(where);
                var targetPosition = GetBannerPosition(placement);
                var requiresReload = !string.IsNullOrEmpty(unitId) &&
                                     (_bannerView == null ||
                                      _bannerUnitIdActive != unitId ||
                                      _bannerCollapsiblePlacementActive != placement ||
                                      _bannerPositionActive != targetPosition);

                if (requiresReload)
                {
                    RequestBannerInternal(unitId, placement, where);
                    return;
                }

                if (_bannerView == null)
                {
                    GULogger.Warning("GameUp", "AdmobAds ShowBanner skipped: no BannerView. where=" + where + ", resolvedUnitId=" + (unitId ?? "null"));
                    OnBannerShowFailed?.Invoke(_bannerPlacementForShow);
                    return;
                }

                if (_bannerLoaded)
                {
                    LogAdTrace(
                        "show",
                        AdUnitType.Banner,
                        _bannerUnitIdActive,
                        where,
                        "collapsibleRequest=" + (_bannerCollapsiblePlacementActive != CollapsibleBannerPlacement.None) +
                        ", isCollapsible=" + _bannerIsCollapsible);
                    _bannerView.Show();
                    OnBannerShown?.Invoke(_bannerPlacementForShow);
                    return;
                }

                if (!string.IsNullOrEmpty(unitId) && !_bannerLoading)
                {
                    LogAdTrace("show_deferred", AdUnitType.Banner, unitId, where, "reason=not_loaded_retry_request");
                    RequestBannerInternal(unitId, placement, where);
                }
                else
                {
                    GULogger.Log("GameUp", "AdmobAds ShowBanner waiting load. where=" + where + ", activeUnitId=" + (_bannerUnitIdActive ?? "null"));
                }
            });
        }
#endif

        public void SetAdUnitIds(string banner, string interstitial, string rewarded, string appOpen)
        {
            // Keep backward compatibility: old API sets both platforms.
            bannerAdUnitIdAndroid = banner;
            bannerAdUnitIdIOS = banner;
            interstitialAdUnitIdAndroid = interstitial;
            interstitialAdUnitIdIOS = interstitial;
            rewardedAdUnitIdAndroid = rewarded;
            rewardedAdUnitIdIOS = rewarded;
            appOpenAdUnitIdAndroid = appOpen;
            appOpenAdUnitIdIOS = appOpen;
        }

        public void Initialize()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (_initialized)
            {
                GULogger.Log("GameUp", "AdmobAds already initialized.");
                return;
            }

            MobileAds.Initialize(initStatus =>
            {
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    _initialized = true;
                    GULogger.Log("GameUp", "AdmobAds initialized.");
                    // Request ads ngay khi SDK sáºµn sÃ ng (trÃ¡nh gá»i RequestAll() trÆ°á»›c khi init xong).
                    RequestBanner();
                    RequestInterstitial();
                    RequestRewardedVideo();
                    RequestAppOpenAds();
                });
            });
#else
            _initialized = true;
            GULogger.Log("GameUp", "AdmobAds skipped (not mobile platform).");
#endif
        }

        public void SetAfterCheckGDPR()
        {
            SetAfterCheckGDPR(true);
        }

        public void SetAfterCheckGDPR(bool isConsent)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            // Forward UMP decision to mediation adapters.
            GULogger.Log("GameUp", "AdmobAds SetAfterCheckGDPR called. consent=" + isConsent);
            GoogleMobileAds.Mediation.UnityAds.Api.UnityAds.SetConsentMetaData("gdpr.consent", isConsent);
            GoogleMobileAds.Mediation.IronSource.Api.IronSource.SetMetaData("do_not_sell", isConsent ? "false" : "true");
#endif
        }

        public void RequestBanner()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!_initialized) return;
            var unitId = ResolveBannerUnitIdForRequest(where: null);
            if (!string.IsNullOrEmpty(unitId))
                RequestBannerInternal(unitId, CollapsibleBannerPlacement.None, where: null);
#endif
        }

        public void RequestCollapsibleBanner(string where, CollapsibleBannerPlacement placement = CollapsibleBannerPlacement.Bottom)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!_initialized) return;
            var unitId = ResolveBannerUnitIdForRequest(where);
            if (!string.IsNullOrEmpty(unitId))
                RequestBannerInternal(unitId, placement == CollapsibleBannerPlacement.None ? CollapsibleBannerPlacement.Bottom : placement, where);
#endif
        }

#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
        private void RequestBannerInternal(string unitId, CollapsibleBannerPlacement placement, string where)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                var collapsibleKeyword = ToCollapsibleKeyword(placement);
                LogAdTrace("request", AdUnitType.Banner, unitId, where, "collapsible=" + Safe(collapsibleKeyword));
                if (_bannerRequestInProgress)
                {
                    if (_bannerUnitIdActive == unitId && _bannerCollapsiblePlacementActive == placement)
                    {
                        LogAdTrace("request_skip", AdUnitType.Banner, unitId, where, extra: "reason=request_in_progress_same_request");
                        return;
                    }

                    _pendingBannerUnitId = unitId;
                    _pendingBannerCollapsiblePlacement = placement;
                    LogAdTrace("request_deferred", AdUnitType.Banner, unitId, where, extra: "reason=request_in_progress_pending_switch,collapsible=" + Safe(collapsibleKeyword));
                    return;
                }

                var targetPosition = GetBannerPosition(placement);
                if (_bannerView != null && (_bannerUnitIdActive != unitId || _bannerPositionActive != targetPosition))
                {
                    _bannerView.Destroy();
                    _bannerView = null;
                    _bannerLoaded = false;
                    _bannerIsCollapsible = false;
                    _bannerLoading = false;
                    _bannerRequestInProgress = false;
                }

                _bannerUnitIdActive = unitId;
                _bannerCollapsiblePlacementActive = placement;
                _bannerPositionActive = targetPosition;
                if (_bannerView == null)
                {
                    var adaptiveBannerSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
                    GULogger.Log("GameUp", "AdmobAds banner_adaptive_size | unitId=" + Safe(unitId) + " | width=" + adaptiveBannerSize.Width + " | height=" + adaptiveBannerSize.Height);
                    _bannerView = new BannerView(unitId, adaptiveBannerSize, targetPosition);
                    var currentBannerView = _bannerView;
                    _bannerView.OnBannerAdLoaded += () =>
                    {
                        MainThreadDispatcher.Enqueue(() =>
                        {
                            if (!ReferenceEquals(_bannerView, currentBannerView))
                                return;

                            _bannerLoaded = true;
                            _bannerLoading = false;
                            _bannerRequestInProgress = false;
                            if (!TryReadIsCollapsible(currentBannerView, out _bannerIsCollapsible))
                                _bannerIsCollapsible = _bannerCollapsiblePlacementActive != CollapsibleBannerPlacement.None;
                            LogAdTrace(
                                "load_success",
                                AdUnitType.Banner,
                                _bannerUnitIdActive,
                                _bannerPlacementForShow,
                                "requestedCollapsible=" + Safe(ToCollapsibleKeyword(_bannerCollapsiblePlacementActive)) +
                                ", isCollapsible=" + _bannerIsCollapsible);
                            if (_bannerShouldBeVisible)
                            {
                                LogAdTrace(
                                    "show",
                                    AdUnitType.Banner,
                                    _bannerUnitIdActive,
                                    _bannerPlacementForShow,
                                    "from=auto_on_loaded,isCollapsible=" + _bannerIsCollapsible);
                                _bannerView?.Show();
                                OnBannerShown?.Invoke(string.IsNullOrEmpty(_bannerPlacementForShow) ? "main" : _bannerPlacementForShow);
                            }

                            if (!string.IsNullOrEmpty(_pendingBannerUnitId) &&
                                (_pendingBannerUnitId != _bannerUnitIdActive || _pendingBannerCollapsiblePlacement != _bannerCollapsiblePlacementActive))
                            {
                                var pendingUnitId = _pendingBannerUnitId;
                                var pendingPlacement = _pendingBannerCollapsiblePlacement;
                                _pendingBannerUnitId = null;
                                _pendingBannerCollapsiblePlacement = CollapsibleBannerPlacement.None;
                                RequestBannerInternal(pendingUnitId, pendingPlacement, _bannerPlacementForShow);
                            }
                        });
                    };
                    _bannerView.OnBannerAdLoadFailed += loadError =>
                    {
                        MainThreadDispatcher.Enqueue(() =>
                        {
                            if (!ReferenceEquals(_bannerView, currentBannerView))
                                return;

                            _bannerLoaded = false;
                            if (!TryReadIsCollapsible(currentBannerView, out _bannerIsCollapsible))
                                _bannerIsCollapsible = _bannerCollapsiblePlacementActive != CollapsibleBannerPlacement.None;
                            _bannerLoading = false;
                            _bannerRequestInProgress = false;
                            var message = loadError?.GetMessage() ?? "unknown";
                            var code = loadError != null ? loadError.GetCode().ToString() : "unknown";
                            GULogger.Warning("GameUp", "AdmobAds load_fail | type=Banner | where=" + Safe(_bannerPlacementForShow) + " | unitId=" + Safe(_bannerUnitIdActive) + " | code=" + code + " | message=" + message + " | collapsible=" + Safe(ToCollapsibleKeyword(_bannerCollapsiblePlacementActive)) + " | isCollapsible=" + _bannerIsCollapsible);
                            if (_bannerShouldBeVisible)
                                OnBannerShowFailed?.Invoke(string.IsNullOrEmpty(_bannerPlacementForShow) ? "main" : _bannerPlacementForShow);

                            if (!string.IsNullOrEmpty(_pendingBannerUnitId))
                            {
                                var pendingUnitId = _pendingBannerUnitId;
                                var pendingPlacement = _pendingBannerCollapsiblePlacement;
                                _pendingBannerUnitId = null;
                                _pendingBannerCollapsiblePlacement = CollapsibleBannerPlacement.None;
                                RequestBannerInternal(pendingUnitId, pendingPlacement, _bannerPlacementForShow);
                            }
                        });
                    };
                    _bannerView.OnAdPaid += adValue =>
                    {
                        MainThreadDispatcher.Enqueue(() =>
                        {
                            if (adValue == null)
                                return;
                            double value = adValue.Value * 0.000001f;
                            var data = new AdImpressionData
                            {
                                AdNetwork = "Admob",
                                AdUnit = unitId,
                                InstanceName = unitId,
                                AdFormat = "Banner",
                                Revenue = value
                            };
                            AdsEvent.RaiseImpressionDataReady(data);
                        });
                    };
                }

                var request = BuildBannerRequest(placement);
                _bannerLoaded = false;
                _bannerIsCollapsible = false;
                _bannerLoading = true;
                _bannerRequestInProgress = true;
                _bannerView.Hide();
                _bannerView.LoadAd(request);
            });
        }
#endif

        public void RequestInterstitial()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!_initialized) return;
            if (!useMultiAdUnitIds)
            {
                var singleId = GetSingleUnitId(AdUnitType.Interstitial);
                if (string.IsNullOrEmpty(singleId)) return;
                RequestInterstitialInternal(singleId, where: null);
                return;
            }

            foreach (var e in GetActiveAdUnitIds())
            {
                if (e == null || e.AdType != AdUnitType.Interstitial || !e.IsValid()) continue;
                RequestInterstitialInternal(e.Id, e.NameId);
            }
#endif
        }

        public void RequestRewardedVideo()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!_initialized) return;
            if (!useMultiAdUnitIds)
            {
                var singleId = GetSingleUnitId(AdUnitType.RewardedVideo);
                if (string.IsNullOrEmpty(singleId)) return;
                RequestRewardedInternal(singleId, where: null);
                return;
            }

            foreach (var e in GetActiveAdUnitIds())
            {
                if (e == null || e.AdType != AdUnitType.RewardedVideo || !e.IsValid()) continue;
                RequestRewardedInternal(e.Id, e.NameId);
            }
#endif
        }

        public void RequestAppOpenAds()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!_initialized) return;
            if (!useMultiAdUnitIds)
            {
                var singleId = GetSingleUnitId(AdUnitType.AppOpen);
                if (string.IsNullOrEmpty(singleId)) return;
                RequestAppOpenInternal(singleId, where: null);
                return;
            }

            foreach (var e in GetActiveAdUnitIds())
            {
                if (e == null || e.AdType != AdUnitType.AppOpen || !e.IsValid()) continue;
                RequestAppOpenInternal(e.Id, e.NameId);
            }
#endif
        }

#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
        private void ResetInterstitialLoadRetry()
        {
            _retryInterstitialAttempt = 0;
            CancelInvoke(nameof(RequestInterstitial));
        }

        private void ResetRewardedLoadRetry()
        {
            _retryRewardedAttempt = 0;
            CancelInvoke(nameof(RequestRewardedVideo));
        }

        private void ScheduleInterstitialLoadRetry()
        {
            _retryInterstitialAttempt++;
            var retryDelay = (float)Math.Pow(2, Math.Min(LoadRetryExponentCap, _retryInterstitialAttempt));
            CancelInvoke(nameof(RequestInterstitial));
            Invoke(nameof(RequestInterstitial), retryDelay);
            LogAdTrace("load_retry_scheduled", AdUnitType.Interstitial, null, null, "delaySeconds=" + retryDelay + ",attempt=" + _retryInterstitialAttempt);
        }

        private void ScheduleRewardedLoadRetry()
        {
            _retryRewardedAttempt++;
            var retryDelay = (float)Math.Pow(2, Math.Min(LoadRetryExponentCap, _retryRewardedAttempt));
            CancelInvoke(nameof(RequestRewardedVideo));
            Invoke(nameof(RequestRewardedVideo), retryDelay);
            LogAdTrace("load_retry_scheduled", AdUnitType.RewardedVideo, null, null, "delaySeconds=" + retryDelay + ",attempt=" + _retryRewardedAttempt);
        }

        private void RequestInterstitialAfterNotReady(string where)
        {
            if (useMultiAdUnitIds && !string.IsNullOrEmpty(where))
            {
                var unitId = ResolveUnitId(AdUnitType.Interstitial, where);
                if (!string.IsNullOrEmpty(unitId))
                    RequestInterstitialInternal(unitId, where);
                return;
            }

            RequestInterstitial();
        }

        private void RequestRewardedAfterNotReady(string where)
        {
            if (useMultiAdUnitIds && !string.IsNullOrEmpty(where))
            {
                var unitId = ResolveUnitId(AdUnitType.RewardedVideo, where);
                if (!string.IsNullOrEmpty(unitId))
                    RequestRewardedInternal(unitId, where);
                return;
            }

            RequestRewardedVideo();
        }

        private void RequestInterstitialInternal(string unitId, string where)
        {
            LogAdTrace("request", AdUnitType.Interstitial, unitId, where);
            var request = new AdRequest();
            InterstitialAd.Load(unitId, request, (ad, error) =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (error != null || ad == null)
                    {
                        var source = error?.GetMessage() ?? (error != null ? error.GetCode().ToString() : "unknown");
                        var code = error != null ? error.GetCode().ToString() : "unknown";
                        GULogger.Warning("GameUp", "AdmobAds load_fail | type=Interstitial | where=" + Safe(where) + " | unitId=" + Safe(unitId) + " | code=" + code + " | message=" + source);
                        OnInterstitialLoadFailed?.Invoke(source);
                        ScheduleInterstitialLoadRetry();
                        return;
                    }

                    ResetInterstitialLoadRetry();

                    if (useMultiAdUnitIds && !string.IsNullOrEmpty(where))
                    {
                        if (_interstitialByWhere.TryGetValue(where, out var old) && old != null) old.Destroy();
                        _interstitialByWhere[where] = ad;
                        RegisterInterstitialEvents(ad, where);
                    }
                    else
                    {
                        if (_interstitialAd != null) _interstitialAd.Destroy();
                        _interstitialAd = ad;
                        RegisterInterstitialEvents(ad, where: null);
                    }

                    LogAdTrace("load_success", AdUnitType.Interstitial, ad.GetAdUnitID(), where);
                    OnInterstitialLoaded?.Invoke();
                });
            });
        }

        private void RequestRewardedInternal(string unitId, string where)
        {
            LogAdTrace("request", AdUnitType.RewardedVideo, unitId, where);
            var request = new AdRequest();
            RewardedAd.Load(unitId, request, (ad, error) =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (error != null || ad == null)
                    {
                        var source = error?.GetMessage() ?? (error != null ? error.GetCode().ToString() : "unknown");
                        var code = error != null ? error.GetCode().ToString() : "unknown";
                        GULogger.Warning("GameUp", "AdmobAds load_fail | type=RewardedVideo | where=" + Safe(where) + " | unitId=" + Safe(unitId) + " | code=" + code + " | message=" + source);
                        OnRewardedLoadFailed?.Invoke(source);
                        ScheduleRewardedLoadRetry();
                        return;
                    }

                    ResetRewardedLoadRetry();

                    if (useMultiAdUnitIds && !string.IsNullOrEmpty(where))
                    {
                        if (_rewardedByWhere.TryGetValue(where, out var old) && old != null) old.Destroy();
                        _rewardedByWhere[where] = ad;
                    }
                    else
                    {
                        if (_rewardedAd != null) _rewardedAd.Destroy();
                        _rewardedAd = ad;
                    }

                    LogAdTrace("load_success", AdUnitType.RewardedVideo, ad.GetAdUnitID(), where);
                    OnRewardedLoaded?.Invoke();
                    ad.OnAdPaid += adValue =>
                    {
                        MainThreadDispatcher.Enqueue(() =>
                        {
                            if (adValue == null)
                                return;
                            double value = adValue.Value * 0.000001f;
                            var data = new AdImpressionData
                            {
                                AdNetwork = "Admob",
                                AdUnit = ad.GetAdUnitID(),
                                InstanceName = ad.GetAdUnitID(),
                                AdFormat = "Rewarded",
                                Revenue = value
                            };
                            MainThreadDispatcher.Enqueue(() => AdsEvent.RaiseImpressionDataReady(data));
                        });
                    };
                });
            });
        }

        private void RequestAppOpenInternal(string unitId, string where)
        {
            LogAdTrace("request", AdUnitType.AppOpen, unitId, where);
            if (useMultiAdUnitIds && !string.IsNullOrEmpty(where))
            {
                if (_appOpenByWhere.TryGetValue(where, out var old) && old != null) old.Destroy();
                _appOpenByWhere.Remove(where);
                _appOpenExpireByWhere.Remove(where);
            }
            else
            {
                if (_appOpenAd != null)
                {
                    _appOpenAd.Destroy();
                    _appOpenAd = null;
                }
            }

            var request = new AdRequest();
            AppOpenAd.Load(unitId, request, (ad, error) =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (error != null || ad == null)
                    {
                        var message = error?.GetMessage() ?? "unknown";
                        var code = error != null ? error.GetCode().ToString() : "unknown";
                        GULogger.Warning("GameUp", "AdmobAds load_fail | type=AppOpen | where=" + Safe(where) + " | unitId=" + Safe(unitId) + " | code=" + code + " | message=" + message);
                        return;
                    }

                    var expire = DateTime.Now + TimeSpan.FromHours(AppOpenTimeoutHours);
                    if (useMultiAdUnitIds && !string.IsNullOrEmpty(where))
                    {
                        _appOpenByWhere[where] = ad;
                        _appOpenExpireByWhere[where] = expire;
                        RegisterAppOpenEvents(ad, where);
                    }
                    else
                    {
                        _appOpenAd = ad;
                        _appOpenExpireTime = expire;
                        RegisterAppOpenEvents(ad, where: null);
                    }
                    LogAdTrace("load_success", AdUnitType.AppOpen, ad.GetAdUnitID(), where, "expireAt=" + expire.ToString("O"));
                });
            });
        }

        private void RegisterInterstitialEvents(InterstitialAd ad)
        {
            RegisterInterstitialEvents(ad, where: null);
        }

        private void RegisterInterstitialEvents(InterstitialAd ad, string where)
        {
            ad.OnAdFullScreenContentClosed += () =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (useMultiAdUnitIds && !string.IsNullOrEmpty(where))
                    {
                        if (_interstitialByWhere.TryGetValue(where, out var cur) && cur != null) cur.Destroy();
                        _interstitialByWhere.Remove(where);
                        var unitId = ResolveUnitId(AdUnitType.Interstitial, where);
                        if (!string.IsNullOrEmpty(unitId))
                            RequestInterstitialInternal(unitId, where);
                    }
                    else
                    {
                        _interstitialAd?.Destroy();
                        _interstitialAd = null;
                        RequestInterstitial();
                    }
                });
            };
            ad.OnAdFullScreenContentFailed += _ =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (useMultiAdUnitIds && !string.IsNullOrEmpty(where))
                    {
                        if (_interstitialByWhere.TryGetValue(where, out var cur) && cur != null) cur.Destroy();
                        _interstitialByWhere.Remove(where);
                        var unitId = ResolveUnitId(AdUnitType.Interstitial, where);
                        if (!string.IsNullOrEmpty(unitId))
                            RequestInterstitialInternal(unitId, where);
                    }
                    else
                    {
                        _interstitialAd?.Destroy();
                        _interstitialAd = null;
                        RequestInterstitial();
                    }
                });
            };

            ad.OnAdPaid += adValue =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (adValue == null)
                        return;
                    double value = adValue.Value * 0.000001f;
                    var data = new AdImpressionData
                    {
                        AdNetwork = "Admob",
                        AdUnit = ad.GetAdUnitID(),
                        InstanceName = ad.GetAdUnitID(),
                        AdFormat = "Interstitial",
                        Revenue = value
                    };
                    MainThreadDispatcher.Enqueue(() => AdsEvent.RaiseImpressionDataReady(data));
                });
            };
        }

        private void RegisterAppOpenEvents(AppOpenAd ad)
        {
            RegisterAppOpenEvents(ad, where: null);
        }

        private void RegisterAppOpenEvents(AppOpenAd ad, string where)
        {
            ad.OnAdFullScreenContentClosed += () =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (useMultiAdUnitIds && !string.IsNullOrEmpty(where))
                    {
                        if (_appOpenByWhere.TryGetValue(where, out var cur) && cur != null) cur.Destroy();
                        _appOpenByWhere.Remove(where);
                        _appOpenExpireByWhere.Remove(where);
                        var unitId = ResolveUnitId(AdUnitType.AppOpen, where);
                        if (!string.IsNullOrEmpty(unitId))
                            RequestAppOpenInternal(unitId, where);
                    }
                    else
                    {
                        _appOpenAd?.Destroy();
                        _appOpenAd = null;
                        RequestAppOpenAds();
                    }
                });
            };
            ad.OnAdFullScreenContentFailed += _ =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (useMultiAdUnitIds && !string.IsNullOrEmpty(where))
                    {
                        if (_appOpenByWhere.TryGetValue(where, out var cur) && cur != null) cur.Destroy();
                        _appOpenByWhere.Remove(where);
                        _appOpenExpireByWhere.Remove(where);
                        var unitId = ResolveUnitId(AdUnitType.AppOpen, where);
                        if (!string.IsNullOrEmpty(unitId))
                            RequestAppOpenInternal(unitId, where);
                    }
                    else
                    {
                        _appOpenAd?.Destroy();
                        _appOpenAd = null;
                        RequestAppOpenAds();
                    }
                });
            };

            ad.OnAdPaid += adValue =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (adValue == null)
                        return;
                    double value = adValue.Value * 0.000001f;
                    var data = new AdImpressionData
                    {
                        AdNetwork = "Admob",
                        AdUnit = ad.GetAdUnitID(),
                        InstanceName = ad.GetAdUnitID(),
                        AdFormat = "AppOpenAd",
                        Revenue = value
                    };
                    MainThreadDispatcher.Enqueue(() => AdsEvent.RaiseImpressionDataReady(data));
                });
            };
        }
#endif

        public bool IsBannerAvailable()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            return _bannerView != null;
#else
            return false;
#endif
        }

        public bool IsInterstitialAvailable()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            return _interstitialAd != null && _interstitialAd.CanShowAd();
#else
            return false;
#endif
        }

        public bool IsRewardedVideoAvailable()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            return _rewardedAd != null && _rewardedAd.CanShowAd();
#else
            return false;
#endif
        }

        public bool IsAppOpenAdsAvailable()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            return _appOpenAd != null && _appOpenAd.CanShowAd() && DateTime.Now < _appOpenExpireTime;
#else
            return false;
#endif
        }

        public bool IsCollapsibleBannerAvailable()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            return !string.IsNullOrEmpty(ResolveBannerUnitIdForRequest(where: null));
#else
            return false;
#endif
        }

        public void ShowBanner(string where)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            ShowBannerInternal(where, CollapsibleBannerPlacement.None);
#endif
        }

        public void ShowCollapsibleBanner(string where, CollapsibleBannerPlacement placement = CollapsibleBannerPlacement.Bottom)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            ShowBannerInternal(where, placement == CollapsibleBannerPlacement.None ? CollapsibleBannerPlacement.Bottom : placement);
#endif
        }

        public void HideBanner(string where)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            MainThreadDispatcher.Enqueue(() =>
            {
                _bannerShouldBeVisible = false;
                _bannerView?.Hide();
            });
#endif
        }

        public void ShowInterstitial(string where, Action onSuccess, Action onFail)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (useMultiAdUnitIds)
            {
                if (string.IsNullOrEmpty(where) || !_interstitialByWhere.TryGetValue(where, out var multiAd) || multiAd == null || !multiAd.CanShowAd())
                {
                    LogAdTrace("show_fail", AdUnitType.Interstitial, ResolveUnitId(AdUnitType.Interstitial, where), where, "reason=not_ready");
                    onFail?.Invoke();
                    RequestInterstitialAfterNotReady(where);
                    return;
                }

                _interstitialByWhere.Remove(where);
                LogAdTrace("show", AdUnitType.Interstitial, multiAd.GetAdUnitID(), where);
                multiAd.OnAdFullScreenContentClosed += () => MainThreadDispatcher.Enqueue(() => onSuccess?.Invoke());
                multiAd.OnAdFullScreenContentFailed += _ => MainThreadDispatcher.Enqueue(() => onFail?.Invoke());
                multiAd.Show();
                return;
            }

            if (_interstitialAd == null || !_interstitialAd.CanShowAd())
            {
                LogAdTrace("show_fail", AdUnitType.Interstitial, GetSingleUnitId(AdUnitType.Interstitial), where, "reason=not_ready");
                onFail?.Invoke();
                RequestInterstitialAfterNotReady(where);
                return;
            }

            var ad = _interstitialAd;
            _interstitialAd = null;
            LogAdTrace("show", AdUnitType.Interstitial, ad.GetAdUnitID(), where);
            ad.OnAdFullScreenContentClosed += () => MainThreadDispatcher.Enqueue(() => onSuccess?.Invoke());
            ad.OnAdFullScreenContentFailed += _ => MainThreadDispatcher.Enqueue(() => onFail?.Invoke());
            ad.Show();
#else
            onFail?.Invoke();
#endif
        }

        public void ShowRewardedVideo(string where, Action onSuccess, Action onFail)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (useMultiAdUnitIds)
            {
                if (string.IsNullOrEmpty(where) || !_rewardedByWhere.TryGetValue(where, out var multiAd) || multiAd == null || !multiAd.CanShowAd())
                {
                    LogAdTrace("show_fail", AdUnitType.RewardedVideo, ResolveUnitId(AdUnitType.RewardedVideo, where), where, "reason=not_ready");
                    onFail?.Invoke();
                    RequestRewardedAfterNotReady(where);
                    return;
                }

                _rewardedByWhere.Remove(where);
                LogAdTrace("show", AdUnitType.RewardedVideo, multiAd.GetAdUnitID(), where);
                AdsRules.BeginInterstitialCappingPause();
                var rewardGrantedMulti = false;
                multiAd.OnAdFullScreenContentClosed += () =>
                {
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        AdsRules.EndInterstitialCappingPause();
                        if (!rewardGrantedMulti) onFail?.Invoke();
                        var unitId = ResolveUnitId(AdUnitType.RewardedVideo, where);
                        if (!string.IsNullOrEmpty(unitId)) RequestRewardedInternal(unitId, where);
                    });
                };
                multiAd.OnAdFullScreenContentFailed += _ =>
                {
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        AdsRules.EndInterstitialCappingPause();
                        onFail?.Invoke();
                        var unitId = ResolveUnitId(AdUnitType.RewardedVideo, where);
                        if (!string.IsNullOrEmpty(unitId)) RequestRewardedInternal(unitId, where);
                    });
                };
                multiAd.Show(_ =>
                {
                    rewardGrantedMulti = true;
                    MainThreadDispatcher.Enqueue(() => onSuccess?.Invoke());
                });
                return;
            }

            if (_rewardedAd == null || !_rewardedAd.CanShowAd())
            {
                LogAdTrace("show_fail", AdUnitType.RewardedVideo, GetSingleUnitId(AdUnitType.RewardedVideo), where, "reason=not_ready");
                onFail?.Invoke();
                RequestRewardedAfterNotReady(where);
                return;
            }

            AdsRules.BeginInterstitialCappingPause();
            var rewardGranted = false;
            var ad = _rewardedAd;
            _rewardedAd = null;
            LogAdTrace("show", AdUnitType.RewardedVideo, ad.GetAdUnitID(), where);
            ad.OnAdFullScreenContentClosed += () =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    AdsRules.EndInterstitialCappingPause();
                    if (!rewardGranted) onFail?.Invoke();
                    RequestRewardedVideo();
                });
            };
            ad.OnAdFullScreenContentFailed += _ =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    AdsRules.EndInterstitialCappingPause();
                    onFail?.Invoke();
                    RequestRewardedVideo();
                });
            };
            ad.Show(reward =>
            {
                rewardGranted = true;
                MainThreadDispatcher.Enqueue(() => onSuccess?.Invoke());
            });
#else
            onFail?.Invoke();
#endif
        }

        public void ShowAppOpenAds(string where, Action onSuccess, Action onFail)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (useMultiAdUnitIds)
            {
                if (string.IsNullOrEmpty(where) ||
                    !_appOpenByWhere.TryGetValue(where, out var multiAd) ||
                    multiAd == null ||
                    !_appOpenExpireByWhere.TryGetValue(where, out var exp) ||
                    DateTime.Now >= exp ||
                    !multiAd.CanShowAd())
                {
                    LogAdTrace("show_fail", AdUnitType.AppOpen, ResolveUnitId(AdUnitType.AppOpen, where), where, "reason=not_ready_or_expired");
                    onFail?.Invoke();
                    return;
                }

                _appOpenByWhere.Remove(where);
                _appOpenExpireByWhere.Remove(where);
                LogAdTrace("show", AdUnitType.AppOpen, multiAd.GetAdUnitID(), where);
                multiAd.OnAdFullScreenContentClosed += () => MainThreadDispatcher.Enqueue(() =>
                {
                    onSuccess?.Invoke();
                    var unitId = ResolveUnitId(AdUnitType.AppOpen, where);
                    if (!string.IsNullOrEmpty(unitId)) RequestAppOpenInternal(unitId, where);
                });
                multiAd.OnAdFullScreenContentFailed += _ => MainThreadDispatcher.Enqueue(() =>
                {
                    onFail?.Invoke();
                    var unitId = ResolveUnitId(AdUnitType.AppOpen, where);
                    if (!string.IsNullOrEmpty(unitId)) RequestAppOpenInternal(unitId, where);
                });
                multiAd.Show();
                return;
            }

            if (_appOpenAd == null || !_appOpenAd.CanShowAd() || DateTime.Now >= _appOpenExpireTime)
            {
                LogAdTrace("show_fail", AdUnitType.AppOpen, GetSingleUnitId(AdUnitType.AppOpen), where, "reason=not_ready_or_expired");
                onFail?.Invoke();
                return;
            }

            var ad = _appOpenAd;
            _appOpenAd = null;
            LogAdTrace("show", AdUnitType.AppOpen, ad.GetAdUnitID(), where);
            ad.OnAdFullScreenContentClosed += () => MainThreadDispatcher.Enqueue(() =>
            {
                onSuccess?.Invoke();
                RequestAppOpenAds();
            });
            ad.OnAdFullScreenContentFailed += _ => MainThreadDispatcher.Enqueue(() =>
            {
                onFail?.Invoke();
                RequestAppOpenAds();
            });
            ad.Show();
#else
            onFail?.Invoke();
#endif
        }

        private void OnDestroy()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            CancelInvoke(nameof(RequestInterstitial));
            CancelInvoke(nameof(RequestRewardedVideo));
            _bannerView?.Destroy();
            _interstitialAd?.Destroy();
            _rewardedAd?.Destroy();
            _appOpenAd?.Destroy();

            foreach (var kv in _interstitialByWhere) kv.Value?.Destroy();
            foreach (var kv in _rewardedByWhere) kv.Value?.Destroy();
            foreach (var kv in _appOpenByWhere) kv.Value?.Destroy();
            _interstitialByWhere.Clear();
            _rewardedByWhere.Clear();
            _appOpenByWhere.Clear();
            _appOpenExpireByWhere.Clear();
#endif
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
                    if (e == null) continue;
                    if (e.AdType != type) continue;
                    if (!e.IsValid()) continue;
                    if (string.Equals(e.NameId?.Trim(), normalizedWhere, StringComparison.OrdinalIgnoreCase))
                        return e.Id;
                }
            }

            switch (type)
            {
                case AdUnitType.Banner: return GetSingleUnitId(AdUnitType.Banner);
                case AdUnitType.Interstitial: return GetSingleUnitId(AdUnitType.Interstitial);
                case AdUnitType.RewardedVideo: return GetSingleUnitId(AdUnitType.RewardedVideo);
                case AdUnitType.AppOpen: return GetSingleUnitId(AdUnitType.AppOpen);
                default: return null;
            }
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
            return EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS
                ? RuntimeAdPlatform.IOS
                : RuntimeAdPlatform.Android;
#else
            return RuntimeAdPlatform.Android;
#endif
        }

        private string GetSingleUnitId(AdUnitType type)
        {
            bool isAndroid = GetRuntimeAdPlatform() == RuntimeAdPlatform.Android;
            switch (type)
            {
                case AdUnitType.Banner:
                    return isAndroid ? bannerAdUnitIdAndroid : bannerAdUnitIdIOS;
                case AdUnitType.Interstitial:
                    return isAndroid ? interstitialAdUnitIdAndroid : interstitialAdUnitIdIOS;
                case AdUnitType.RewardedVideo:
                    return isAndroid ? rewardedAdUnitIdAndroid : rewardedAdUnitIdIOS;
                case AdUnitType.AppOpen:
                    return isAndroid ? appOpenAdUnitIdAndroid : appOpenAdUnitIdIOS;
                default:
                    return null;
            }
        }

        private System.Collections.Generic.List<AdUnitIdEntry> GetActiveAdUnitIds()
        {
            bool isAndroid = GetRuntimeAdPlatform() == RuntimeAdPlatform.Android;
            var preferred = isAndroid ? adUnitIdsAndroid : adUnitIdsIOS;
            var fallback = isAndroid ? adUnitIdsIOS : adUnitIdsAndroid;
            if (preferred != null && preferred.Count > 0)
                return preferred;
            if (fallback != null && fallback.Count > 0)
                return fallback;
            return preferred ?? new System.Collections.Generic.List<AdUnitIdEntry>();
        }

        bool IAdUnitIdResolver.TryResolve(int intId, out AdUnitType adType, out string nameId)
        {
            adType = AdUnitType.Interstitial;
            nameId = null;

            var activeAdUnitIds = GetActiveAdUnitIds();
            if (!useMultiAdUnitIds || activeAdUnitIds == null || activeAdUnitIds.Count == 0)
                return false;

            for (int i = 0; i < activeAdUnitIds.Count; i++)
            {
                var e = activeAdUnitIds[i];
                if (e == null) continue;
                if (e.intId != intId) continue;
                if (!e.IsValid()) continue;
                adType = e.AdType;
                nameId = e.NameId;
                return !string.IsNullOrEmpty(nameId);
            }
            return false;
        }

        // Placement-aware availability for AdsManager
        bool IPlacementAwareAds.IsBannerAvailable(string where)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!useMultiAdUnitIds) return IsBannerAvailable();
            var unitId = ResolveUnitId(AdUnitType.Banner, where);
            return !string.IsNullOrEmpty(unitId); // banner can be created on demand
#else
            return false;
#endif
        }

        bool IPlacementAwareAds.IsCollapsibleBannerAvailable(string where)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!useMultiAdUnitIds) return IsCollapsibleBannerAvailable();
            return !string.IsNullOrEmpty(ResolveUnitId(AdUnitType.Banner, where));
#else
            return false;
#endif
        }

        bool IPlacementAwareAds.IsInterstitialAvailable(string where)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!useMultiAdUnitIds) return IsInterstitialAvailable();
            return !string.IsNullOrEmpty(where) &&
                   _interstitialByWhere.TryGetValue(where, out var ad) &&
                   ad != null &&
                   ad.CanShowAd();
#else
            return false;
#endif
        }

        bool IPlacementAwareAds.IsRewardedVideoAvailable(string where)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!useMultiAdUnitIds) return IsRewardedVideoAvailable();
            return !string.IsNullOrEmpty(where) &&
                   _rewardedByWhere.TryGetValue(where, out var ad) &&
                   ad != null &&
                   ad.CanShowAd();
#else
            return false;
#endif
        }

        bool IPlacementAwareAds.IsAppOpenAdsAvailable(string where)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!useMultiAdUnitIds) return IsAppOpenAdsAvailable();
            return !string.IsNullOrEmpty(where) &&
                   _appOpenByWhere.TryGetValue(where, out var ad) &&
                   ad != null &&
                   _appOpenExpireByWhere.TryGetValue(where, out var exp) &&
                   DateTime.Now < exp &&
                   ad.CanShowAd();
#else
            return false;
#endif
        }
    }
}