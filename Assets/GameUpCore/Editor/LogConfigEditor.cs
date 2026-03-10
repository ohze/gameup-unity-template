using UnityEditor;
using GameUp.Core;

namespace GameUp.Core.Editor
{
    [CustomEditor(typeof(LogConfig))]
    public sealed class LogConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (LogConfig)target;
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                $"Current log level: {config.MinLogLevel}\n" +
                "Logs below this level will be filtered out.\n" +
                "In release builds, all logs are stripped unless ENABLE_LOG is defined.",
                MessageType.Info);
        }
    }
}
