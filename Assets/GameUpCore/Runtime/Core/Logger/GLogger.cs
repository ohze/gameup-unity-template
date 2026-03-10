using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GameUp.Core
{
    public enum LogLevel
    {
        Verbose = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        None = 99
    }

    /// <summary>
    /// Custom tagged logger with conditional compilation.
    /// All log calls are stripped from release builds unless ENABLE_LOG is defined.
    /// </summary>
    public static class GLogger
    {
        public static LogLevel MinLevel { get; set; } = LogLevel.Verbose;

        [Conditional("ENABLE_LOG"), Conditional("UNITY_EDITOR")]
        public static void Log(string tag, string message)
        {
            if (MinLevel > LogLevel.Info) return;
            Debug.Log(FormatMessage(tag, message));
        }

        [Conditional("ENABLE_LOG"), Conditional("UNITY_EDITOR")]
        public static void Log(string message)
        {
            if (MinLevel > LogLevel.Info) return;
            Debug.Log(message);
        }

        [Conditional("ENABLE_LOG"), Conditional("UNITY_EDITOR")]
        public static void Verbose(string tag, string message)
        {
            if (MinLevel > LogLevel.Verbose) return;
            Debug.Log($"<color=#888888>{FormatMessage(tag, message)}</color>");
        }

        [Conditional("ENABLE_LOG"), Conditional("UNITY_EDITOR")]
        public static void Warning(string tag, string message)
        {
            if (MinLevel > LogLevel.Warning) return;
            Debug.LogWarning(FormatMessage(tag, message));
        }

        [Conditional("ENABLE_LOG"), Conditional("UNITY_EDITOR")]
        public static void Error(string tag, string message)
        {
            if (MinLevel > LogLevel.Error) return;
            Debug.LogError(FormatMessage(tag, message));
        }

        static string FormatMessage(string tag, string message)
            => $"[{tag}] {message}";
    }
}
