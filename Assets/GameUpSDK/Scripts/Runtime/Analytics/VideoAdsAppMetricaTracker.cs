using System.Collections.Generic;
using UnityEngine;

namespace GameUp.SDK
{
    /// <summary>
    /// Trạng thái funnel video_ads_* theo IDLE / more details (available → started → watch).
    /// </summary>
    internal static class VideoAdsAppMetricaTracker
    {
        private const float NotAvailableCooldownSeconds = 60f;

        private static readonly Dictionary<string, float> RewardedNotAvailableCooldownUntil =
            new Dictionary<string, float>();

        private static string _sessionPlacement;
        private static string _sessionAdType;
        private static bool _sessionAvailableSuccess;

        public static string NormalizePlacement(string where)
        {
            if (string.IsNullOrWhiteSpace(where)) return "unknown";
            return where.Trim().ToLowerInvariant().Replace(' ', '_');
        }

        public static bool HasInternetConnection()
        {
            return Application.internetReachability != NetworkReachability.NotReachable;
        }

        /// <summary>
        /// Rewarded: CD 60s cho lặp <c>not_available</c> cùng placement (spec more details).
        /// Interstitial: luôn gửi.
        /// </summary>
        public static bool ShouldSendAvailable(string placement, string adType, string result)
        {
            if (result != AppMetricaEvent.ResultNotAvailable) return true;
            if (adType != AppMetricaEvent.AdTypeRewarded) return true;

            float now = Time.realtimeSinceStartup;
            if (RewardedNotAvailableCooldownUntil.TryGetValue(placement, out float until) && now < until)
                return false;

            RewardedNotAvailableCooldownUntil[placement] = now + NotAvailableCooldownSeconds;
            return true;
        }

        public static void BeginShowSession(string placement, string adType, bool availableSuccess)
        {
            _sessionPlacement = placement;
            _sessionAdType = adType;
            _sessionAvailableSuccess = availableSuccess;
        }

        public static bool CanSendStarted(string placement, string adType)
        {
            return _sessionAvailableSuccess
                   && _sessionPlacement == placement
                   && _sessionAdType == adType;
        }

        public static void ClearSession()
        {
            _sessionPlacement = null;
            _sessionAdType = null;
            _sessionAvailableSuccess = false;
        }
    }
}
