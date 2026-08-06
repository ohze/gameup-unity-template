using GameUp.SDK;
using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    [CustomEditor(typeof(GameUpSdkConfig))]
    public class GameUpSdkConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Cấu hình AppsFlyer / AppMetrica / Remote Config của project. " +
                "Sửa tại đây hoặc trong cửa sổ GameUp/SDK/Setup đều như nhau.",
                MessageType.Info);

            var path = AssetDatabase.GetAssetPath(target).Replace('\\', '/');
            if (!path.Contains("/Resources/"))
            {
                EditorGUILayout.HelpBox(
                    "Asset phải nằm trong thư mục Resources thì bản build mới load được.\nGợi ý: " +
                    GameUpSetupPaths.ConfigFolder, MessageType.Error);
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
