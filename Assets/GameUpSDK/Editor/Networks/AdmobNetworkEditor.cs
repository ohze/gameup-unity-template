using GameUp.SDK;
using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    /// <summary>
    /// Inspector chung cho 3 network: dữ liệu thật nằm trong GameUpAdsConfig,
    /// ở đây chỉ nhúng lại đúng section tương ứng để sửa nhanh ngay trên prefab.
    /// </summary>
    public abstract class AdNetworkEditorBase : UnityEditor.Editor
    {
        private AdMobIdEditorPlatform _platform;
        private SerializedObject _configSerialized;
        private bool _expanded = true;

        protected abstract string HeaderLabel { get; }
        protected abstract void DrawSection(SerializedObject config, AdMobIdEditorPlatform platform);

        protected virtual void OnEnable()
        {
            _platform = NetworkEditorUI.DefaultPlatform;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(HeaderLabel, EditorStyles.boldLabel);
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            var overrideProp = serializedObject.FindProperty("configOverride");
            var config = overrideProp?.objectReferenceValue as GameUpAdsConfig ?? GameUpAdsConfigAsset.Find();

            EditorGUILayout.Space();
            if (config == null)
            {
                EditorGUILayout.HelpBox("Chưa có asset GameUpAdsConfig trong project.", MessageType.Warning);
                if (GUILayout.Button("Tạo GameUpAdsConfig")) GameUpAdsConfigAsset.GetOrCreate();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Nguồn cấu hình", AssetDatabase.GetAssetPath(config));
            if (GUILayout.Button("Chọn asset", GUILayout.Width(90)))
            {
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }
            EditorGUILayout.EndHorizontal();

            _expanded = EditorGUILayout.Foldout(_expanded, "Cấu hình ID (sửa trực tiếp)", true);
            if (!_expanded) return;

            if (_configSerialized == null || _configSerialized.targetObject != config)
                _configSerialized = new SerializedObject(config);

            _configSerialized.Update();
            _platform = NetworkEditorUI.DrawPlatformSelector(_platform);
            EditorGUILayout.Space();
            DrawSection(_configSerialized, _platform);
            _configSerialized.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(AdmobNetwork))]
    public class AdmobNetworkEditor : AdNetworkEditorBase
    {
        protected override string HeaderLabel => "AdMob Network Configuration";

        protected override void DrawSection(SerializedObject config, AdMobIdEditorPlatform platform)
            => NetworkEditorUI.DrawAdmobSection(config.FindProperty("admob"), platform);
    }
}
