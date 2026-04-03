using System;
using UnityEngine;
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// AdMob (Google Mobile Ads) implementation of IAds. Handles Banner, Interstitial, Rewarded, and App Open.
    /// </summary>
    public class AdmobAds : MonoBehaviour, IAds
    {
        [Header("Ad Unit IDs (optional - set via code)")]
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

        private bool _initialized;

#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
        private BannerView _bannerView;
        private InterstitialAd _interstitialAd;
        private RewardedAd _rewardedAd;
        private AppOpenAd _appOpenAd;
        private DateTime _appOpenExpireTime = DateTime.MinValue;
        private const int AppOpenTimeoutHours = 4;
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
                Debug.Log("[GameUp] AdmobAds already initialized.");
                return;
            }

            MobileAds.Initialize(initStatus =>
            {
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    _initialized = true;
                    Debug.Log("[GameUp] AdmobAds initialized.");
                    // Request ads ngay khi SDK sẵn sàng (tránh gọi RequestAll() trước khi init xong).
                    RequestBanner();
                    RequestInterstitial();
                    RequestRewardedVideo();
                    RequestAppOpenAds();
                });
            });
#else
            _initialized = true;
            Debug.Log("[GameUp] AdmobAds skipped (not mobile platform).");
#endif
        }

        public void SetAfterCheckGDPR()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            // Consent is typically handled by UMP; SDK respects it after init.
            Debug.Log("[GameUp] AdmobAds SetAfterCheckGDPR called.");
            GoogleMobileAds.Mediation.UnityAds.Api.UnityAds.SetConsentMetaData("gdpr.consent", true);
            GoogleMobileAds.Mediation.IronSource.Api.IronSource.SetMetaData("do_not_sell", "true");
#endif
        }

        public void RequestBanner()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!_initialized || string.IsNullOrEmpty(bannerAdUnitId)) return;
            MainThreadDispatcher.Enqueue(() =>
            {
                if (_bannerView != null)
                {
                    _bannerView.Destroy();
                    _bannerView = null;
                }

                // Dùng size chuẩn để có fill. Custom (full width x 150) dễ bị "request doesn't meet size requirements".
                _bannerView = new BannerView(bannerAdUnitId, AdSize.Banner, AdPosition.Bottom);
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
                            AdUnit = bannerAdUnitId,
                            InstanceName = bannerAdUnitId,
                            AdFormat = "Banner",
                            Revenue = value
                        };
                        AdsEvent.RaiseImpressionDataReady(data);
                    });
                };
                var request = new AdRequest();
                _bannerView.LoadAd(request);
            });
#endif
        }

        public void RequestInterstitial()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!_initialized || string.IsNullOrEmpty(interstitialAdUnitId)) return;
            var request = new AdRequest();
            InterstitialAd.Load(interstitialAdUnitId, request, (ad, error) =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (error != null || ad == null)
                    {
                        var source = error?.GetMessage() ?? (error != null ? error.GetCode().ToString() : "unknown");
                        OnInterstitialLoadFailed?.Invoke(source);
                        return;
                    }

                    if (_interstitialAd != null) _interstitialAd.Destroy();
                    _interstitialAd = ad;
                    RegisterInterstitialEvents(ad);
                    OnInterstitialLoaded?.Invoke();
                });
            });
#endif
        }

        public void RequestRewardedVideo()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!_initialized || string.IsNullOrEmpty(rewardedAdUnitId)) return;
            var request = new AdRequest();
            RewardedAd.Load(rewardedAdUnitId, request, (ad, error) =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (error != null || ad == null)
                    {
                        var source = error?.GetMessage() ?? (error != null ? error.GetCode().ToString() : "unknown");
                        OnRewardedLoadFailed?.Invoke(source);
                        return;
                    }

                    if (_rewardedAd != null) _rewardedAd.Destroy();
                    _rewardedAd = ad;
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
#endif
        }

        public void RequestAppOpenAds()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (!_initialized || string.IsNullOrEmpty(appOpenAdUnitId)) return;
            if (_appOpenAd != null)
            {
                _appOpenAd.Destroy();
                _appOpenAd = null;
            }

            var request = new AdRequest();
            AppOpenAd.Load(appOpenAdUnitId, request, (ad, error) =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    if (error != null || ad == null)
                    {
                        Debug.Log("[GameUp] AdmobAds AppOpen load failed: " + (error?.GetMessage() ?? "null"));
                        return;
                    }

                    _appOpenAd = ad;
                    _appOpenExpireTime = DateTime.Now + TimeSpan.FromHours(AppOpenTimeoutHours);
                    RegisterAppOpenEvents(ad);
                });
            });
#endif
        }

#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
        private void RegisterInterstitialEvents(InterstitialAd ad)
        {
            ad.OnAdFullScreenContentClosed += () =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    _interstitialAd?.Destroy();
                    _interstitialAd = null;
                    RequestInterstitial();
                });
            };
            ad.OnAdFullScreenContentFailed += _ =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    _interstitialAd?.Destroy();
                    _interstitialAd = null;
                    RequestInterstitial();
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
            ad.OnAdFullScreenContentClosed += () =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    _appOpenAd?.Destroy();
                    _appOpenAd = null;
                    RequestAppOpenAds();
                });
            };
            ad.OnAdFullScreenContentFailed += _ =>
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    _appOpenAd?.Destroy();
                    _appOpenAd = null;
                    RequestAppOpenAds();
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

        public void ShowBanner(string where)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            MainThreadDispatcher.Enqueue(() =>
            {
                if (_bannerView != null)
                    _bannerView.Show();
            });
#endif
        }

        public void HideBanner(string where)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            MainThreadDispatcher.Enqueue(() => { _bannerView?.Hide(); });
#endif
        }

        public void ShowInterstitial(string where, Action onSuccess, Action onFail)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IPHONE)
            if (_interstitialAd == null || !_interstitialAd.CanShowAd())
            {
                onFail?.Invoke();
                return;
            }

            var ad = _interstitialAd;
            _interstitialAd = null;
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
            if (_rewardedAd == null || !_rewardedAd.CanShowAd())
            {
                onFail?.Invoke();
                return;
            }

            AdsRules.BeginInterstitialCappingPause();
            var rewardGranted = false;
            var ad = _rewardedAd;
            _rewardedAd = null;
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
            if (_appOpenAd == null || !_appOpenAd.CanShowAd() || DateTime.Now >= _appOpenExpireTime)
            {
                onFail?.Invoke();
                return;
            }

            var ad = _appOpenAd;
            _appOpenAd = null;
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
#endif
        }
    }
}