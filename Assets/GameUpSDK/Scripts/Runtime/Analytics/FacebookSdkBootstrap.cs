using UnityEngine;
#if FACEBOOK_DEPENDENCIES_INSTALLED && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
using Facebook.Unity;
using Facebook.Unity.Settings;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// Gá»i <c>FB.Init</c> sá»›m trÃªn Android/iOS Ä‘á»ƒ Facebook SDK báº­t vÃ  gá»­i app events (ká»ƒ cáº£ App Launch khi
    /// <see href="https://developers.facebook.com/docs/unity/getting-started/android">cáº¥u hÃ¬nh Android Ä‘Ãºng</see>:
    /// Bundle ID, key hash, class name trÃªn Meta Developer Console; <c>FacebookSettings</c> trong Unity).
    /// Auto log app events láº¥y tá»« FacebookSettings (máº·c Ä‘á»‹nh báº­t). Chá»‰ compile khi <c>FACEBOOK_DEPENDENCIES_INSTALLED</c>.
    /// </summary>
    public static class FacebookSdkBootstrap
    {
        private const string LogTag = "[GameUpSDK][Facebook]";

        /// <summary>True sau khi callback Init bÃ¡o SDK sáºµn sÃ ng (build Android/iOS, cÃ³ define Facebook).</summary>
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
        /// Khá»Ÿi táº¡o Facebook SDK (App Id / Client Token tá»« FacebookSettings). An toÃ n gá»i láº¡i: Ä‘Ã£ init thÃ¬ gá»i <c>ActivateApp</c>.
        /// </summary>
        public static void TryInitialize()
        {
#if FACEBOOK_DEPENDENCIES_INSTALLED && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            if (!FacebookSettings.IsValidAppId)
            {
                Debug.LogWarning(LogTag + " Bá» qua FB.Init â€” App Id chÆ°a há»£p lá»‡ (Facebook â†’ Edit Settings / GameUp Setup).");
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
                Debug.LogWarning(LogTag + " FB.Init hoÃ n táº¥t nhÆ°ng FB.IsInitialized = false.");
                return;
            }

            FB.ActivateApp();
            Debug.Log(LogTag + " SDK Ä‘Ã£ init â€” app activation / app events (Meta Event Testing) cÃ³ thá»ƒ hiá»ƒn thá»‹ sau vÃ i phÃºt.");
        }
#endif
    }
}
