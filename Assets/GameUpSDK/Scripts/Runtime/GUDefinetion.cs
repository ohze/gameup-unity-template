namespace GameUp.SDK
{
    public class GUDefinetion
    {
        // Backward-compat define (không nên dùng để include SDK bên thứ 3 nữa).
        public const string DepsReadyDefine = "GAMEUP_SDK_DEPS_READY";

        // Per-provider dependency defines (được set tự động bởi GameUpDependenciesWindow).
        public const string FirebaseDepsInstalled = "FIREBASE_DEPENDENCIES_INSTALLED";
        public const string AppsFlyerDepsInstalled = "APPSFLYER_DEPENDENCIES_INSTALLED";
        public const string GameAnalyticsDepsInstalled = "GAMEANALYTICS_DEPENDENCIES_INSTALLED";
        public const string AdMobDepsInstalled = "ADMOB_DEPENDENCIES_INSTALLED";
        public const string LevelPlayDepsInstalled = "LEVELPLAY_DEPENDENCIES_INSTALLED";
        public const string FacebookDepsInstalled = "FACEBOOK_DEPENDENCIES_INSTALLED";
        public const string MaxDepsInstalled = "MAXSDK_DEPENDENCIES_INSTALLED";
        public const string AppMetricaDepsInstalled = "APPMETRICA_DEPENDENCIES_INSTALLED";
        public const string AdjustDepsInstalled = "ADJUST_DEPENDENCIES_INSTALLED";

        /// <summary>MMP (attribution) được chọn ở Setup Dependencies — quyết định cài AppsFlyer hay Adjust. Mặc định AppsFlyer.</summary>
        public const string MmpAppsFlyer = "GAMEUP_MMP_APPSFLYER";
        public const string MmpAdjust = "GAMEUP_MMP_ADJUST";

        // Define "Primary Mediation" cũ: installer giờ cho chọn nhiều mạng cùng lúc (thứ tự ở GameUpAdsConfig.mediationPriority)
        // nên không còn set nữa. Giữ hằng để project cũ migrate lựa chọn và để nút "Dọn define cũ" gỡ được.
        private const string PrimaryMediationObsolete =
            "Không còn dùng: mạng quảng cáo đang chạy = các SDK đã cài (*_DEPENDENCIES_INSTALLED), thứ tự ở GameUpAdsConfig.mediationPriority.";

        [System.Obsolete(PrimaryMediationObsolete)] public const string PrimaryMediationLevelPlay = "GAMEUP_PRIMARY_MEDIATION_LEVELPLAY";
        [System.Obsolete(PrimaryMediationObsolete)] public const string PrimaryMediationAdMob = "GAMEUP_PRIMARY_MEDIATION_ADMOB";
        [System.Obsolete(PrimaryMediationObsolete)] public const string PrimaryMediationMax = "GAMEUP_PRIMARY_MEDIATION_MAX";
    }
}