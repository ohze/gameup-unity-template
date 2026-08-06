using System;
using System.IO;
using System.Reflection;
using GameUp.SDK;
using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    // ==========================================
    // APPSFLYER SETUP TAB -> GameUpSdkConfig.appsFlyer
    // ==========================================
    public class AppsFlyerSetupTab : SdkConfigTabBase
    {
        public override string Title => "AppsFlyer";

        protected override void DrawSection(SerializedObject so)
        {
            var appsFlyer = so.FindProperty("appsFlyer");

            EditorGUILayout.LabelField("AppsFlyer Configuration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(appsFlyer.FindPropertyRelative("devKey"), new GUIContent("Dev Key"));
            EditorGUILayout.PropertyField(appsFlyer.FindPropertyRelative("appIdIOS"), new GUIContent("App ID (iOS)"));
            EditorGUILayout.PropertyField(appsFlyer.FindPropertyRelative("isDebug"), new GUIContent("Debug Mode"));
            EditorGUILayout.PropertyField(appsFlyer.FindPropertyRelative("getConversionData"), new GUIContent("Get Conversion Data"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.HelpBox(
                "AppsFlyerUtils đẩy các giá trị này sang AppsFlyerObjectScript lúc Awake, trước khi SDK init — không cần sửa prefab.",
                MessageType.None);
        }
    }

    // ==========================================
    // APPMETRICA SETUP TAB -> GameUpSdkConfig.appMetrica
    // ==========================================
    public class AppmetricaSetupTab : SdkConfigTabBase
    {
        public override string Title => "AppMetrica";

#if APPMETRICA_DEPENDENCIES_INSTALLED
        public override bool IsVisible => true;
#else
        public override bool IsVisible => false;
#endif

        protected override void DrawSection(SerializedObject so)
        {
            var appMetrica = so.FindProperty("appMetrica");

            EditorGUILayout.LabelField("AppMetrica Configuration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(appMetrica.FindPropertyRelative("apiKey"), new GUIContent("API Key"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(appMetrica.FindPropertyRelative("enableEventLogging"), new GUIContent("Send Game Events"));
            EditorGUILayout.HelpBox("Send game events: gửi level/wave/IAP/ad revenue qua GameUpAnalytics → AppMetrica.",
                MessageType.None);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(appMetrica.FindPropertyRelative("enableLogs"), new GUIContent("SDK Debug Logs"));
            EditorGUILayout.EndVertical();
        }
    }

    // ==========================================
    // FIREBASE REMOTE CONFIG TAB -> GameUpSdkConfig.remoteConfig
    // ==========================================
    public class FirebaseRCSetupTab : SdkConfigTabBase
    {
        public override string Title => "Firebase RC";

        protected override void DrawSection(SerializedObject so)
        {
            var rc = so.FindProperty("remoteConfig");

            EditorGUILayout.LabelField("Firebase Remote Config Defaults", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Đây là giá trị mặc định (SetDefaults) khi chưa fetch được Remote Config. Tên field = key trên Firebase Console.",
                MessageType.None);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Ads", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(rc.FindPropertyRelative("inter_capping_time"),
                new GUIContent("inter_capping_time (s)", "Khoảng thời gian tối thiểu (giây) giữa 2 lần hiển thị Interstitial."));
            EditorGUILayout.PropertyField(rc.FindPropertyRelative("inter_start_level"),
                new GUIContent("inter_start_level", "Level bắt đầu hiện Interstitial (level tính từ 1)."));
            EditorGUILayout.PropertyField(rc.FindPropertyRelative("enable_banner"),
                new GUIContent("enable_banner", "Tắt/Bật hiển thị Banner trong Game. Ưu tiên cao hơn AdsManager.showBannerAfterInit: nếu false thì không show banner."));

            var ctaRate = rc.FindPropertyRelative("native_cta_click_rate");
            ctaRate.floatValue = EditorGUILayout.Slider(
                new GUIContent("native_cta_click_rate", "Tỉ lệ (0..1) vùng CTA nhận click của Native Ad, đẩy xuống native Android/iOS sau khi fetch."),
                ctaRate.floatValue, 0f, 1f);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Rate App", EditorStyles.miniBoldLabel);
            var enableRateApp = rc.FindPropertyRelative("enable_rate_app");
            EditorGUILayout.PropertyField(enableRateApp,
                new GUIContent("enable_rate_app", "Tắt/Bật hiển thị Rate App trong Game."));
            using (new EditorGUI.DisabledScope(!enableRateApp.boolValue))
            {
                EditorGUILayout.PropertyField(rc.FindPropertyRelative("level_start_show_rate_app"),
                    new GUIContent("level_start_show_rate_app", "Level hiện Rate App."));
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Khác", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(rc.FindPropertyRelative("no_internet_popup_enable"),
                new GUIContent("no_internet_popup_enable", "Tắt/Bật hiển thị Popup yêu cầu Internet."));
            EditorGUILayout.PropertyField(rc.FindPropertyRelative("extraData"),
                new GUIContent("Remote Config Extra Data", "ScriptableObject chứa thêm các key Remote Config riêng của dự án (tên field = key)."));
            EditorGUILayout.EndVertical();
        }
    }

    // ==========================================
    // FACEBOOK SETUP TAB -> FacebookSettings.asset
    // ==========================================
    public class FacebookSetupTab : SetupTabBase
    {
        public override string Title => "Facebook";
        public override bool RequiresWritablePrefab => false;

        private string _appLabel, _appId, _clientToken, _androidKeystorePath;
        private static Type _settingsType;

        public override void Load()
        {
            var type = GetFacebookSettingsType();
            if (type == null) return;
            var asset = AssetDatabase.LoadAssetAtPath(GetFacebookSettingsAssetPath(), type) as ScriptableObject;
            if (asset == null) return;

            var so = new SerializedObject(asset);
            var appLabels = so.FindProperty("appLabels");
            var appIds = so.FindProperty("appIds");
            var clientTokens = so.FindProperty("clientTokens");
            var keystore = so.FindProperty("androidKeystorePath");

            _appLabel = appLabels?.arraySize > 0 ? appLabels.GetArrayElementAtIndex(0).stringValue ?? "" : "";
            _appId = appIds?.arraySize > 0 ? appIds.GetArrayElementAtIndex(0).stringValue ?? "" : "";
            _clientToken = clientTokens?.arraySize > 0 ? clientTokens.GetArrayElementAtIndex(0).stringValue ?? "" : "";
            _androidKeystorePath = keystore != null ? keystore.stringValue ?? "" : "";
        }

        public override void Draw()
        {
            EditorGUILayout.LabelField("Facebook Settings", EditorStyles.boldLabel);
            var type = GetFacebookSettingsType();
            if (type == null)
            {
                EditorGUILayout.HelpBox("Chưa cài Facebook Unity SDK.", MessageType.Warning);
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath(GetFacebookSettingsAssetPath(), type) as ScriptableObject;
            if (asset == null)
            {
                if (GUILayout.Button("Tạo FacebookSettings.asset")) TryCreateAsset();
                return;
            }

            EditorGUILayout.BeginVertical("box");
            _appLabel = EditorGUILayout.TextField("App Name", _appLabel);
            _appId = EditorGUILayout.TextField("Facebook App Id", _appId);
            _clientToken = EditorGUILayout.TextField("Client Token", _clientToken);
            _androidKeystorePath = EditorGUILayout.TextField("Keystore Path", _androidKeystorePath);
            EditorGUILayout.EndVertical();
        }

        public override void Save()
        {
            var type = GetFacebookSettingsType();
            if (type == null) return;
            var asset = AssetDatabase.LoadAssetAtPath(GetFacebookSettingsAssetPath(), type) as ScriptableObject;
            if (asset == null) return;

            var so = new SerializedObject(asset);
            EnsureListSize(so, "appLabels", 1);
            EnsureListSize(so, "appIds", 1);
            EnsureListSize(so, "clientTokens", 1);
            so.Update();

            if (so.FindProperty("appLabels")?.arraySize > 0)
                so.FindProperty("appLabels").GetArrayElementAtIndex(0).stringValue = _appLabel ?? "";
            if (so.FindProperty("appIds")?.arraySize > 0)
                so.FindProperty("appIds").GetArrayElementAtIndex(0).stringValue = _appId ?? "";
            if (so.FindProperty("clientTokens")?.arraySize > 0)
                so.FindProperty("clientTokens").GetArrayElementAtIndex(0).stringValue = _clientToken ?? "";
            if (so.FindProperty("androidKeystorePath") != null)
                so.FindProperty("androidKeystorePath").stringValue = _androidKeystorePath ?? "";

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        // --- FACEBOOK UTILS ---
        private static Type GetFacebookSettingsType()
        {
            if (_settingsType != null) return _settingsType;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("Facebook.Unity.Settings.FacebookSettings", false);
                if (t != null && typeof(ScriptableObject).IsAssignableFrom(t)) return _settingsType = t;
            }

            return null;
        }

        private static string GetFacebookSettingsAssetPath()
        {
            var t = GetFacebookSettingsType();
            if (t == null) return GameUpSetupPaths.PathFacebookSettings;
            try
            {
                var path =
                    t.GetField("FacebookSettingsPath", BindingFlags.Public | BindingFlags.Static)
                        ?.GetValue(null) as string ?? "FacebookSDK/SDK/Resources";
                var name = t.GetField("FacebookSettingsAssetName", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null) as string ?? "FacebookSettings";
                var ext = t.GetField("FacebookSettingsAssetExtension", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null) as string ?? ".asset";
                return $"Assets/{path.Replace('\\', '/').Trim('/')}/{name}{ext}".Replace("//", "/");
            }
            catch
            {
                return GameUpSetupPaths.PathFacebookSettings;
            }
        }

        private void TryCreateAsset()
        {
            var t = GetFacebookSettingsType();
            if (t == null) return;
            string path = GetFacebookSettingsAssetPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance(t), path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Load();
        }

        private static void EnsureListSize(SerializedObject so, string listName, int minSize)
        {
            var p = so.FindProperty(listName);
            if (p != null && p.isArray && p.arraySize < minSize)
            {
                p.arraySize = minSize;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    // ==========================================
    // GAME ANALYTICS SETUP TAB -> Settings.asset
    // ==========================================
    public class GameAnalyticsSetupTab : SetupTabBase
    {
        public override string Title => "Game Analytics";
        public override bool RequiresWritablePrefab => false;

        private ScriptableObject _gaAsset;
        private int _addPlatformDropdownIndex;

        public override void Load()
        {
            var type = GetGameAnalyticsSettingsType();
            if (type != null)
                _gaAsset =
                    AssetDatabase.LoadAssetAtPath(GameUpSetupPaths.PathGameAnalyticsSettings, type) as ScriptableObject;
        }

        public override void Draw()
        {
            EditorGUILayout.LabelField("Game Analytics", EditorStyles.boldLabel);
            var type = GetGameAnalyticsSettingsType();
            if (type == null)
            {
                EditorGUILayout.HelpBox("Chưa cài GameAnalytics SDK.", MessageType.Error);
                return;
            }

            if (_gaAsset == null)
            {
                EditorGUILayout.HelpBox("Chưa có Settings.asset tại Resources/GameAnalytics.", MessageType.Warning);
                return;
            }

            var so = new SerializedObject(_gaAsset);
            so.Update();

            var usePlayerBuild = so.FindProperty("UsePlayerSettingsBuildNumber");
            if (usePlayerBuild != null)
                EditorGUILayout.PropertyField(usePlayerBuild, new GUIContent("Auto build từ Player Settings"));

            EditorGUILayout.Space(8);
            var available = type.GetMethod("GetAvailablePlatforms", BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(_gaAsset, null) as string[];
            if (available != null && available.Length > 0)
            {
                _addPlatformDropdownIndex = Mathf.Clamp(_addPlatformDropdownIndex, 0, available.Length - 1);
                _addPlatformDropdownIndex =
                    EditorGUILayout.Popup("Platform to add", _addPlatformDropdownIndex, available);
                if (GUILayout.Button("Add platform"))
                {
                    var parsed = ParsePlatform(available[_addPlatformDropdownIndex]);
                    if (parsed.HasValue)
                    {
                        type.GetMethod("AddPlatform", BindingFlags.Instance | BindingFlags.Public)
                            ?.Invoke(_gaAsset, new object[] { parsed.Value });
                    }
                }
            }

            var platforms = so.FindProperty("Platforms");
            var gameKeys = so.FindProperty("gameKey");
            var secretKeys = so.FindProperty("secretKey");

            if (platforms == null) return;
            int removeAt = -1;
            for (int i = 0; i < platforms.arraySize; i++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Platform",
                    ((RuntimePlatform)platforms.GetArrayElementAtIndex(i).intValue).ToString(), EditorStyles.boldLabel);
                if (i < gameKeys.arraySize)
                    EditorGUILayout.PropertyField(gameKeys.GetArrayElementAtIndex(i), new GUIContent("Game Key"));
                if (i < secretKeys.arraySize)
                    EditorGUILayout.PropertyField(secretKeys.GetArrayElementAtIndex(i), new GUIContent("Secret Key"));
                if (GUILayout.Button("Remove platform")) removeAt = i;
                EditorGUILayout.EndVertical();
            }

            if (removeAt >= 0)
                type.GetMethod("RemovePlatformAtIndex", BindingFlags.Instance | BindingFlags.Public)
                    ?.Invoke(_gaAsset, new object[] { removeAt });
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(_gaAsset);
        }

        public override void Save()
        {
            if (_gaAsset != null) EditorUtility.SetDirty(_gaAsset);
        }

        private static Type GetGameAnalyticsSettingsType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("GameAnalyticsSDK.Setup.Settings", false);
                if (t != null && typeof(ScriptableObject).IsAssignableFrom(t)) return t;
            }

            return null;
        }

        private static RuntimePlatform? ParsePlatform(string displayName)
        {
            if (displayName == "WSA") return RuntimePlatform.WSAPlayerARM;
            return Enum.TryParse(displayName, out RuntimePlatform p) ? p : (RuntimePlatform?)null;
        }
    }
}
