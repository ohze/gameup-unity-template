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

        /// <summary>Chỉ một trong hai được bật; do GameUpDependenciesWindow set — phù hợp khi SDK là UPM package (không cần asset trong Assets/).</summary>
        public const string PrimaryMediationLevelPlay = "GAMEUP_PRIMARY_MEDIATION_LEVELPLAY";
        public const string PrimaryMediationAdMob = "GAMEUP_PRIMARY_MEDIATION_ADMOB";
    }
}