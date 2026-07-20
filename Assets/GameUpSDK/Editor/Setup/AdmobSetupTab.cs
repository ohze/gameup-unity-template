using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GameUp.SDK;

namespace GameUp.SDK.Editor.Setup
{
    // ==========================================
    // ADMOB SETUP TAB -> Lưu vào AdmobAds.prefab
    // ==========================================
    public class AdmobSetupTab : SetupTabBase
    {
        public override string Title => "AdMob / AppOpen";

        private AdMobIdEditorPlatform _platform = AdMobIdEditorPlatform.Android;
        private string _appIdAndroid, _appIdIOS;
        
        private List<string> _testDevices = new List<string>();
        private AdUnitConfigData _interstitialConfig = new AdUnitConfigData();
        private AdUnitConfigData _rewardedConfig = new AdUnitConfigData();
        private AdUnitConfigData _appOpenConfig = new AdUnitConfigData();
        private AdUnitConfigData _bannerConfig = new AdUnitConfigData();
        private AdUnitConfigData _nativeAdConfig = new AdUnitConfigData();

        public override void Load()
        {
            _platform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? AdMobIdEditorPlatform.IOS : AdMobIdEditorPlatform.Android;
            
            // LOAD TỪ PREFAB RIÊNG (AdmobAds.prefab)
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(GameUpSetupPaths.PathAdMob);
            if (go != null)
            {
                var comp = go.GetComponentInChildren<AdmobNetwork>(true);
                if (comp != null)
                {
                    var so = new SerializedObject(comp);
                    AssignStringList(so.FindProperty("testDevices"), _testDevices);
                    _interstitialConfig.Load(so.FindProperty("interstitialConfig"));
                    _rewardedConfig.Load(so.FindProperty("rewardedConfig"));
                    _appOpenConfig.Load(so.FindProperty("appOpenConfig"));
                    _bannerConfig.Load(so.FindProperty("bannerConfig"));
                    _nativeAdConfig.Load(so.FindProperty("nativeAdConfig"));
                }
            }

            var googleAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(GameUpSetupPaths.PathGoogleMobileAdsSettings);
            if (googleAsset != null)
            {
                var so = new SerializedObject(googleAsset);
                Assign(so, "adMobAndroidAppId", ref _appIdAndroid);
                Assign(so, "adMobIOSAppId", ref _appIdIOS);
            }
        }

        public override void Draw()
        {
            EditorGUILayout.LabelField("AdMob Fallback & AppOpen", EditorStyles.boldLabel);
            _platform = (AdMobIdEditorPlatform)EditorGUILayout.EnumPopup("Editor Test Platform", _platform);
            EditorGUILayout.Space();

            DrawStringListUI("Test Devices", _testDevices);
            DrawConfigDataUI("Banner Configuration", _bannerConfig, _platform, AdUnitType.Banner);
            DrawConfigDataUI("Interstitial Configuration", _interstitialConfig, _platform, AdUnitType.Interstitial);
            DrawConfigDataUI("Rewarded Configuration", _rewardedConfig, _platform, AdUnitType.RewardedVideo);
            DrawConfigDataUI("App Open Configuration", _appOpenConfig, _platform, AdUnitType.AppOpen);
            DrawConfigDataUI("NativeAd Configuration", _nativeAdConfig, _platform, AdUnitType.NativeAd);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Google Mobile Ads App IDs", EditorStyles.boldLabel);
            _appIdAndroid = EditorGUILayout.TextField("Android App ID", _appIdAndroid);
            _appIdIOS = EditorGUILayout.TextField("iOS App ID", _appIdIOS);
        }

        private void ApplyToSerializedObject(SerializedObject so)
        {
            SetStringList(so.FindProperty("testDevices"), _testDevices);
            _interstitialConfig.Save(so.FindProperty("interstitialConfig"));
            _rewardedConfig.Save(so.FindProperty("rewardedConfig"));
            _appOpenConfig.Save(so.FindProperty("appOpenConfig"));
            _bannerConfig.Save(so.FindProperty("bannerConfig"));
            _nativeAdConfig.Save(so.FindProperty("nativeAdConfig"));
        }

        public override void Save()
        {
            // LƯU VÀO PREFAB RIÊNG (AdmobAds.prefab)
            ModifyPrefab(GameUpSetupPaths.PathAdMob, root => {
                var comp = root.GetComponentInChildren<AdmobNetwork>(true);
                if (comp != null)
                {
                    var so = new SerializedObject(comp);
                    ApplyToSerializedObject(so);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            });

            var googleAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(GameUpSetupPaths.PathGoogleMobileAdsSettings);
            if (googleAsset != null)
            {
                var so = new SerializedObject(googleAsset);
                Set(so, "adMobAndroidAppId", _appIdAndroid);
                Set(so, "adMobIOSAppId", _appIdIOS);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(googleAsset);
            }
        }
    }

    // ==========================================
    // MAX SETUP TAB -> Lưu vào MaxAds.prefab
    // ==========================================
    public class MaxSetupTab : SetupTabBase
    {
        public override string Title => "MAX Mediation";
#if MAXSDK_DEPENDENCIES_INSTALLED
        public override bool IsVisible => true;
#else
        public override bool IsVisible => false;
#endif

#if MAXSDK_DEPENDENCIES_INSTALLED
        private AdMobIdEditorPlatform _platform = AdMobIdEditorPlatform.Android;
        private string _sdkKey;
        private bool _showMediationDebugger;
        
        private AdUnitConfigData _interstitialConfig = new AdUnitConfigData();
        private AdUnitConfigData _rewardedConfig = new AdUnitConfigData();
        private AdUnitConfigData _appOpenConfig = new AdUnitConfigData();
        private AdUnitConfigData _bannerConfig = new AdUnitConfigData();

        public override void Load()
        {
            _platform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? AdMobIdEditorPlatform.IOS : AdMobIdEditorPlatform.Android;
            
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(GameUpSetupPaths.PathMax); // Thay bằng PathMax
            if (go != null)
            {
                var comp = go.GetComponentInChildren<MaxNetwork>(true);
                if (comp != null)
                {
                    var so = new SerializedObject(comp);
                    Assign(so, "sdkKey", ref _sdkKey);
                    var debuggerProp = so.FindProperty("showMediationDebugger");
                    if (debuggerProp != null) _showMediationDebugger = debuggerProp.boolValue;

                    _interstitialConfig.Load(so.FindProperty("interstitialConfig"));
                    _rewardedConfig.Load(so.FindProperty("rewardedConfig"));
                    _appOpenConfig.Load(so.FindProperty("appOpenConfig"));
                    _bannerConfig.Load(so.FindProperty("bannerConfig"));
                }
            }
        }

        public override void Draw()
        {
            EditorGUILayout.LabelField("AppLovin MAX Mediation", EditorStyles.boldLabel);
            _platform = (AdMobIdEditorPlatform)EditorGUILayout.EnumPopup("Editor Test Platform", _platform);
            EditorGUILayout.Space();

            _sdkKey = EditorGUILayout.TextField("SDK Key", _sdkKey);
            _showMediationDebugger = EditorGUILayout.Toggle("Show Mediation Debugger", _showMediationDebugger);
            EditorGUILayout.Space();

            DrawConfigDataUI("Banner Configuration", _bannerConfig, _platform, AdUnitType.Banner);
            DrawConfigDataUI("Interstitial Configuration", _interstitialConfig, _platform, AdUnitType.Interstitial);
            DrawConfigDataUI("Rewarded Configuration", _rewardedConfig, _platform, AdUnitType.RewardedVideo);
            DrawConfigDataUI("App Open Configuration", _appOpenConfig, _platform, AdUnitType.AppOpen);
        }

        private void ApplyToSerializedObject(SerializedObject so)
        {
            Set(so, "sdkKey", _sdkKey);
            var debuggerProp = so.FindProperty("showMediationDebugger");
            if (debuggerProp != null) debuggerProp.boolValue = _showMediationDebugger;

            _interstitialConfig.Save(so.FindProperty("interstitialConfig"));
            _rewardedConfig.Save(so.FindProperty("rewardedConfig"));
            _appOpenConfig.Save(so.FindProperty("appOpenConfig"));
            _bannerConfig.Save(so.FindProperty("bannerConfig"));
        }

        public override void Save()
        {
            ModifyPrefab(GameUpSetupPaths.PathMax, root => {
                var comp = root.GetComponentInChildren<MaxNetwork>(true);
                if (comp != null) { 
                    var so = new SerializedObject(comp); 
                    ApplyToSerializedObject(so); 
                    so.ApplyModifiedPropertiesWithoutUndo(); 
                }
            });
        }
#else
        public override void Load() { } public override void Draw() { } public override void Save() { }
#endif
    }

    // ==========================================
    // IRONSOURCE SETUP TAB -> Lưu vào IronSourceAds.prefab
    // ==========================================
    public class IronSourceSetupTab : SetupTabBase
    {
        public override string Title => "IronSource Mediation";
#if LEVELPLAY_DEPENDENCIES_INSTALLED
        public override bool IsVisible => true;
#else
        public override bool IsVisible => false;
#endif

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private AdMobIdEditorPlatform _platform = AdMobIdEditorPlatform.Android;
        private string _levelPlayAppKey;
        
        private AdUnitConfigData _interstitialConfig = new AdUnitConfigData();
        private AdUnitConfigData _rewardedConfig = new AdUnitConfigData();
        private AdUnitConfigData _bannerConfig = new AdUnitConfigData();

        public override void Load()
        {
            _platform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS ? AdMobIdEditorPlatform.IOS : AdMobIdEditorPlatform.Android;
            
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(GameUpSetupPaths.PathIronSource); // Thay bằng PathIronSource
            if (go != null)
            {
                var comp = go.GetComponentInChildren<IronSourceNetwork>(true);
                if (comp != null)
                {
                    var so = new SerializedObject(comp);
                    Assign(so, "levelPlayAppKey", ref _levelPlayAppKey);

                    _interstitialConfig.Load(so.FindProperty("interstitialConfig"));
                    _rewardedConfig.Load(so.FindProperty("rewardedConfig"));
                    _bannerConfig.Load(so.FindProperty("bannerConfig"));
                }
            }
        }

        public override void Draw()
        {
            EditorGUILayout.LabelField("IronSource (LevelPlay) Mediation", EditorStyles.boldLabel);
            _platform = (AdMobIdEditorPlatform)EditorGUILayout.EnumPopup("Editor Test Platform", _platform);
            EditorGUILayout.Space();

            _levelPlayAppKey = EditorGUILayout.TextField("LevelPlay App Key", _levelPlayAppKey);
            EditorGUILayout.Space();

            DrawConfigDataUI("Banner Configuration", _bannerConfig, _platform, AdUnitType.Banner);
            DrawConfigDataUI("Interstitial Configuration", _interstitialConfig, _platform, AdUnitType.Interstitial);
            DrawConfigDataUI("Rewarded Configuration", _rewardedConfig, _platform, AdUnitType.RewardedVideo);
        }

        private void ApplyToSerializedObject(SerializedObject so)
        {
            Set(so, "levelPlayAppKey", _levelPlayAppKey);
            _interstitialConfig.Save(so.FindProperty("interstitialConfig"));
            _rewardedConfig.Save(so.FindProperty("rewardedConfig"));
            _bannerConfig.Save(so.FindProperty("bannerConfig"));
        }

        public override void Save()
        {
            ModifyPrefab(GameUpSetupPaths.PathIronSource, root => {
                var comp = root.GetComponentInChildren<IronSourceNetwork>(true);
                if (comp != null) { 
                    var so = new SerializedObject(comp); 
                    ApplyToSerializedObject(so); 
                    so.ApplyModifiedPropertiesWithoutUndo(); 
                }
            });
        }
#else
        public override void Load() { } public override void Draw() { } public override void Save() { }
#endif
    }
}