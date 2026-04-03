using UnityEngine;
#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
using GameAnalyticsSDK;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// Gọi <c>GameAnalytics.Initialize()</c> theo bước 2.5 tài liệu GA Unity — SDK không tự init trong <c>Awake</c>.
    /// Dùng <see cref="RuntimeInitializeLoadType.AfterSceneLoad"/> để chạy sau <c>Awake</c> của GameObject GameAnalytics (nếu có trong scene).
    /// Vẫn cần <c>Resources/GameAnalytics/Settings</c> (game key / secret) và nên có một GameObject GameAnalytics (menu Window → GameAnalytics).
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
        /// Khởi tạo GameAnalytics một lần. An toàn gọi lại. Trên iOS 14.5+, nếu cần ATT trước khi init để IDFA,
        /// xem <see href="https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/">GA Unity — ATT</see>
        /// (dùng <c>RequestTrackingAuthorization</c> rồi <c>Initialize</c> thay vì chỉ dựa vào auto bootstrap).
        /// </summary>
        public static void TryInitialize()
        {
#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
            if (GameAnalytics.Initialized)
                return;

            GameAnalytics.Initialize();
            Debug.Log(LogTag + " Đã gọi GameAnalytics.Initialize() (platform: " + Application.platform + ").");
#endif
        }
    }
}
