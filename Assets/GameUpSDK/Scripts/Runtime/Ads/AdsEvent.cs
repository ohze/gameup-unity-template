using System;

namespace GameUp.SDK
{
    /// <summary>
    /// DTO for ad impression data (ARM). Used when forwarding IronSource/LevelPlay impression data to GameUpAnalytics.
    /// </summary>
    public class AdImpressionData
    {
        public string AdNetwork { get; set; }
        public string AdUnit { get; set; }
        public string InstanceName { get; set; }
        public string AdFormat { get; set; }
        public double? Revenue { get; set; }
    }

    /// <summary>
    /// Centralized Firebase event names and parameter names for ads logging.
    /// PascalCase for identifiers, snake_case for values.
    /// </summary>
    public static class AdsEvent
    {
        /// <summary>
        /// Fired when an ad impression with revenue is ready (e.g. IronSource/LevelPlay OnAdInfoChanged with revenue).
        /// Subscribe in AdsManager and pass to GameUpAnalytics.LogAdImpression.
        /// </summary>
        public static event Action<AdImpressionData> OnImpressionDataReady;
        internal static void RaiseImpressionDataReady(AdImpressionData data) => OnImpressionDataReady?.Invoke(data);
        // Interstitial Events
        public const string InterStartLoad = "ad_inter_start_load";
        public const string InterCompleteLoad = "ad_inter_complete_load";
        public const string InterLoadFail = "ad_inter_load_fail";
        public const string InterShow = "ad_inter_show";
        public const string InterShowComplete = "ad_inter_show_complete";

        // Rewarded Events
        public const string RewardStartLoad = "ad_rewarded_start_load";
        public const string RewardCompleteLoad = "ad_rewarded_complete_load";
        public const string RewardLoadFail = "ad_rewarded_load_fail";
        public const string RewardShow = "ad_rewarded_show";
        public const string RewardShowComplete = "ad_rewarded_show_complete";

        // Manager / waterfall events (AdsManager)
        public const string AdsRequest = "ads_request";
        public const string AdsAvailable = "ads_available";
        public const string AdsShowSuccess = "ads_show_success";
        public const string AdsShowFail = "ads_show_fail";

        // AppsFlyer Events
        public const string AfInterShow = "af_inters_show";
        public const string AfInterDisplayed = "af_inters_displayed";
        public const string AfRewardShow = "af_rewarded_show";
        public const string AfRewardDisplayed = "af_rewarded_displayed";

        // Parameters
        public const string ParamWhere = "where";
        public const string ParamLevel = "level";
        public const string ParamSource = "source";
        public const string ParamAdType = "ad_type";
        public const string ParamPlacement = "placement";
        public const string ParamAfLevel = "af_level";

        // Ad type values (for ParamAdType / placement context)
        public const string AdTypeBanner = "banner";
        public const string AdTypeInterstitial = "interstitial";
        public const string AdTypeRewardedVideo = "rewarded_video";
        public const string AdTypeAppOpen = "app_open";
    }
}
