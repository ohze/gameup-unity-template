using System.Collections.Generic;
using UnityEngine;

namespace GameUp.SDK
{
    public class CappingTimeCondition : IAdCondition
    {
        private readonly AdUnitType _adUnitType;

        public CappingTimeCondition(AdUnitType adUnitType = AdUnitType.Interstitial, float cappingTime = 60f, float startCounter = 60f)
        {
            _adUnitType = adUnitType;
            AdCappingManager.Instance.SetCappingLimit(_adUnitType, cappingTime, startCounter);
        }

        public bool CanShow(AdUnitType adType, string where, out string reason)
        {
            if (adType == _adUnitType)
            {
                if (!AdCappingManager.Instance.IsCappingReady(_adUnitType))
                {
                    reason = $"capping_not_ready_{_adUnitType}";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        public string GetString()
        {
            return $"Condition: Capping Time: {_adUnitType}";
        }
    }

    public class MinLevelCondition : IAdCondition
    {
        private readonly System.Func<int> _getCurrentLevelFunc;
        private readonly int _minLevelRequired;

        public MinLevelCondition(int minLevelRequired, System.Func<int> getCurrentLevelFunc)
        {
            _minLevelRequired = minLevelRequired;
            _getCurrentLevelFunc = getCurrentLevelFunc;
        }

        public bool CanShow(AdUnitType adType, string where, out string reason)
        {
            if (adType == AdUnitType.Interstitial)
            {
                var currentLevel = _getCurrentLevelFunc?.Invoke() ?? int.MaxValue;
                if (currentLevel < _minLevelRequired)
                {
                    reason = $"level_too_low_({currentLevel}<{_minLevelRequired})";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        public string GetString()
        {
            return $"Condition: Min Level: {_minLevelRequired}";
        }
    }

    public class RemoveAdCondition : IAdCondition
    {
        private readonly List<AdUnitType> _adUnitTypes;
        private readonly System.Func<bool> _getRemoveAdFunc;

        public RemoveAdCondition(List<AdUnitType> removeAdUnits, System.Func<bool> getRemoveAdFunc)
        {
            _getRemoveAdFunc = getRemoveAdFunc;
            _adUnitTypes = removeAdUnits;
        }

        public bool CanShow(AdUnitType adType, string where, out string reason)
        {
            if (_adUnitTypes.Contains(adType) && _getRemoveAdFunc.Invoke())
            {
                reason = $"can remove_ad_{adType}";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public string GetString()
        {
            return $"Condition: Remove: {string.Join(", ", _adUnitTypes)}";
        }
    }

    public class CrossFormatCooldownCondition : IAdCondition
    {
        private readonly AdUnitType _targetAdType; // Loại quảng cáo bị chặn (VD: Interstitial)
        private readonly AdUnitType _triggerAdType; // Loại quảng cáo làm mốc (VD: RewardedVideo)
        private readonly float _cooldownSeconds; // Thời gian cần phải chờ (VD: 30 giây)

        public CrossFormatCooldownCondition(AdUnitType targetAdType, AdUnitType triggerAdType, float cooldownSeconds)
        {
            _targetAdType = targetAdType;
            _triggerAdType = triggerAdType;
            _cooldownSeconds = cooldownSeconds;
        }

        public bool CanShow(AdUnitType adType, string where, out string reason)
        {
            if (adType == _targetAdType)
            {
                var timeSinceTriggerClosed = AdHistoryTracker.GetTimeSinceLastClosed(_triggerAdType);

                if (timeSinceTriggerClosed < _cooldownSeconds)
                {
                    reason = $"cooldown_from_{_triggerAdType}_({timeSinceTriggerClosed:F1}s < {_cooldownSeconds}s)";
                    return false; // Chặn hiển thị!
                }
            }

            reason = string.Empty;
            return true;
        }

        public string GetString()
        {
            return $"Cross format cooldown: {_cooldownSeconds}";
        }
    }

    public class HideBannerFromRemote : IAdCondition
    {
        private readonly System.Func<bool> _getHideBannerFunc;

        public HideBannerFromRemote(System.Func<bool> getHideBannerFunc)
        {
            _getHideBannerFunc = getHideBannerFunc;
        }
        
        public bool CanShow(AdUnitType adType, string where, out string reason)
        {
            if (adType == AdUnitType.Banner)
            {
                if (_getHideBannerFunc.Invoke())
                {
                    reason = $"can hide_banner_{where}";
                    return false;
                }
            }
            reason = string.Empty;
            return true;
        }

        public string GetString()
        {
            return $"Condition: Hide banner: {_getHideBannerFunc?.Invoke()}";
        }
    }
}