using System;
using GameUp.Core;
using UnityEngine;

namespace GameUp.SDK
{
    [Serializable]
    public class AppsFlyerSettings
    {
        [Tooltip("Dev Key trên AppsFlyer dashboard.")]
        public string devKey;

        [Tooltip("App ID iOS (số trong App Store URL). Android không cần.")]
        public string appIdIOS;

        [Tooltip("Bật log debug của AppsFlyer SDK. Tắt khi release.")]
        public bool isDebug;

        [Tooltip("Nhận conversion data (deferred deeplink) qua callback của AppsFlyerObjectScript.")]
        public bool getConversionData;
    }

    [Serializable]
    public class AppMetricaSettings
    {
        [Tooltip("API Key của AppMetrica.")]
        public string apiKey;

        [Tooltip("Bật log debug của AppMetrica SDK.")]
        public bool enableLogs;

        [Tooltip("Gửi game events (level/wave/IAP/ad revenue) qua GameUpAnalytics → AppMetrica.")]
        public bool enableEventLogging = true;
    }

    /// <summary>
    /// Giá trị mặc định cho Firebase Remote Config (dùng cho SetDefaults và khi fetch lỗi).
    /// Tên field khớp key trên Firebase Console — <see cref="FirebaseRemoteConfigUtils"/> copy
    /// sang field cùng tên của mình lúc Awake, rồi Remote Config ghi đè sau khi fetch.
    /// </summary>
    [Serializable]
    public class RemoteConfigDefaults
    {
        public int inter_capping_time = 120;
        public int inter_start_level = 3;
        public bool enable_rate_app;
        public int level_start_show_rate_app = 5;
        public bool no_internet_popup_enable = true;
        public bool enable_banner = true;
        public float native_cta_click_rate = 0.3f;

        [Tooltip("ScriptableObject chứa thêm các key Remote Config riêng của dự án (tên field = key).")]
        public ScriptableObject extraData;
    }

    /// <summary>
    /// Cấu hình analytics + Remote Config của project. Cùng với <see cref="GameUpAdsConfig"/>,
    /// đây là toàn bộ dữ liệu trước kia nằm rải rác trong các prefab của package.
    /// </summary>
    [CreateAssetMenu(fileName = "GameUpSdkConfig", menuName = "GameUp/SDK Config", order = 1)]
    public class GameUpSdkConfig : ScriptableObject
    {
        public const string AssetName = "GameUpSdkConfig";
        public const string ResourcePath = GameUpAdsConfig.ResourceFolder + "/" + AssetName;

        [Header("AppsFlyer")] public AppsFlyerSettings appsFlyer = new AppsFlyerSettings();
        [Header("AppMetrica")] public AppMetricaSettings appMetrica = new AppMetricaSettings();
        [Header("Firebase Remote Config — giá trị mặc định")] public RemoteConfigDefaults remoteConfig = new RemoteConfigDefaults();

        private static GameUpSdkConfig _instance;
        private static bool _lookedUp;

        public static GameUpSdkConfig Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (_lookedUp) return null;

                _lookedUp = true;
                _instance = Resources.Load<GameUpSdkConfig>(ResourcePath);

#if UNITY_EDITOR
                if (_instance == null) _instance = GameUpConfigLookup.FindInProject<GameUpSdkConfig>();
#endif
                if (_instance == null)
                {
                    GULogger.Error("GameUp",
                        $"Không tìm thấy {AssetName}. Mở menu GameUp/SDK/Setup rồi bấm 'Save Configuration' để tạo asset tại Resources/{ResourcePath}.asset");
                }
                return _instance;
            }
        }

        public static GameUpSdkConfig Resolve(GameUpSdkConfig overrideAsset)
        {
            return overrideAsset != null ? overrideAsset : Instance;
        }

        public static void ClearCache()
        {
            _instance = null;
            _lookedUp = false;
        }
    }

#if UNITY_EDITOR
    internal static class GameUpConfigLookup
    {
        /// <summary>Fallback trong Editor: asset có thể chưa nằm trong Resources (vừa tạo / bị kéo đi chỗ khác).</summary>
        public static T FindInProject<T>() where T : ScriptableObject
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:" + typeof(T).Name);
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) return asset;
            }
            return null;
        }
    }
#endif
}
