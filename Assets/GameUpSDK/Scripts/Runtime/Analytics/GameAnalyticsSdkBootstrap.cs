using UnityEngine;
#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
using GameAnalyticsSDK;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// Gá»i <c>GameAnalytics.Initialize()</c> theo bÆ°á»›c 2.5 tÃ i liá»‡u GA Unity â€” SDK khÃ´ng tá»± init trong <c>Awake</c>.
    /// DÃ¹ng <see cref="RuntimeInitializeLoadType.AfterSceneLoad"/> Ä‘á»ƒ cháº¡y sau <c>Awake</c> cá»§a GameObject GameAnalytics (náº¿u cÃ³ trong scene).
    /// Váº«n cáº§n <c>Resources/GameAnalytics/Settings</c> (game key / secret) vÃ  nÃªn cÃ³ má»™t GameObject GameAnalytics (menu Window â†’ GameAnalytics).
    /// </summary>
    public static class GameAnalyticsSdkBootstrap
    {
        private const string LogTag = "[GameUpSDK][GameAnalytics]";

#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeAfterFirstScene()
        {
            TryInitialize();
        }
#endif

        /// <summary>
        /// Khá»Ÿi táº¡o GameAnalytics má»™t láº§n. An toÃ n gá»i láº¡i. TrÃªn iOS 14.5+, náº¿u cáº§n ATT trÆ°á»›c khi init Ä‘á»ƒ IDFA,
        /// xem <see href="https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/">GA Unity â€” ATT</see>
        /// (dÃ¹ng <c>RequestTrackingAuthorization</c> rá»“i <c>Initialize</c> thay vÃ¬ chá»‰ dá»±a vÃ o auto bootstrap).
        /// </summary>
        public static void TryInitialize()
        {
#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
            if (GameAnalytics.Initialized)
                return;

            GameAnalytics.Initialize();
            Debug.Log(LogTag + " ÄÃ£ gá»i GameAnalytics.Initialize() (platform: " + Application.platform + ").");
#endif
        }
    }
}
