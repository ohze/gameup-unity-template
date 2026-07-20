namespace GameUp.SDK
{
    /// <summary>
    /// Event names và params gửi lên AppMetrica (Product Analytics / IDLE spec).
    /// </summary>
    public static class AppMetricaEvent
    {
        public const string LevelStart = "level_start";
        public const string LevelFinish = "level_finish";

        public const string VideoAdsAvailable = "video_ads_available";
        public const string VideoAdsStarted = "video_ads_started";
        public const string VideoAdsWatch = "video_ads_watch";

        public const string AfAdRevenue = "af_ad_revenue";
        public const string AfPurchase = "af_purchase";

        public const string ParamPlacement = "placement";
        public const string ParamAdType = "ad_type";
        public const string ParamResult = "result";
        public const string ParamConnection = "connection";

        public const string AdTypeRewarded = "rewarded";
        public const string AdTypeInterstitial = "interstitial";

        public const string ResultSuccess = "success";
        public const string ResultNotAvailable = "not_available";
        public const string ResultStart = "start";
        public const string ResultFailed = "failed";
        public const string ResultWatched = "watched";
        public const string ResultCanceled = "canceled";

        public const string ParamAfRevenue = "af_revenue";
        public const string ParamAfCurrency = "af_currency";
        public const string ParamMonetizationNetwork = "monetization_network";
        public const string ParamAdUnit = "ad_unit";
    }
}
