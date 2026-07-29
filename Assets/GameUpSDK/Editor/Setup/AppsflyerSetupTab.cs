using System;
using System.IO;
using System.Reflection;
using GameUp.SDK;
using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    // ==========================================
    // APPSFLYER SETUP TAB -> AppsFlyerObject.prefab
    // ==========================================
    public class AppsFlyerSetupTab : SetupTabBase
    {
        public override string Title => "AppsFlyer";
        private string _devKey, _appId;
        private bool _isDebug;

        public override void Load()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(GameUpSetupPaths.PathAppsFlyer);
            if (go == null) return;
            var type = Type.GetType("AppsFlyerObjectScript, AppsFlyer");
            if (type == null) return;
            var comp = go.GetComponent(type);
            if (comp != null)
            {
                var so = new SerializedObject(comp);
                Assign(so, "devKey", ref _devKey);
                Assign(so, "appID", ref _appId);
                AssignBool(so, "isDebug", ref _isDebug);
            }
        }

        public override void Draw()
        {
            EditorGUILayout.LabelField("AppsFlyer Configuration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            _devKey = EditorGUILayout.TextField("Dev Key", _devKey);
            _appId = EditorGUILayout.TextField("App ID (iOS)", _appId);
            _isDebug = EditorGUILayout.Toggle("Debug Mode", _isDebug);
            EditorGUILayout.EndVertical();
        }

        public override void Save()
        {
            ModifyPrefab(GameUpSetupPaths.PathAppsFlyer, root =>
            {
                var type = Type.GetType("AppsFlyerObjectScript, AppsFlyer");
                if (type == null) return;
                var comp = root.GetComponent(type);
                if (comp != null)
                {
                    var so = new SerializedObject(comp);
                    Set(so, "devKey", _devKey);
                    Set(so, "appID", _appId);
                    SetBool(so, "isDebug", _isDebug);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"AppsFlyer saved: {GameUpSetupPaths.PathAppsFlyer} : {_devKey} : {_appId}");
                }
            });
        }
    }

    // ==========================================
    // APPMETRICA SETUP TAB -> AppmetricaObject.prefab
    // ==========================================
    public class AppmetricaSetupTab : SetupTabBase
    {
        public override string Title => "AppMetrica";

#if APPMETRICA_DEPENDENCIES_INSTALLED
        public override bool IsVisible => true;

        private string _apiKey;
        private bool _enableLogs;
        private bool _enableEventLogging = true;

        public override void Load()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(GameUpSetupPaths.PathAppmetrica);
            if (go == null) return;
            var comp = go.GetComponent<AppMetricaActivator>();
            if (comp != null)
            {
                var so = new SerializedObject(comp);
                Assign(so, "apiKey", ref _apiKey);
                AssignBool(so, "enableLogs", ref _enableLogs);
                AssignBool(so, "enableEventLogging", ref _enableEventLogging);
            }
        }

        public override void Draw()
        {
            EditorGUILayout.LabelField("AppMetrica Configuration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            _apiKey = EditorGUILayout.TextField("API Key", _apiKey);
            EditorGUILayout.Space();
            _enableEventLogging = EditorGUILayout.Toggle("Send Game Events", _enableEventLogging);
            EditorGUILayout.HelpBox("Send game events: gửi level/wave/IAP/ad revenue qua GameUpAnalytics → AppMetrica.",
                MessageType.None);
            EditorGUILayout.Space();
            _enableLogs = EditorGUILayout.Toggle("SDK Debug Logs", _enableLogs);
            EditorGUILayout.EndVertical();
        }

        public override void Save()
        {
            ModifyPrefab(GameUpSetupPaths.PathAppmetrica, root =>
            {
                var comp = root.GetComponent<AppMetricaActivator>();
                if (comp != null)
                {
                    var so = new SerializedObject(comp);
                    Set(so, "apiKey", _apiKey);
                    SetBool(so, "enableLogs", _enableLogs);
                    SetBool(so, "enableEventLogging", _enableEventLogging);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            });
        }
#else
        public override bool IsVisible => false;
        public override void Load() { } public override void Draw() { } public override void Save() { }
#endif
    }

    // ==========================================
    // FIREBASE REMOTE CONFIG TAB -> SDK.prefab
    // ==========================================
    public class FirebaseRCSetupTab : SetupTabBase
    {
        public override string Title => "Firebase RC";

        private int _rcInterCappingTime = 120;
        private int _rcInterStartLevel = 3;
        private bool _rcEnableBanner = true;

        public override void Load()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(GameUpSetupPaths.PathSDK);
            if (go == null) return;

            var rcComp = go.GetComponent<FirebaseRemoteConfigUtils>();
            if (rcComp != null)
            {
                var so = new SerializedObject(rcComp);
                AssignInt(so, "inter_capping_time", ref _rcInterCappingTime);
                AssignInt(so, "inter_start_level", ref _rcInterStartLevel);
                AssignBool(so, "enable_banner", ref _rcEnableBanner);
            }
        }

        public override void Draw()
        {
            EditorGUILayout.LabelField("Firebase Remote Config Defaults", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            _rcInterCappingTime = EditorGUILayout.IntField("inter_capping_time (s)", _rcInterCappingTime);
            _rcInterStartLevel = EditorGUILayout.IntField("inter_start_level", _rcInterStartLevel);
            _rcEnableBanner = EditorGUILayout.Toggle("enable_banner", _rcEnableBanner);
            EditorGUILayout.EndVertical();
        }

        public override void Save()
        {
            ModifyPrefab(GameUpSetupPaths.PathSDK, root =>
            {
                var rcComp = root.GetComponent<FirebaseRemoteConfigUtils>();
                if (rcComp != null)
                {
                    var so = new SerializedObject(rcComp);
                    SetInt(so, "inter_capping_time", _rcInterCappingTime);
                    SetInt(so, "inter_start_level", _rcInterStartLevel);
                    SetBool(so, "enable_banner", _rcEnableBanner);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            });
        }
    }

    // ==========================================
    // FACEBOOK SETUP TAB -> FacebookSettings.asset
    // ==========================================
    public class FacebookSetupTab : SetupTabBase
    {
        public override string Title => "Facebook";
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