using GameUp.Core;
using System.Collections.Generic;
using UnityEngine;

namespace GameUp.SDK
{
    public class AdsTracker : MonoBehaviour
    {
        private readonly Dictionary<object, object> _logParamCache = new Dictionary<object, object>(3);
        private readonly Dictionary<string, string> _afParamCache = new Dictionary<string, string>(1);
        private readonly Dictionary<string, int> _placementLevelCache = new Dictionary<string, int>();

        public void RegisterPlacementLevel(string where, int level)
        {
            _placementLevelCache[where] = level;
        }

        private int GetLevel(string where)
        {
            return _placementLevelCache.GetValueOrDefault(where, 0);
        }

        public void SubscribeToNetwork(IAdNetwork network)
        {
            if (network.InterstitialAd != null)
            {
                network.InterstitialAd.OnAdLoaded += (where) => LogAdsEvent(AdsEvent.InterCompleteLoad, null, null);
                network.InterstitialAd.OnAdLoadFailed += (where, error) => LogAdsEvent(AdsEvent.InterLoadFail, null, error);
                network.InterstitialAd.OnAdDisplayed += HandleInterDisplayed;
                network.InterstitialAd.OnAdDisplayFailed += HandleInterDisplayFailed;
            }

            if (network.RewardedAd != null)
            {
                network.RewardedAd.OnAdLoaded += (where) => LogAdsEvent(AdsEvent.RewardCompleteLoad, null, null);
                network.RewardedAd.OnAdLoadFailed += (where, error) => LogAdsEvent(AdsEvent.RewardLoadFail, null, error);
                network.RewardedAd.OnAdDisplayed += HandleRewardDisplayed;
                network.RewardedAd.OnAdDisplayFailed += HandleRewardDisplayFailed;
            }

            if (network.BannerAd != null)
            {
                network.BannerAd.OnAdDisplayed += (where) => LogAdsEventManager(AdsEvent.AdsShowSuccess, AdsEvent.AdTypeBanner, where);
                network.BannerAd.OnAdDisplayFailed += (where, error) => LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeBanner, where, error);
            }

            if (network.AppOpenAd != null)
            {
                network.AppOpenAd.OnAdDisplayed += (where) => LogAdsEventManager(AdsEvent.AdsShowSuccess, AdsEvent.AdTypeAppOpen, where);
                network.AppOpenAd.OnAdDisplayFailed += (where, error) => LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeAppOpen, where, error);
            }
        }

        private void HandleInterDisplayed(string where)
        {
            int level = GetLevel(where);
            LogAdsEvent(AdsEvent.InterShow, where, null, AdsEvent.AfInterShow);
            LogAdsEventWithLevel(AdsEvent.InterShowComplete, where, level, AdsEvent.AfInterDisplayed);
            GameUpAnalytics.LogVideoAdsStarted(AppMetricaEvent.AdTypeInterstitial, where, AppMetricaEvent.ResultStart, true);
            GameUpAnalytics.LogVideoAdsWatch(AppMetricaEvent.AdTypeInterstitial, where, AppMetricaEvent.ResultWatched, true);
        }

        private void HandleInterDisplayFailed(string where, string error)
        {
            LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeInterstitial, where, error);
            GameUpAnalytics.LogVideoAdsStarted(AppMetricaEvent.AdTypeInterstitial, where, AppMetricaEvent.ResultFailed, true);
        }

        private void HandleRewardDisplayed(string where)
        {
            int level = GetLevel(where);
            LogAdsEvent(AdsEvent.RewardShow, where, null, AdsEvent.AfRewardShow);
            LogAdsEventWithLevel(AdsEvent.RewardShowComplete, where, level, AdsEvent.AfRewardDisplayed);
            GameUpAnalytics.LogVideoAdsStarted(AppMetricaEvent.AdTypeRewarded, where, AppMetricaEvent.ResultStart, true);
            GameUpAnalytics.LogVideoAdsWatch(AppMetricaEvent.AdTypeRewarded, where, AppMetricaEvent.ResultWatched, true);
        }

        private void HandleRewardDisplayFailed(string where, string error)
        {
            LogAdsEventManager(AdsEvent.AdsShowFail, AdsEvent.AdTypeRewardedVideo, where, error);
            GameUpAnalytics.LogVideoAdsStarted(AppMetricaEvent.AdTypeRewarded, where, AppMetricaEvent.ResultFailed, true);
        }

        private void LogAdsEvent(string eventName, string paramWhere = null, string paramSource = null, string appsFlyerEventName = null)
        {
            if (!string.IsNullOrEmpty(paramWhere)) FirebaseUtils.LogEvent(eventName, AdsEvent.ParamWhere, paramWhere);
            else if (!string.IsNullOrEmpty(paramSource)) FirebaseUtils.LogEvent(eventName, AdsEvent.ParamSource, paramSource);
            else FirebaseUtils.LogEvent(eventName, null, null);

            if (!string.IsNullOrEmpty(appsFlyerEventName) && !string.IsNullOrEmpty(paramWhere))
            {
                _afParamCache[AdsEvent.ParamAfLevel] = paramWhere;
                AppsFlyerUtils.LogEvents(appsFlyerEventName, _afParamCache);
            }
        }

        private void LogAdsEventWithLevel(string eventName, string where, int level, string appsFlyerEventName = null)
        {
            _logParamCache.Clear();
            _logParamCache[AdsEvent.ParamWhere] = where ?? "";
            _logParamCache[AdsEvent.ParamLevel] = level.ToString();
            FirebaseUtils.LogEventsAPI(eventName, _logParamCache);
            if (!string.IsNullOrEmpty(appsFlyerEventName))
            {
                _afParamCache[AdsEvent.ParamAfLevel] = level.ToString();
                AppsFlyerUtils.LogEvents(appsFlyerEventName, _afParamCache);
            }
        }

        public void LogAdsEventManager(string eventName, string adType, string placement, string failReason = null)
        {
            _logParamCache.Clear();
            _logParamCache[AdsEvent.ParamAdType] = adType;
            _logParamCache[AdsEvent.ParamPlacement] = placement ?? "";
            if (!string.IsNullOrEmpty(failReason)) _logParamCache[AdsEvent.ParamSource] = failReason;
                
            FirebaseUtils.LogEventsAPI(eventName, _logParamCache);
            GULogger.Log($"[GameUp] AdsTracker Event: {eventName} | type={adType} | place={placement} | failReason={failReason}");
        }
    }
}