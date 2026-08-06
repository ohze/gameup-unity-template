#if APPMETRICA_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
using Io.AppMetrica;
#endif
using GameUp.Core;
using UnityEngine;


namespace GameUp.SDK
{
    public class AppMetricaActivator : MonoSingleton<AppMetricaActivator>
    {
        [Tooltip("Để trống = dùng asset GameUpSdkConfig chung của project (Resources/GameUpSDK/GameUpSdkConfig).")]
        [SerializeField] private GameUpSdkConfig configOverride;

        // --- LEGACY: dữ liệu cũ nằm trong prefab, chỉ còn dùng cho migrate ---
        [HideInInspector] [SerializeField] private string apiKey;
        [HideInInspector] [SerializeField] private bool enableLogs;
        [HideInInspector] [SerializeField] private bool enableEventLogging = true;

        private AppMetricaSettings Settings => GameUpSdkConfig.Resolve(configOverride)?.appMetrica;

        /// <summary>Bật gửi game events qua <see cref="AppMetricaUtils"/> / <see cref="GameUpAnalytics"/>.</summary>
        public static bool EnableEventLogging =>
#if APPMETRICA_DEPENDENCIES_INSTALLED
            Instance != null && Instance.Settings != null && Instance.Settings.enableEventLogging;
#else
            false;
#endif

        /// <summary>Bật <c>Debug.Log</c> xác nhận gửi event trong <see cref="AppMetricaUtils"/> (tab Appmetrica → SDK debug logs).</summary>
        public static bool IsUtilsDebugLogEnabled =>
#if APPMETRICA_DEPENDENCIES_INSTALLED
            Instance != null && Instance.Settings != null && Instance.Settings.enableLogs;
#else
            false;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Activate()
        {
#if APPMETRICA_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
            var settings = Instance != null ? Instance.Settings : null;
            if (settings == null || string.IsNullOrEmpty(settings.apiKey))
            {
                GULogger.Warning("GameUp", "AppMetricaActivator: thiếu API key trong GameUpSdkConfig, bỏ qua activate.");
                return;
            }

            AppMetrica.Activate(new AppMetricaConfig(settings.apiKey)
            {
                FirstActivationAsUpdate = !IsFirstLaunch(),
                Logs = settings.enableLogs,
            });
#endif
        }

#if UNITY_EDITOR
        /// <summary>Xuất dữ liệu cũ trong prefab (chỉ dùng cho công cụ migrate).</summary>
        public AppMetricaSettings ExportLegacySettings() => new AppMetricaSettings
        {
            apiKey = apiKey,
            enableLogs = enableLogs,
            enableEventLogging = enableEventLogging
        };
#endif

        private static bool IsFirstLaunch()
        {
            if (PlayerPrefs.HasKey("FirstLaunch"))
            {
                return false;
            }
            else
            {
                PlayerPrefs.SetInt("FirstLaunch", 1);
                return true;
            }
        }
    }
}