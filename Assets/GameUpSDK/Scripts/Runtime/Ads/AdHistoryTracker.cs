using System.Collections.Generic;
using UnityEngine;

namespace GameUp.SDK
{
    public static class AdHistoryTracker
    {
        private static readonly Dictionary<AdUnitType, float> LastClosedRealTime = new Dictionary<AdUnitType, float>();

        public static void MarkAdClosed(AdUnitType adType)
        {
            LastClosedRealTime[adType] = Time.unscaledTime;
        }

        public static float GetTimeSinceLastClosed(AdUnitType adType)
        {
            if (LastClosedRealTime.TryGetValue(adType, out var lastTime))
            {
                return Time.unscaledTime - lastTime;
            }

            return 999999f;
        }
    }
}