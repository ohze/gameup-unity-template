using UnityEngine;
#if FACEBOOK_DEPENDENCIES_INSTALLED && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
using Facebook.Unity;
using Facebook.Unity.Settings;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// Gọi <c>FB.Init</c> sớm trên Android/iOS để Facebook SDK bật và gửi app events (kể cả App Launch khi
    /// <see href="https://developers.facebook.com/docs/unity/getting-started/android">cấu hình Android đúng</see>:
    /// Bundle ID, key hash, class name trên Meta Developer Console; <c>FacebookSettings</c> trong Unity).
    /// Auto log app events lấy từ FacebookSettings (mặc định bật). Chỉ compile khi <c>FACEBOOK_DEPENDENCIES_INSTALLED</c>.
    /// </summary>
    public static class FacebookSdkBootstrap
    {
        private const string LogTag = "[GameUpSDK][Facebook]";

        /// <summary>True sau khi callback Init báo SDK sẵn sàng (build Android/iOS, có define Facebook).</summary>
        public static bool IsInitialized
        {
            get
            {
#if FACEBOOK_DEPENDENCIES_INSTALLED && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
                return FB.IsInitialized;
#else
                return false;
#endif
            }
        }

#if FACEBOOK_DEPENDENCIES_INSTALLED && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            TryInitialize();
        }
#endif

        /// <summary>
        /// Khởi tạo Facebook SDK (App Id / Client Token từ FacebookSettings). An toàn gọi lại: đã init thì gọi <c>ActivateApp</c>.
        /// </summary>
        public static void TryInitialize()
        {
#if FACEBOOK_DEPENDENCIES_INSTALLED && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            if (!FacebookSettings.IsValidAppId)
            {
                Debug.LogWarning(LogTag + " Bỏ qua FB.Init — App Id chưa hợp lệ (Facebook → Edit Settings / GameUp Setup).");
                return;
            }

            if (FB.IsInitialized)
            {
                FB.ActivateApp();
                return;
            }

            FB.Init(OnFacebookInitComplete);
#endif
        }

#if FACEBOOK_DEPENDENCIES_INSTALLED && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        private static void OnFacebookInitComplete()
        {
            if (!FB.IsInitialized)
            {
                Debug.LogWarning(LogTag + " FB.Init hoàn tất nhưng FB.IsInitialized = false.");
                return;
            }

            FB.ActivateApp();
            Debug.Log(LogTag + " SDK đã init — app activation / app events (Meta Event Testing) có thể hiển thị sau vài phút.");
        }
#endif
    }
}
