using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// ScriptableObject to configure log level at runtime.
    /// Create via Assets > Create > GameUp > Log Config.
    /// </summary>
    [CreateAssetMenu(fileName = "LogConfig", menuName = "GameUp/Log Config")]
    public sealed class LogConfig : ScriptableObject
    {
        [SerializeField] LogLevel _minLogLevel = LogLevel.Verbose;

        public LogLevel MinLogLevel => _minLogLevel;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            GLogger.MinLevel = _minLogLevel;
        }
    }
}
