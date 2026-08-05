using System;
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
    /// Log có phân cấp.
    /// Verbose/Log/Warning bị strip khỏi build release (chỉ biên dịch khi có UNITY_EDITOR hoặc ENABLE_LOG)
    /// nên chuỗi truyền vào cũng không bị dựng — không tốn GC trên máy thật.
    /// Error/Exception LUÔN được biên dịch để crash reporter (Crashlytics...) còn thấy sự cố trên device;
    /// muốn tắt hẳn thì set <see cref="MinLevel"/> = <see cref="LogLevel.None"/>.
    /// </summary>
    public static class GULogger
    {
        // Tự động set level mặc định dựa trên môi trường ngay khi compile
#if UNITY_EDITOR
        public static LogLevel MinLevel { get; set; } = LogLevel.Verbose;
#else
        public static LogLevel MinLevel { get; set; } = LogLevel.Warning;
#endif

        // Cho phép các project đổi Log Level bằng code (ví dụ từ một tool Debug in-game)
        public static void SetLogLevel(LogLevel level)
        {
            MinLevel = level;
        }

        public static bool IsLoggable(LogLevel level)
        {
            return level >= MinLevel;
        }

        [Conditional("ENABLE_LOG"), Conditional("UNITY_EDITOR")]
        public static void Log(string tag, string message)
        {
            if (!IsLoggable(LogLevel.Info)) return;
            Debug.Log(FormatMessage(tag, message));
        }

        [Conditional("ENABLE_LOG"), Conditional("UNITY_EDITOR")]
        public static void Log(string message)
        {
            if (!IsLoggable(LogLevel.Info)) return;
            Debug.Log(message);
        }

        [Conditional("ENABLE_LOG"), Conditional("UNITY_EDITOR")]
        public static void Verbose(string tag, string message)
        {
            if (!IsLoggable(LogLevel.Verbose)) return;
            Debug.Log($"<color=#888888>{FormatMessage(tag, message)}</color>");
        }

        [Conditional("ENABLE_LOG"), Conditional("UNITY_EDITOR")]
        public static void Warning(string tag, string message)
        {
            if (!IsLoggable(LogLevel.Warning)) return;
            Debug.LogWarning(FormatMessage(tag, message));
        }

        /// <summary>Không strip trong build: lỗi trên device phải nhìn thấy được.</summary>
        public static void Error(string tag, string message)
        {
            if (!IsLoggable(LogLevel.Error)) return;
            Debug.LogError(FormatMessage(tag, message));
        }

        /// <inheritdoc cref="Error"/>
        public static void Exception(Exception exception, string tag = "Exception")
        {
            if (exception == null) return;
            if (!IsLoggable(LogLevel.Error)) return;

            Debug.LogError(FormatMessage(tag, exception.Message));
            Debug.LogException(exception);
        }

        static string FormatMessage(string tag, string message)
            => $"[{tag}] {message}";
    }
}
