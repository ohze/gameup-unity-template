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