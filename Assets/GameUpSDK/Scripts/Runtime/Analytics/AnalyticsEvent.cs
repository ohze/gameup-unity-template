namespace GameUp.SDK
{
    /// <summary>
    /// Event names and parameter names for game analytics: Firebase, AppsFlyer (MMP); GameAnalytics progression (level/wave) map trong <see cref="GameUpAnalytics"/>.
    /// Ad-related events are handled elsewhere.
    /// </summary>
    public static class AnalyticsEvent
    {
        // ---------- Firebase Events ----------

        public const string EarnVirtualCurrency = "earn_virtual_currency";
        public const string SpendVirtualCurrency = "spend_virtual_currency";
        public const string StartLoading = "start_loading";
        public const string CompleteLoading = "complete_loading";
        public const string LevelStart = "level_start";
        public const string LevelFail = "level_fail";
        public const string LevelComplete = "level_complete";
        public const string ButtonClick = "button_click";
        public const string WaveStart = "wave_start";
        public const string WaveFail = "wave_fail";
        public const string WaveComplete = "wave_complete";
        public const string StartLevel1 = "start_level_1";
        public const string CompleteLevel1 = "complete_level_1";

        // ---------- AppsFlyer Events ----------

        public const string AfCompleteRegistration = "af_complete_registration";
        public const string AfLevelAchieved = "af_level_achieved";
        public const string AfPurchase = "af_purchase";
        public const string AfTutorialCompletion = "af_tutorial_completion";
        public const string AfAchievementUnlocked = "af_achievement_unlocked";

        // ---------- Firebase params ----------

        public const string ParamVirtualCurrencyName = "virtual_currency_name";
        public const string ParamValue = "value";
        public const string ParamSource = "source";
        public const string ParamLevel = "level";
        public const string ParamIndex = "index";
        public const string ParamTime = "time";
        public const string ParamWave = "wave";

        // ---------- AppsFlyer params ----------

        public const string ParamAfLevel = "af_level";
        public const string ParamAfScore = "af_score";
        public const string ParamAfRegistrationMethod = "af_registration_method";
        public const string ParamAfCustomerUserId = "af_customer_user_id";
        public const string ParamAfCurrencyCode = "af_currency_code";
        public const string ParamAfQuantity = "af_quantity";
        public const string ParamAfContentId = "af_content_id";
        public const string ParamAfPurchasePrice = "af_purchase_price";
        public const string ParamAfOrderId = "af_order_id";
        public const string ParamAfSuccess = "af_success";
        public const string ParamAfTutorialId = "af_tutorial_id";
        public const string ParamContentId = "content_id";
    }
}
