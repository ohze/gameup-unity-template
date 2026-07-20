using UnityEditor;
using UnityEngine;
using GameUp.SDK;

namespace GameUp.SDK.Editor.Setup
{
    [CustomEditor(typeof(MaxNetwork))]
    public class MaxNetworkEditor : UnityEditor.Editor
    {
        private AdUnitConfigData _interstitialConfig = new AdUnitConfigData();
        private AdUnitConfigData _rewardedConfig = new AdUnitConfigData();
        private AdUnitConfigData _appOpenAdConfig = new AdUnitConfigData();
        private AdUnitConfigData _bannerConfig = new AdUnitConfigData();
        private AdUnitConfigData _nativeAdConfig = new AdUnitConfigData();

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
            _appOpenAdConfig.Load(serializedObject.FindProperty("appOpenAdConfig"));
            _bannerConfig.Load(serializedObject.FindProperty("bannerConfig"));
            _nativeAdConfig.Load(serializedObject.FindProperty("nativeAdConfig"));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AppLovin MAX Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Giao diện cấu hình ID đã được đồng bộ với cửa sổ SDK Setup. Bạn có thể sửa trực tiếp tại đây.", MessageType.Info);
            EditorGUILayout.Space();

            DrawPropertiesExcluding(serializedObject, "m_Script", "interstitialConfig", "rewardedConfig", "appOpenAdConfig", "bannerConfig", "nativeAdConfig");
            
            EditorGUILayout.Space();
            _platform = (AdMobIdEditorPlatform)EditorGUILayout.EnumPopup("Preview Platform UI", _platform);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            NetworkEditorUI.DrawConfigDataUI("Banner Configuration", _bannerConfig, _platform, AdUnitType.Banner);
            NetworkEditorUI.DrawConfigDataUI("Interstitial Configuration", _interstitialConfig, _platform, AdUnitType.Interstitial);
            NetworkEditorUI.DrawConfigDataUI("Rewarded Configuration", _rewardedConfig, _platform, AdUnitType.RewardedVideo);
            NetworkEditorUI.DrawConfigDataUI("App Open Configuration", _appOpenAdConfig, _platform, AdUnitType.AppOpen);
            NetworkEditorUI.DrawConfigDataUI("Native Ad Configuration", _nativeAdConfig, _platform, AdUnitType.NativeAd);

            if (EditorGUI.EndChangeCheck() || GUI.changed)
            {
                _interstitialConfig.Save(serializedObject.FindProperty("interstitialConfig"));
                _rewardedConfig.Save(serializedObject.FindProperty("rewardedConfig"));
                _appOpenAdConfig.Save(serializedObject.FindProperty("appOpenAdConfig"));
                _bannerConfig.Save(serializedObject.FindProperty("bannerConfig"));
                _nativeAdConfig.Save(serializedObject.FindProperty("nativeAdConfig"));
                
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}