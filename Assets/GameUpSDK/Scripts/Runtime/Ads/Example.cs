using System;
using GameUp.SDK;
using UnityEngine;

namespace GameUp.SDK
{
    public class Example : MonoBehaviour
    {
        private void Start()
        {
            FirebaseRemoteConfigUtils.Instance.OnFetchCompleted += OnRemoteConfigFetched;
        }

        private void OnRemoteConfigFetched(bool b)
        {
            SetUpAdsCondition();
        }

        private int GetLevel()
        {
            return PlayerPrefs.GetInt("Level", 1);
        }

        private void SetUpAdsCondition()
        {
            // Đặt điều kiện thời gian tối thiểu giữa 2 lần hiện interstitial liên tiếp
            AdsManager.Instance.AddCondition(new CappingTimeCondition(AdUnitType.Interstitial,
                FirebaseRemoteConfigUtils.Instance.inter_capping_time, 0));

            // Đặt điều kiện Level tối thiểu có thể hiện quảng cáo, so sánh <
            AdsManager.Instance.AddCondition(new MinLevelCondition(FirebaseRemoteConfigUtils.Instance.inter_start_level,
                GetLevel));

            // Đặt điều kiện không được hiện AOA sau các loại Ads
            AdsManager.Instance.AddCondition(new CrossFormatCooldownCondition(AdUnitType.AppOpen,
                AdUnitType.RewardedVideo, 10));
            AdsManager.Instance.AddCondition(new CrossFormatCooldownCondition(AdUnitType.AppOpen,
                AdUnitType.Interstitial, 10));
            
            AdsManager.Instance.AddCondition(new ShowAdByKey(AdUnitType.Banner, () => !FirebaseRemoteConfigUtils.Instance.enable_banner));
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                // Không hiện AOA khi đang có Ads khác ngoài Banner
                if (!AdCappingManager.Instance.IsAnyAdShowing)
                {
                    AdsManager.Instance.ShowAppOpenAds();
                }
            }
        }
    }

    public class ShowAdByKey : IAdCondition
    {
        private readonly AdUnitType _adType;
        private readonly Func<bool> _funcCheckHideAd;

        public ShowAdByKey(AdUnitType adType, Func<bool> funcCheckHideAd)
        {
            _adType = adType;
            _funcCheckHideAd = funcCheckHideAd;
        }

        public bool CanShow(AdUnitType adType, string where, out string reason)
        {
            if (adType == _adType)
            {
                if (_funcCheckHideAd.Invoke())
                {
                    reason = $"ShowAdByKey: {adType} - {_funcCheckHideAd.Invoke()}";
                    return false;
                }
            }
            reason = string.Empty;
            return true;
        }

        public string GetString()
        {
            return "Condition: ShowAdByKey";
        }
    }
}