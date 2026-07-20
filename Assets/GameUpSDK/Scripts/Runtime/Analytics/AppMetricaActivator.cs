#if APPMETRICA_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
using Io.AppMetrica;
#endif
using GameUp.Core;
using UnityEngine;


namespace GameUp.SDK
{
    public class AppMetricaActivator : MonoSingleton<AppMetricaActivator>
    {
        [SerializeField] private string apiKey;
        [SerializeField] private bool enableLogs;
        [SerializeField] private bool enableEventLogging = true;

        /// <summary>Bật gửi game events qua <see cref="AppMetricaUtils"/> / <see cref="GameUpAnalytics"/>.</summary>
        public static bool EnableEventLogging =>
#if APPMETRICA_DEPENDENCIES_INSTALLED
            Instance != null && Instance.enableEventLogging;
#else
            false;
#endif

        /// <summary>Bật <c>Debug.Log</c> xác nhận gửi event trong <see cref="AppMetricaUtils"/> (tab Appmetrica → SDK debug logs).</summary>
        public static bool IsUtilsDebugLogEnabled =>
#if APPMETRICA_DEPENDENCIES_INSTALLED
            Instance != null && Instance.enableLogs;
#else
            false;
#endif
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Activate()
        {
#if APPMETRICA_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
            AppMetrica.Activate(new AppMetricaConfig(Instance.apiKey)
            {
                FirstActivationAsUpdate = !IsFirstLaunch(),
                Logs = Instance.enableLogs,
            });
#endif
        }

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