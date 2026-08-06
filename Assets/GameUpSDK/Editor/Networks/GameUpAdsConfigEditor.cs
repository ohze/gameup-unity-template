using GameUp.SDK;
using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    [CustomEditor(typeof(GameUpAdsConfig))]
    public class GameUpAdsConfigEditor : UnityEditor.Editor
    {
        private static readonly string[] Tabs = { "AdMob", "MAX", "IronSource" };

        private int _tabIndex;
        private AdMobIdEditorPlatform _platform;

        private void OnEnable()
        {
            _platform = NetworkEditorUI.DefaultPlatform;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Nguồn cấu hình ads duy nhất của project. Sửa tại đây hoặc trong cửa sổ GameUp/SDK/Setup đều như nhau.",
                MessageType.Info);

            var path = AssetDatabase.GetAssetPath(target).Replace('\\', '/');
            if (!path.Contains("/Resources/"))
            {
                EditorGUILayout.HelpBox(
                    "Asset phải nằm trong thư mục Resources thì bản build mới load được.\nGợi ý: " +
                    GameUpSetupPaths.AdsConfigFolder, MessageType.Error);
            }

            EditorGUILayout.Space();
            _tabIndex = GUILayout.Toolbar(_tabIndex, Tabs);
            _platform = NetworkEditorUI.DrawPlatformSelector(_platform);
            EditorGUILayout.Space(8);

            switch (_tabIndex)
            {
                case 0:
                    NetworkEditorUI.DrawAdmobSection(serializedObject.FindProperty("admob"), _platform);
                    break;
                case 1:
                    NetworkEditorUI.DrawMaxSection(serializedObject.FindProperty("max"), _platform);
                    break;
                default:
                    NetworkEditorUI.DrawIronSourceSection(serializedObject.FindProperty("ironSource"), _platform);
                    break;
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("Tạo Class AdPlacement (Constants)", GUILayout.Height(24)))
            {
                AdPlacementGenerator.Generate();
            }
        }
    }
}
