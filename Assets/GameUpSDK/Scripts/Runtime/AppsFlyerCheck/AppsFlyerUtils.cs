using System.Collections.Generic;
using GameUp.Core;
#if APPSFLYER_DEPENDENCIES_INSTALLED
using AppsFlyerSDK;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// Gọi event / ad revenue AppsFlyer. SDK được khởi tạo bởi AppsFlyerObject (AppsFlyerObjectScript) — devKey và appID cấu hình trên object đó.
    /// </summary>
    public class AppsFlyerUtils : MonoSingleton<AppsFlyerUtils>
    {
#if APPSFLYER_DEPENDENCIES_INSTALLED
        /// <summary>
        /// Set AppsFlyer Customer User ID (CUID) for ROI360 matching.
        /// </summary>
        public static void SetCustomerUserId(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return;
            AppsFlyer.setCustomerUserId(userId);
        }

        /// <summary>
        /// Gửi ad revenue lên AppsFlyer bằng AFAdRevenueData.
        /// </summary>
        public static void LogAdRevenue(AFAdRevenueData adRevenueData, Dictionary<string, string> additionalParameters = null)
        {
            if (adRevenueData == null)
                return;
            AppsFlyer.logAdRevenue(adRevenueData, additionalParameters);
        }

        /// <summary>
        /// Gửi ad revenue lên AppsFlyer. Dùng enum MediationNetwork của SDK (GoogleAdMob, IronSource, ApplovinMax, ...).
        /// </summary>
        public static void LogAdRevenue(string monetizationNetwork, MediationNetwork mediationNetwork,
            double eventRevenue, string revenueCurrency, Dictionary<string, string> additionalParameters = null)
        {
            var adRevenueData = new AFAdRevenueData(monetizationNetwork, mediationNetwork, revenueCurrency, eventRevenue);
            LogAdRevenue(adRevenueData, additionalParameters);
        }

        public static void LogEvents(string eventName, Dictionary<string, string> eventValues = null)
        {
            AppsFlyer.sendEvent(eventName, eventValues);
        }
#else
        public static void SetCustomerUserId(string userId) { }

        public static void LogAdRevenue(object adRevenueData, Dictionary<string, string> additionalParameters = null) { }

        public static void LogAdRevenue(string monetizationNetwork, int mediationNetwork,
            double eventRevenue, string revenueCurrency, Dictionary<string, string> additionalParameters = null)
        { }

        public static void LogEvents(string eventName, Dictionary<string, string> eventValues = null) { }
#endif
    }
}
