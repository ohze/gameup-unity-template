using System;
using UnityEngine;
using GameUp.Core;
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
#endif



namespace GameUp.SDK
{
    /// <summary>
    /// AdMob (Google Mobile Ads) implementation of IAds. Handles Banner, Interstitial, Rewarded, and App Open.
    /// </summary>
    public class AdmobAds : MonoBehaviour, IAds, IPlacementAwareAds, IAdUnitIdResolver, IConsentAwareAds
    {
        [Header("Ad Unit IDs")]
        [Tooltip("Bật để dùng nhiều Ad Unit theo placement key (where). Tắt = dùng 1 ID/format như hiện tại.")]
        [SerializeField] private bool useMultiAdUnitIds;

        [Tooltip("Danh sách mapping: (AdType, NameId=where, Id=ad unit id). Chỉ dùng khi useMultiAdUnitIds=true.")]
        [SerializeField] private System.Collections.Generic.List<AdUnitIdEntry> adUnitIds = new System.Collections.Generic.List<AdUnitIdEntry>();

        [Header("Single IDs (legacy / fallback)")]
        [SerializeField]
        private string bannerAdUnitId;

        [SerializeField] private string interstitialAdUnitId;
        [SerializeField] private string rewardedAdUnitId;
        [SerializeField] private string appOpenAdUnitId;

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
#endif

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "null" : value;
        }

        private void LogAdTrace(string phase, AdUnitType type, string unitId, string where = null, string extra = null)
        {
            var message = $"AdmobAds {phase} | type={type} | where={Safe(where)} | unitId={Safe(unitId)}";
            if (!string.IsNullOrEmpty(extra))
                message = $"{message} | {extra}";
            GULogger.Log("GameUp", message);
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
                return bannerAdUnitId;

            string fallbackUnitId = null;
            for (int i = 0; i < adUnitIds.Count; i++)
            {
                var e = adUnitIds[i];
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
                    GULogger.Warning("GameUp", $"AdmobAds ShowBanner skipped: no BannerView. where={where}, resolvedUnitId={unitId ?? "null"}");
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
                    GULogger.Log("GameUp", $"AdmobAds ShowBanner waiting load. where={where}, activeUnitId={_bannerUnitIdActive ?? "null"}");
                }
            });
        }
#endif

        public void SetAdUnitIds(string banner, string interstitial, string rewarded, string appOpen)
        {
            bannerAdUnitId = banner;
            interstitialAdUnitId = interstitial;
            rewardedAdUnitId = rewarded;
            appOpenAdUnitId = appOpen;
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
                    // Request ads ngay khi SDK sẵn sàng (tránh gọi RequestAll() trước khi init xong).
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
            GULogger.Log("GameUp", $"AdmobAds SetAfterCheckGDPR called. consent={isConsent}");
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
                    GULogger.Log("GameUp", $"AdmobAds banner_adaptive_size | unitId={Safe(unitId)} | width={adaptiveBannerSize.Width} | height={adaptiveBannerSize.Height}");
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
                            GULogger.Warning("GameUp", $"AdmobAds load_fail | type=Banner | where={Safe(_bannerPlacementForShow)} | unitId={Safe(_bannerUnitIdActive)} | code={code} | message={message} | collapsible={Safe(ToCollapsibleKeyword(_bannerCollapsiblePlacementActive))} | isCollapsible={_bannerIsCollapsible}");
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
                if (string.IsNullOrEmpty(interstitialAdUnitId)) return;
                RequestInterstitialInternal(interstitialAdUnitId, where: null);
                return;
            }

            foreach (var e in adUnitIds)
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
                if (string.IsNullOrEmpty(rewardedAdUnitId)) return;
                RequestRewardedInternal(rewardedAdUnitId, where: null);
                return;
            }

            foreach (var e in adUnitIds)
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
                if (string.IsNullOrEmpty(appOpenAdUnitId)) return;
                RequestAppOpenInternal(appOpenAdUnitId, where: null);
                return;
            }

            foreach (var e in adUnitIds)
            {
                if (e == null || e.AdType != AdUnitType.AppOpen || !e.IsValid()) continue;
                RequestAppOpenInternal(e.Id, e.NameId);
            }
#endif
        }

#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
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
                        GULogger.Warning("GameUp", $"AdmobAds load_fail | type=Interstitial | where={Safe(where)} | unitId={Safe(unitId)} | code={code} | message={source}");
                        OnInterstitialLoadFailed?.Invoke(source);
                        return;
                    }

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
                        GULogger.Warning("GameUp", $"AdmobAds load_fail | type=RewardedVideo | where={Safe(where)} | unitId={Safe(unitId)} | code={code} | message={source}");
                        OnRewardedLoadFailed?.Invoke(source);
                        return;
                    }

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
                        GULogger.Warning("GameUp", $"AdmobAds load_fail | type=AppOpen | where={Safe(where)} | unitId={Safe(unitId)} | code={code} | message={message}");
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
                LogAdTrace("show_fail", AdUnitType.Interstitial, interstitialAdUnitId, where, "reason=not_ready");
                onFail?.Invoke();
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
                LogAdTrace("show_fail", AdUnitType.RewardedVideo, rewardedAdUnitId, where, "reason=not_ready");
                onFail?.Invoke();
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
                LogAdTrace("show_fail", AdUnitType.AppOpen, appOpenAdUnitId, where, "reason=not_ready_or_expired");
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
                for (int i = 0; i < adUnitIds.Count; i++)
                {
                    var e = adUnitIds[i];
                    if (e == null) continue;
                    if (e.AdType != type) continue;
                    if (!e.IsValid()) continue;
                    if (string.Equals(e.NameId?.Trim(), normalizedWhere, StringComparison.OrdinalIgnoreCase))
                        return e.Id;
                }
            }

            switch (type)
            {
                case AdUnitType.Banner: return bannerAdUnitId;
                case AdUnitType.Interstitial: return interstitialAdUnitId;
                case AdUnitType.RewardedVideo: return rewardedAdUnitId;
                case AdUnitType.AppOpen: return appOpenAdUnitId;
                default: return null;
            }
        }

        bool IAdUnitIdResolver.TryResolve(int intId, out AdUnitType adType, out string nameId)
        {
            adType = AdUnitType.Interstitial;
            nameId = null;

            if (!useMultiAdUnitIds || adUnitIds == null || adUnitIds.Count == 0)
                return false;

            for (int i = 0; i < adUnitIds.Count; i++)
            {
                var e = adUnitIds[i];
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