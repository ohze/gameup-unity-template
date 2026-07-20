using UnityEditor;
using UnityEngine;
using GameUp.SDK;

namespace GameUp.SDK.Editor.Setup
{
    [CustomEditor(typeof(IronSourceNetwork))]
    public class IronSourceNetworkEditor : UnityEditor.Editor
    {
        private AdUnitConfigData _interstitialConfig = new AdUnitConfigData();
        private AdUnitConfigData _rewardedConfig = new AdUnitConfigData();
        private AdUnitConfigData _bannerConfig = new AdUnitConfigData();

        private AdMobIdEditorPlatform _platform;

        private void OnEnable()
        {
            _platform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? AdMobIdEditorPlatform.IOS : AdMobIdEditorPlatform.Android;
            LoadData();
        }

        private void LoadData()
        {
            _interstitialConfig.Load(serializedObject.FindProperty("interstitialConfig"));
            _rewardedConfig.Load(serializedObject.FindProperty("rewardedConfig"));
            _bannerConfig.Load(serializedObject.FindProperty("bannerConfig"));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("IronSource (LevelPlay) Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Giao diện cấu hình ID đã được đồng bộ với cửa sổ SDK Setup. Bạn có thể sửa trực tiếp tại đây.", MessageType.Info);
            EditorGUILayout.Space();

            DrawPropertiesExcluding(serializedObject, "m_Script", "interstitialConfig", "rewardedConfig", "bannerConfig");
            
            EditorGUILayout.Space();
            _platform = (AdMobIdEditorPlatform)EditorGUILayout.EnumPopup("Preview Platform UI", _platform);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            NetworkEditorUI.DrawConfigDataUI("Banner Configuration", _bannerConfig, _platform, AdUnitType.Banner);
            NetworkEditorUI.DrawConfigDataUI("Interstitial Configuration", _interstitialConfig, _platform, AdUnitType.Interstitial);
            NetworkEditorUI.DrawConfigDataUI("Rewarded Configuration", _rewardedConfig, _platform, AdUnitType.RewardedVideo);

            if (EditorGUI.EndChangeCheck() || GUI.changed)
            {
                _interstitialConfig.Save(serializedObject.FindProperty("interstitialConfig"));
                _rewardedConfig.Save(serializedObject.FindProperty("rewardedConfig"));
                _bannerConfig.Save(serializedObject.FindProperty("bannerConfig"));
                
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}