using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
using GameAnalyticsSDK;
#endif

namespace GameUp.SDK.Editor
{
    public class GameUpSetupWindow : EditorWindow
    {
        // Đường dẫn được resolve động: hỗ trợ cả Assets/ (dev project) và Packages/ (UPM Git install)
        private static string _packageRoot;

        private static string PackageRoot
        {
            get
            {
                if (_packageRoot != null) return _packageRoot;

                // Thử dùng PackageInfo để tìm đường dẫn chính xác khi cài qua UPM
                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var pkgInfoType = Type.GetType("UnityEditor.PackageManager.PackageInfo, UnityEditor");
                    if (pkgInfoType != null)
                    {
                        var method = pkgInfoType.GetMethod("FindForAssembly", BindingFlags.Static | BindingFlags.Public,
                            null, new[] { typeof(Assembly) }, null);
                        if (method != null)
                        {
                            var info = method.Invoke(null, new object[] { assembly });
                            if (info != null)
                            {
                                var assetPathProp = pkgInfoType.GetProperty("assetPath");
                                var path = assetPathProp?.GetValue(info) as string;
                                if (!string.IsNullOrEmpty(path))
                                {
                                    _packageRoot = path;
                                    return _packageRoot;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    /* fallback below */
                }

                // Fallback: project gốc
                _packageRoot = "Assets/GameUpSDK";
                return _packageRoot;
            }
        }

        /// <summary>Bản prefab có thể chỉnh sửa khi SDK cài qua UPM (Packages read-only).</summary>
        private const string WritablePrefabsRoot = "Assets/SDK/Prefabs";

        private static string GetPackagePrefabDirectory()
        {
            return (PackageRoot.Replace('\\', '/') + "/Prefab").Replace("//", "/");
        }

        /// <summary>Ưu tiên bản clone tại Assets/SDK/Prefabs nếu đã có; ngược lại dùng Prefab trong package / Assets.</summary>
        private static string GetPrefabDirectory()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WritablePrefabsRoot + "/SDK.prefab") != null)
                return WritablePrefabsRoot;
            return GetPackagePrefabDirectory();
        }

        private static string PathSDK => GetPrefabDirectory() + "/SDK.prefab";
        private static string PathAppsFlyer => GetPrefabDirectory() + "/AppsFlyerObject.prefab";
        private static string PathAppmetrica => GetPrefabDirectory() + "/AppmetricaObject.prefab";
#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private static string PathIronSource => GetPrefabDirectory() + "/IronSourceAds.prefab";
#endif
#if MAXSDK_DEPENDENCIES_INSTALLED
        private static string PathMax => GetPrefabDirectory() + "/MaxAds.prefab";
#endif
        private static string PathAdMob => GetPrefabDirectory() + "/AdmobAds.prefab";

        private const string PathGoogleMobileAdsSettings =
            "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";

        private const string PathLevelPlayMediationSettings =
            "Assets/LevelPlay/Resources/LevelPlayMediationSettings.asset";

        private const string PathGameAnalyticsSettings = "Assets/Resources/GameAnalytics/Settings.asset";

        /// <summary>Mặc định GameAnalytics plugin; <see cref="GameAnalytics.WhereIs"/> dùng khi đường dẫn lệch.</summary>
        private const string PathGameAnalyticsPrefabDefault =
            "Assets/GameAnalytics/Plugins/Prefabs/GameAnalytics.prefab";

        /// <summary>Định dạng đồng bộ với Facebook.Unity.Settings.FacebookSettings (SDK 18.x).</summary>
        private const string PathFacebookSettings = "Assets/FacebookSDK/SDK/Resources/FacebookSettings.asset";

        private int _activeTab;
        private string[] _tabs;
        private Dictionary<int, Action> _tabDrawers;
        private GameUp.SDK.AdsManager.PrimaryMediation _lastPrimaryMediation;

        private enum SetupTab
        {
            Facebook,
            AppsFlyer,
            GameAnalytics,
#if APPMETRICA_DEPENDENCIES_INSTALLED
            AppMetrica,
#endif
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            IronSourceMediation,
#endif
#if MAXSDK_DEPENDENCIES_INSTALLED
            MaxMediation,
#endif
            AdMobAppOpen,
            FirebaseRemoteConfig,
        }

        // FacebookSettings.asset
        private string _facebookAppLabel = "";
        private string _facebookAppId = "";
        private string _facebookClientToken = "";
        private string _facebookAndroidKeystorePath = "";

        // AppsFlyer
        private string _appsFlyerDevKey = "";
        private string _appsFlyerAppId = "";
        private bool _appsFlyerIsDebug = false;
        
        // Appmetrica
        private string _appmetricaApikey = "";
        private bool _appmetricaEnbaleLogs = false;

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        // IronSource
        private string _ironSourceAppKey = "";
        private AdMobIdEditorPlatform _ironSourceEditorPlatform = AdMobIdEditorPlatform.Android;
        private string _ironSourceBannerIdAndroid = "";
        private string _ironSourceInterstitialIdAndroid = "";
        private string _ironSourceRewardedIdAndroid = "";
        private string _ironSourceBannerIdIOS = "";
        private string _ironSourceInterstitialIdIOS = "";
        private string _ironSourceRewardedIdIOS = "";
        private bool _ironSourceUseMultiAdUnitIds;
        private List<GameUp.SDK.AdUnitIdEntry> _ironSourceAdUnitIdsAndroid = new List<GameUp.SDK.AdUnitIdEntry>();
        private List<GameUp.SDK.AdUnitIdEntry> _ironSourceAdUnitIdsIOS = new List<GameUp.SDK.AdUnitIdEntry>();

        // LevelPlay Mediation Settings
        private string _levelPlayAndroidAppKey = "";
        private string _levelPlayIOSAppKey = "";
#endif

#if MAXSDK_DEPENDENCIES_INSTALLED
        // AppLovin MAX
        private string _maxSdkKey = "";
        private AdMobIdEditorPlatform _maxEditorPlatform = AdMobIdEditorPlatform.Android;
        private string _maxBannerIdAndroid = "";
        private string _maxInterstitialIdAndroid = "";
        private string _maxRewardedIdAndroid = "";
        private string _maxAppOpenIdAndroid = "";
        private string _maxBannerIdIOS = "";
        private string _maxInterstitialIdIOS = "";
        private string _maxRewardedIdIOS = "";
        private string _maxAppOpenIdIOS = "";
        private bool _maxUseMultiAdUnitIds;
        private List<GameUp.SDK.AdUnitIdEntry> _maxAdUnitIdsAndroid = new List<GameUp.SDK.AdUnitIdEntry>();
        private List<GameUp.SDK.AdUnitIdEntry> _maxAdUnitIdsIOS = new List<GameUp.SDK.AdUnitIdEntry>();
#endif

        private enum AdMobIdEditorPlatform
        {
            Android,
            IOS
        }

        // AdMob
        private AdMobIdEditorPlatform _admobEditorPlatform = AdMobIdEditorPlatform.Android;
        private string _admobBannerIdAndroid = "";
        private string _admobInterstitialIdAndroid = "";
        private string _admobRewardedIdAndroid = "";
        private string _admobAppOpenIdAndroid = "";
        private string _admobBannerIdIOS = "";
        private string _admobInterstitialIdIOS = "";
        private string _admobRewardedIdIOS = "";
        private string _admobAppOpenIdIOS = "";
        private bool _admobUseMultiAdUnitIds;
        private List<GameUp.SDK.AdUnitIdEntry> _admobAdUnitIdsAndroid = new List<GameUp.SDK.AdUnitIdEntry>();
        private List<GameUp.SDK.AdUnitIdEntry> _admobAdUnitIdsIOS = new List<GameUp.SDK.AdUnitIdEntry>();

        // Google Mobile Ads App IDs
        private string _googleMobileAdsAndroidAppId = "";
        private string _googleMobileAdsIOSAppId = "";

        // FirebaseRemoteConfigUtils
        private int _rcInterCappingTime = 120;
        private int _rcInterStartLevel = 3;
        private bool _rcEnableBanner = true;
        private bool _adsShowBannerAfterInit = true;
        private string _adsShowBannerPlacementAfterInit = "main";
        private float _adsShowBannerDelaySeconds = 2f;

        /// <summary>Dropdown "Platform to add" trong tab Game Analytics.</summary>
        private int _gaAddPlatformDropdownIndex;

        private Vector2 _scrollPosition;
        private string _loadErrors;
        private string _saveErrors;

        [MenuItem("GameUp/SDK/Setup")]
        public static void ShowWindow()
        {
            if (!GameUp.SDK.Installer.GameUpDependenciesWindow.AreAllRequiredPackagesInstalled())
            {
                GameUp.SDK.Installer.GameUpDependenciesWindow.ShowWindow();
                return;
            }

            var window = GetWindow<GameUpSetupWindow>("GameUp SDK Setup");
            window.minSize = new Vector2(400, 480);
        }

        private void OnEnable()
        {
            _admobEditorPlatform = GetDefaultAdmobEditorPlatform();
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            _ironSourceEditorPlatform = GetDefaultAdmobEditorPlatform();
#endif
#if MAXSDK_DEPENDENCIES_INSTALLED
            _maxEditorPlatform = GetDefaultAdmobEditorPlatform();
#endif
            LoadFromSceneOrPrefabs();

            _lastPrimaryMediation = GetPrimaryMediationFromDefines();
            BuildTabsForPrimaryMediation(_lastPrimaryMediation, keepActiveTab: false);
        }

        private static bool RequiresPrefabCloneBeforeSetup()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(WritablePrefabsRoot + "/SDK.prefab") == null;
        }

        private static bool CanSetupFromWritablePrefabs(out string reason)
        {
            var writableSdkPath = (WritablePrefabsRoot + "/SDK.prefab").Replace('\\', '/');
            if (AssetDatabase.LoadAssetAtPath<GameObject>(writableSdkPath) == null)
            {
                reason = "Chưa có prefab clone tại " + writableSdkPath +
                         ".\nHãy clone prefab sang Assets trước để cấu hình.";
                return false;
            }

            reason = null;
            return true;
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (!string.IsNullOrEmpty(_loadErrors))
            {
                EditorGUILayout.HelpBox(_loadErrors, MessageType.Warning);
                EditorGUILayout.Space(4);
            }

            if (!string.IsNullOrEmpty(_saveErrors))
            {
                EditorGUILayout.HelpBox(_saveErrors, MessageType.Error);
                _saveErrors = null;
                EditorGUILayout.Space(4);
            }

            if (RequiresPrefabCloneBeforeSetup())
            {
                EditorGUILayout.HelpBox("Bạn cần clone prefab từ Package sang Assets để kích hoạt chỉnh sửa.",
                    MessageType.Warning);
                EditorGUILayout.Space(8);
                if (GUILayout.Button("Clone Prefab từ Package → Assets/SDK/Prefabs", GUILayout.Height(30)))
                {
                    if (TryClonePackagePrefabsToWritable(out var cloneErr))
                    {
                        LoadFromSceneOrPrefabs();
                        Debug.Log("[GameUpSDK] Đã clone prefab thành công.");
                    }
                    else if (!string.IsNullOrEmpty(cloneErr))
                        _saveErrors = cloneErr;
                }

                EditorGUILayout.Space(6);
            }

            var pm = GetPrimaryMediationFromDefines();
            if (pm != _lastPrimaryMediation)
            {
                _lastPrimaryMediation = pm;
                BuildTabsForPrimaryMediation(pm, keepActiveTab: true);
            }

            if (_tabs == null || _tabs.Length == 0 || _tabDrawers == null || _tabDrawers.Count == 0)
            {
                BuildTabsForPrimaryMediation(pm, keepActiveTab: false);
            }

            _activeTab = GUILayout.Toolbar(_activeTab, _tabs);
            EditorGUILayout.Space(8);

            if (_activeTab < 0) _activeTab = 0;
            if (_activeTab >= _tabs.Length) _activeTab = _tabs.Length - 1;
            bool canSetup = CanSetupFromWritablePrefabs(out var lockReason);
            EditorGUI.BeginDisabledGroup(!canSetup);
            if (_tabDrawers.TryGetValue(_activeTab, out var draw))
                draw?.Invoke();
            EditorGUI.EndDisabledGroup();

            if (!canSetup && !string.IsNullOrEmpty(lockReason))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(lockReason, MessageType.Warning);
            }

            EditorGUILayout.Space(16);
            EditorGUI.BeginDisabledGroup(!canSetup);
            if (GUILayout.Button("Save Configuration", GUILayout.Height(32)))
            {
                SaveConfiguration();
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Tạo SDK trong Scene hiện tại", GUILayout.Height(28)))
            {
                CreateSDKInCurrentScene();
            }

            EditorGUILayout.EndScrollView();
        }

        private static GameUp.SDK.AdsManager.PrimaryMediation GetPrimaryMediationFromDefines()
        {
            try
            {
                string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
                if (!string.IsNullOrEmpty(symbols))
                {
                    if (symbols.Contains("GAMEUP_PRIMARY_MEDIATION_MAX"))
                        return GameUp.SDK.AdsManager.PrimaryMediation.Max;
                    if (symbols.Contains(GameUp.SDK.GUDefinetion.PrimaryMediationAdMob))
                        return GameUp.SDK.AdsManager.PrimaryMediation.AdMob;
                }
            }
            catch
            {
                /* ignore */
            }

            return GameUp.SDK.AdsManager.PrimaryMediation.LevelPlay;
        }

        private void BuildTabsForPrimaryMediation(GameUp.SDK.AdsManager.PrimaryMediation pm, bool keepActiveTab)
        {
            string previousTabName = (_tabs != null && _activeTab >= 0 && _activeTab < _tabs.Length)
                ? _tabs[_activeTab]
                : null;

            var tabs = new List<SetupTab> { SetupTab.Facebook, SetupTab.AppsFlyer, SetupTab.GameAnalytics };

            if (pm == GameUp.SDK.AdsManager.PrimaryMediation.LevelPlay)
            {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
                tabs.Add(SetupTab.IronSourceMediation);
#endif
                tabs.Add(SetupTab.AdMobAppOpen);
            }
            else if (pm == GameUp.SDK.AdsManager.PrimaryMediation.Max)
            {
#if MAXSDK_DEPENDENCIES_INSTALLED
                tabs.Add(SetupTab.MaxMediation);
#endif
                tabs.Add(SetupTab.AdMobAppOpen);
            }
            else
            {
                tabs.Add(SetupTab.AdMobAppOpen);
            }

            tabs.Add(SetupTab.FirebaseRemoteConfig);
#if APPMETRICA_DEPENDENCIES_INSTALLED
            tabs.Add(SetupTab.AppMetrica);
#endif

            _tabs = tabs.ConvertAll(GetTabLabel).ToArray();
            _tabDrawers = new Dictionary<int, Action>(_tabs.Length);
            for (int i = 0; i < tabs.Count; i++)
            {
                var t = tabs[i];
                _tabDrawers[i] = () =>
                {
                    switch (t)
                    {
                        case SetupTab.Facebook: DrawFacebookSection(); break;
                        case SetupTab.AppsFlyer: DrawAppsFlyerSection(); break;
#if APPMETRICA_DEPENDENCIES_INSTALLED
                        case SetupTab.AppMetrica: DrawAppmetricaSection(); break;
#endif
                        case SetupTab.GameAnalytics: DrawGameAnalyticsSection(); break;
#if LEVELPLAY_DEPENDENCIES_INSTALLED
                        case SetupTab.IronSourceMediation: DrawIronSourceSection(); break;
#endif
#if MAXSDK_DEPENDENCIES_INSTALLED
                        case SetupTab.MaxMediation: DrawMaxSection(); break;
#endif
                        case SetupTab.AdMobAppOpen: DrawAdMobSection(); break;
                        case SetupTab.FirebaseRemoteConfig: DrawFirebaseRemoteConfigSection(); break;
                    }
                };
            }

            if (keepActiveTab && !string.IsNullOrEmpty(previousTabName))
            {
                int idx = Array.IndexOf(_tabs, previousTabName);
                _activeTab = idx >= 0 ? idx : 3;
            }
            else _activeTab = 3;
        }

        private static string GetTabLabel(SetupTab tab)
        {
            return tab switch
            {
                SetupTab.Facebook => "Facebook",
                SetupTab.AppsFlyer => "AppsFlyer",
                SetupTab.GameAnalytics => "Game Analytics",
#if APPMETRICA_DEPENDENCIES_INSTALLED
                SetupTab.AppMetrica => "Appmetrica",
#endif
#if LEVELPLAY_DEPENDENCIES_INSTALLED
                SetupTab.IronSourceMediation => "IronSource Mediation",
#endif
#if MAXSDK_DEPENDENCIES_INSTALLED
                SetupTab.MaxMediation => "MAX Mediation",
#endif
                SetupTab.AdMobAppOpen => "AdMob (App Open)",
                SetupTab.FirebaseRemoteConfig => "Firebase RC",
                _ => tab.ToString()
            };
        }

        private void CreateSDKInCurrentScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PathSDK);
            if (prefab == null)
            {
                _saveErrors = "Không tìm thấy prefab SDK tại: " + PathSDK;
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance != null)
            {
                EnsureGameAnalyticsChildUnderSdkObject(instance);
                Selection.activeGameObject = instance;
                EditorGUIUtility.PingObject(instance);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("[GameUpSDK] Đã thêm SDK vào scene.");
            }
        }

        // ---- FACEBOOK ----
        private static Type _facebookSettingsType;

        private static Type GetFacebookSettingsType()
        {
            if (_facebookSettingsType != null) return _facebookSettingsType;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType("Facebook.Unity.Settings.FacebookSettings", false);
                    if (t != null && typeof(ScriptableObject).IsAssignableFrom(t))
                    {
                        _facebookSettingsType = t;
                        break;
                    }
                }
                catch
                {
                }
            }

            return _facebookSettingsType;
        }

        private static string GetFacebookSettingsAssetPath()
        {
            var t = GetFacebookSettingsType();
            if (t == null) return PathFacebookSettings;
            try
            {
                var pathField = t.GetField("FacebookSettingsPath", BindingFlags.Public | BindingFlags.Static);
                var nameField = t.GetField("FacebookSettingsAssetName", BindingFlags.Public | BindingFlags.Static);
                var extField = t.GetField("FacebookSettingsAssetExtension", BindingFlags.Public | BindingFlags.Static);
                string rel = (pathField?.GetValue(null) as string ?? "FacebookSDK/SDK/Resources").Replace('\\', '/')
                    .Trim('/');
                string name = nameField?.GetValue(null) as string ?? "FacebookSettings";
                string ext = extField?.GetValue(null) as string ?? ".asset";
                return $"Assets/{rel}/{name}{ext}".Replace("//", "/");
            }
            catch
            {
                return PathFacebookSettings;
            }
        }

        private static void TryFacebookManifestRegenerate()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = asm.GetType("Facebook.Unity.Editor.ManifestMod", false);
                    if (t == null) continue;
                    var m = t.GetMethod("GenerateManifest", BindingFlags.Static | BindingFlags.Public);
                    m?.Invoke(null, null);
                    return;
                }
            }
            catch
            {
            }
        }

        private void LoadFacebookSettings()
        {
            var settingsType = GetFacebookSettingsType();
            if (settingsType == null) return;
            string path = GetFacebookSettingsAssetPath();
            var asset = AssetDatabase.LoadAssetAtPath(path, settingsType) as ScriptableObject;
            if (asset == null)
            {
                _facebookAppLabel = _facebookAppId = _facebookClientToken = _facebookAndroidKeystorePath = "";
                return;
            }

            var so = new SerializedObject(asset);
            var appLabels = so.FindProperty("appLabels");
            var appIds = so.FindProperty("appIds");
            var clientTokens = so.FindProperty("clientTokens");
            var keystore = so.FindProperty("androidKeystorePath");

            _facebookAppLabel = appLabels != null && appLabels.arraySize > 0
                ? appLabels.GetArrayElementAtIndex(0).stringValue ?? ""
                : "";
            _facebookAppId = appIds != null && appIds.arraySize > 0
                ? appIds.GetArrayElementAtIndex(0).stringValue ?? ""
                : "";
            _facebookClientToken = clientTokens != null && clientTokens.arraySize > 0
                ? clientTokens.GetArrayElementAtIndex(0).stringValue ?? ""
                : "";
            _facebookAndroidKeystorePath = keystore != null ? keystore.stringValue ?? "" : "";
        }

        private bool SaveFacebookSettingsAsset()
        {
            var settingsType = GetFacebookSettingsType();
            if (settingsType == null) return true;
            string path = GetFacebookSettingsAssetPath();
            var asset = AssetDatabase.LoadAssetAtPath(path, settingsType) as ScriptableObject;
            if (asset == null) return true;

            var so = new SerializedObject(asset);
            EnsureFacebookListSize(so, "appLabels", 1);
            EnsureFacebookListSize(so, "appIds", 1);
            EnsureFacebookListSize(so, "clientTokens", 1);
            so.Update();

            if (so.FindProperty("appLabels")?.arraySize > 0)
                so.FindProperty("appLabels").GetArrayElementAtIndex(0).stringValue = _facebookAppLabel ?? "";
            if (so.FindProperty("appIds")?.arraySize > 0)
                so.FindProperty("appIds").GetArrayElementAtIndex(0).stringValue = _facebookAppId ?? "";
            if (so.FindProperty("clientTokens")?.arraySize > 0)
                so.FindProperty("clientTokens").GetArrayElementAtIndex(0).stringValue = _facebookClientToken ?? "";
            if (so.FindProperty("androidKeystorePath") != null)
                so.FindProperty("androidKeystorePath").stringValue = _facebookAndroidKeystorePath ?? "";

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            TryFacebookManifestRegenerate();
            return true;
        }

        private static void EnsureFacebookListSize(SerializedObject so, string listName, int minSize)
        {
            var p = so.FindProperty(listName);
            if (p == null || p.isArray == false || p.arraySize >= minSize) return;
            p.arraySize = minSize;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private void DrawFacebookSection()
        {
            EditorGUILayout.LabelField("Facebook Settings", EditorStyles.boldLabel);
            var settingsType = GetFacebookSettingsType();
            if (settingsType == null)
            {
                EditorGUILayout.HelpBox("Chưa cài Facebook Unity SDK.", MessageType.Warning);
                return;
            }

            string path = GetFacebookSettingsAssetPath();
            var asset = AssetDatabase.LoadAssetAtPath(path, settingsType) as ScriptableObject;
            if (asset == null)
            {
                if (GUILayout.Button("Tạo FacebookSettings.asset")) TryCreateFacebookSettingsAsset();
                return;
            }

            _facebookAppLabel = EditorGUILayout.TextField("App Name", _facebookAppLabel);
            _facebookAppId = EditorGUILayout.TextField("Facebook App Id", _facebookAppId);
            _facebookClientToken = EditorGUILayout.TextField("Client Token", _facebookClientToken);
            _facebookAndroidKeystorePath = EditorGUILayout.TextField("Keystore Path", _facebookAndroidKeystorePath);
        }

        private bool TryCreateFacebookSettingsAsset()
        {
            var settingsType = GetFacebookSettingsType();
            if (settingsType == null) return false;
            string path = GetFacebookSettingsAssetPath();
            if (AssetDatabase.LoadAssetAtPath(path, settingsType) != null) return true;
            string diskPath = Path.Combine(Application.dataPath,
                "FacebookSDK/SDK/Resources".Replace('\\', Path.DirectorySeparatorChar));
            if (!Directory.Exists(diskPath)) Directory.CreateDirectory(diskPath);
            var instance = ScriptableObject.CreateInstance(settingsType);
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TryFacebookManifestRegenerate();
            LoadFacebookSettings();
            return true;
        }

        // ---- APPSFLYER ----
        private void DrawAppsFlyerSection()
        {
            EditorGUILayout.LabelField("AppsFlyer", EditorStyles.boldLabel);
            _appsFlyerDevKey = EditorGUILayout.TextField("Dev Key", _appsFlyerDevKey);
            _appsFlyerAppId = EditorGUILayout.TextField("App ID (iOS)", _appsFlyerAppId);
            _appsFlyerIsDebug = EditorGUILayout.Toggle("Debug Mode", _appsFlyerIsDebug);
        }
        private void DrawAppmetricaSection()
        {
            EditorGUILayout.LabelField("Appmetrica", EditorStyles.boldLabel);
            _appmetricaApikey = EditorGUILayout.TextField("API Key", _appmetricaApikey);
            _appmetricaEnbaleLogs = EditorGUILayout.Toggle("Debug Mode", _appmetricaEnbaleLogs);
        }

        // ---- GAME ANALYTICS (RESTORED TO 100%) ----
        private static Type _gameAnalyticsSettingsType;

        private static Type GetGameAnalyticsSettingsType()
        {
            if (_gameAnalyticsSettingsType != null) return _gameAnalyticsSettingsType;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType("GameAnalyticsSDK.Setup.Settings", false);
                    if (t != null && typeof(ScriptableObject).IsAssignableFrom(t))
                    {
                        _gameAnalyticsSettingsType = t;
                        break;
                    }
                }
                catch
                {
                }
            }

            return _gameAnalyticsSettingsType;
        }

        private static RuntimePlatform? ParseGameAnalyticsPlatformDisplayName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return null;
            if (string.Equals(displayName, "WSA", StringComparison.Ordinal)) return RuntimePlatform.WSAPlayerARM;
            return Enum.TryParse(displayName, out RuntimePlatform p) ? p : (RuntimePlatform?)null;
        }

        private static string GetPlayerSettingsVersionHint() => PlayerSettings.bundleVersion ?? "";

        private void DrawGameAnalyticsSection()
        {
            EditorGUILayout.LabelField("Game Analytics", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Cấu hình Game Key / Secret Key / Build trên asset:\n" + PathGameAnalyticsSettings,
                MessageType.None);

            var settingsType = GetGameAnalyticsSettingsType();
            if (settingsType == null)
            {
                EditorGUILayout.HelpBox("Không tìm thấy GameAnalytics SDK. Hãy cài đặt package để sử dụng.",
                    MessageType.Error);
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath(PathGameAnalyticsSettings, settingsType) as ScriptableObject;
            if (asset == null)
            {
                EditorGUILayout.HelpBox("Chưa có Settings.asset tại Resources/GameAnalytics.", MessageType.Warning);
                return;
            }

            if (GUILayout.Button("Chọn Settings.asset trong Project", GUILayout.Height(22)))
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            EditorGUILayout.Space(6);
            var so = new SerializedObject(asset);
            so.Update();

            var usePlayerBuild = so.FindProperty("UsePlayerSettingsBuildNumber");
            if (usePlayerBuild != null)
            {
                EditorGUILayout.PropertyField(usePlayerBuild,
                    new GUIContent("Auto build từ Player Settings", "Gửi tự động Application.version lên server."));
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Game Setup", EditorStyles.boldLabel);

            var getAvailable =
                settingsType.GetMethod("GetAvailablePlatforms", BindingFlags.Instance | BindingFlags.Public);
            var available = getAvailable?.Invoke(asset, null) as string[];

            if (available == null || available.Length == 0)
            {
                EditorGUILayout.HelpBox("Không còn platform nào để thêm.", MessageType.Info);
            }
            else
            {
                _gaAddPlatformDropdownIndex = Mathf.Clamp(_gaAddPlatformDropdownIndex, 0, available.Length - 1);
                _gaAddPlatformDropdownIndex =
                    EditorGUILayout.Popup("Platform to add", _gaAddPlatformDropdownIndex, available);
                if (GUILayout.Button("Add platform", GUILayout.Height(24)))
                {
                    var parsed = ParseGameAnalyticsPlatformDisplayName(available[_gaAddPlatformDropdownIndex]);
                    if (parsed.HasValue)
                    {
                        var add = settingsType.GetMethod("AddPlatform", BindingFlags.Instance | BindingFlags.Public);
                        add?.Invoke(asset, new object[] { parsed.Value });
                        EditorUtility.SetDirty(asset);
                        so.Update();

                        var defaultBuild = GetPlayerSettingsVersionHint();
                        var buildProp = so.FindProperty("Build");
                        if (buildProp != null && buildProp.arraySize > 0 && !string.IsNullOrEmpty(defaultBuild))
                            buildProp.GetArrayElementAtIndex(buildProp.arraySize - 1).stringValue = defaultBuild;

                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(asset);
                    }
                }
            }

            var platforms = so.FindProperty("Platforms");
            var gameKeys = so.FindProperty("gameKey");
            var secretKeys = so.FindProperty("secretKey");
            var builds = so.FindProperty("Build");

            if (platforms == null || gameKeys == null || secretKeys == null || builds == null) return;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Platforms Configured", EditorStyles.boldLabel);

            int removeAt = -1;
            for (int i = 0; i < platforms.arraySize; i++)
            {
                var plat = (RuntimePlatform)platforms.GetArrayElementAtIndex(i).intValue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Platform", plat.ToString(), EditorStyles.boldLabel);

                if (i < gameKeys.arraySize)
                    EditorGUILayout.PropertyField(gameKeys.GetArrayElementAtIndex(i), new GUIContent("Game Key"));
                if (i < secretKeys.arraySize)
                    EditorGUILayout.PropertyField(secretKeys.GetArrayElementAtIndex(i), new GUIContent("Secret Key"));
                if (i < builds.arraySize)
                    EditorGUILayout.PropertyField(builds.GetArrayElementAtIndex(i), new GUIContent("Build version"));

                string hint = GetPlayerSettingsVersionHint();
                if (usePlayerBuild != null && usePlayerBuild.boolValue &&
                    (plat == RuntimePlatform.Android || plat == RuntimePlatform.IPhonePlayer))
                {
                    EditorGUILayout.HelpBox($"Auto-build đang bật: Runtime sẽ gửi Version: {hint}", MessageType.Info);
                }

                if (GUILayout.Button("Remove platform")) removeAt = i;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            if (removeAt >= 0)
            {
                var remove =
                    settingsType.GetMethod("RemovePlatformAtIndex", BindingFlags.Instance | BindingFlags.Public);
                remove?.Invoke(asset, new object[] { removeAt });
                EditorUtility.SetDirty(asset);
                so.Update();
            }

            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(asset);
        }

        private static void SaveGameAnalyticsSettingsAsset()
        {
            var t = GetGameAnalyticsSettingsType();
            if (t == null) return;
            if (AssetDatabase.LoadAssetAtPath(PathGameAnalyticsSettings, t) == null) return;
            AssetDatabase.SaveAssets();
        }

        private static string ResolveGameAnalyticsPrefabPath()
        {
#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
            string p = GameAnalytics.WhereIs("GameAnalytics.prefab", "Prefab");
            if (!string.IsNullOrEmpty(p)) return p.Replace('\\', '/');
#endif
            return PathGameAnalyticsPrefabDefault;
        }

        private static bool SdkRootHasGameAnalyticsDescendant(GameObject sdkRoot)
        {
#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
            if (sdkRoot == null) return false;
            foreach (var ga in sdkRoot.GetComponentsInChildren<GameAnalytics>(true))
                if (ga != null && ga.gameObject != sdkRoot)
                    return true;
#endif
            return false;
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        // ---- IRONSOURCE ----
        private void DrawIronSourceSection()
        {
            EditorGUILayout.LabelField("IronSource (LevelPlay) Mediation", EditorStyles.boldLabel);
            _ironSourceAppKey = EditorGUILayout.TextField("App Key", _ironSourceAppKey);
            _ironSourceUseMultiAdUnitIds = EditorGUILayout.Toggle("Use Multi IDs", _ironSourceUseMultiAdUnitIds);
            _ironSourceEditorPlatform =
                (AdMobIdEditorPlatform)EditorGUILayout.EnumPopup("ID Platform", _ironSourceEditorPlatform);

            if (_ironSourceUseMultiAdUnitIds)
            {
                var list = GetSelectedIronSourceAdUnitIdList();
                DrawAdUnitIdList(ref list);
                SetSelectedIronSourceAdUnitIdList(list);
            }
            else
            {
                SetSelectedIronSourceBannerId(EditorGUILayout.TextField("Banner ID", GetSelectedIronSourceBannerId()));
                SetSelectedIronSourceInterstitialId(EditorGUILayout.TextField("Interstitial ID",
                    GetSelectedIronSourceInterstitialId()));
                SetSelectedIronSourceRewardedId(EditorGUILayout.TextField("Rewarded ID",
                    GetSelectedIronSourceRewardedId()));
            }

            _levelPlayAndroidAppKey = EditorGUILayout.TextField("Android App Key", _levelPlayAndroidAppKey);
            _levelPlayIOSAppKey = EditorGUILayout.TextField("iOS App Key", _levelPlayIOSAppKey);
        }
#endif

#if MAXSDK_DEPENDENCIES_INSTALLED
        // ---- APPLOVIN MAX ----
        private void DrawMaxSection()
        {
            EditorGUILayout.LabelField("AppLovin MAX Mediation", EditorStyles.boldLabel);
            _maxSdkKey = EditorGUILayout.TextField("SDK Key", _maxSdkKey);
            _maxUseMultiAdUnitIds = EditorGUILayout.Toggle("Use Multi IDs", _maxUseMultiAdUnitIds);
            _maxEditorPlatform = (AdMobIdEditorPlatform)EditorGUILayout.EnumPopup("ID Platform", _maxEditorPlatform);

            if (_maxUseMultiAdUnitIds)
            {
                var list = GetSelectedMaxAdUnitIdList();
                DrawAdUnitIdList(ref list);
                SetSelectedMaxAdUnitIdList(list);
            }
            else
            {
                SetSelectedMaxBannerId(EditorGUILayout.TextField("Banner ID", GetSelectedMaxBannerId()));
                SetSelectedMaxInterstitialId(EditorGUILayout.TextField("Interstitial ID",
                    GetSelectedMaxInterstitialId()));
                SetSelectedMaxRewardedId(EditorGUILayout.TextField("Rewarded ID", GetSelectedMaxRewardedId()));
                SetSelectedMaxAppOpenId(EditorGUILayout.TextField("App Open ID", GetSelectedMaxAppOpenId()));
            }
        }
#endif

        // ---- ADMOB ----
        private void DrawAdMobSection()
        {
            EditorGUILayout.LabelField("AdMob Fallback/AppOpen", EditorStyles.boldLabel);
            _admobEditorPlatform =
                (AdMobIdEditorPlatform)EditorGUILayout.EnumPopup("ID Platform", _admobEditorPlatform);
            _admobUseMultiAdUnitIds = EditorGUILayout.Toggle("Use Multi IDs", _admobUseMultiAdUnitIds);

            if (_admobUseMultiAdUnitIds)
            {
                var list = GetSelectedAdmobAdUnitIdList();
                DrawAdUnitIdList(ref list);
                SetSelectedAdmobAdUnitIdList(list);
            }
            else
            {
                SetSelectedAdmobBannerId(EditorGUILayout.TextField("Banner ID", GetSelectedAdmobBannerId()));
                SetSelectedAdmobInterstitialId(EditorGUILayout.TextField("Interstitial ID",
                    GetSelectedAdmobInterstitialId()));
                SetSelectedAdmobRewardedId(EditorGUILayout.TextField("Rewarded ID", GetSelectedAdmobRewardedId()));
                SetSelectedAdmobAppOpenId(EditorGUILayout.TextField("App Open ID", GetSelectedAdmobAppOpenId()));
            }

            _googleMobileAdsAndroidAppId = EditorGUILayout.TextField("Android App ID", _googleMobileAdsAndroidAppId);
            _googleMobileAdsIOSAppId = EditorGUILayout.TextField("iOS App ID", _googleMobileAdsIOSAppId);
        }

        private static void DrawAdUnitIdList(ref List<GameUp.SDK.AdUnitIdEntry> list)
        {
            if (list == null) list = new List<GameUp.SDK.AdUnitIdEntry>();
            NormalizeIntIds(list);
            EditorGUILayout.BeginVertical("box");
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i] ?? (list[i] = new GameUp.SDK.AdUnitIdEntry());
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(e.intId.ToString(), GUILayout.Width(30));
                e.adType = (GameUp.SDK.AdUnitType)EditorGUILayout.EnumPopup(e.adType, GUILayout.Width(100));
                e.nameId = EditorGUILayout.TextField(e.nameId ?? "", GUILayout.Width(100));
                e.id = EditorGUILayout.TextField(e.id ?? "", GUILayout.MinWidth(120));
                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    list.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add")) list.Add(new GameUp.SDK.AdUnitIdEntry());
            EditorGUILayout.EndVertical();
        }

        private static void NormalizeIntIds(List<GameUp.SDK.AdUnitIdEntry> list)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null)
                    list[i].intId = i + 1;
        }

        private void DrawFirebaseRemoteConfigSection()
        {
            EditorGUILayout.LabelField("Firebase Remote Config Defaults", EditorStyles.boldLabel);
            _rcInterCappingTime = EditorGUILayout.IntField("inter_capping_time", _rcInterCappingTime);
            _rcInterStartLevel = EditorGUILayout.IntField("inter_start_level", _rcInterStartLevel);
            _rcEnableBanner = EditorGUILayout.Toggle("enable_banner", _rcEnableBanner);

            EditorGUILayout.Space(10);
            DrawAdsManagerBannerAfterInitSection();
        }

        private void DrawAdsManagerBannerAfterInitSection()
        {
            _adsShowBannerAfterInit = EditorGUILayout.Toggle("Show Banner After Init", _adsShowBannerAfterInit);
            _adsShowBannerDelaySeconds = EditorGUILayout.FloatField("Delay Seconds", _adsShowBannerDelaySeconds);
            _adsShowBannerPlacementAfterInit = EditorGUILayout.TextField("Placement", _adsShowBannerPlacementAfterInit);
        }

        private static AdMobIdEditorPlatform GetDefaultAdmobEditorPlatform()
        {
            return EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS
                ? AdMobIdEditorPlatform.IOS
                : AdMobIdEditorPlatform.Android;
        }

        // ---- DATA GETTER/SETTERS ----
        private string GetSelectedAdmobBannerId() => _admobEditorPlatform == AdMobIdEditorPlatform.Android
            ? _admobBannerIdAndroid
            : _admobBannerIdIOS;

        private string GetSelectedAdmobInterstitialId() => _admobEditorPlatform == AdMobIdEditorPlatform.Android
            ? _admobInterstitialIdAndroid
            : _admobInterstitialIdIOS;

        private string GetSelectedAdmobRewardedId() => _admobEditorPlatform == AdMobIdEditorPlatform.Android
            ? _admobRewardedIdAndroid
            : _admobRewardedIdIOS;

        private string GetSelectedAdmobAppOpenId() => _admobEditorPlatform == AdMobIdEditorPlatform.Android
            ? _admobAppOpenIdAndroid
            : _admobAppOpenIdIOS;

        private void SetSelectedAdmobBannerId(string val)
        {
            if (_admobEditorPlatform == AdMobIdEditorPlatform.Android) _admobBannerIdAndroid = val;
            else _admobBannerIdIOS = val;
        }

        private void SetSelectedAdmobInterstitialId(string val)
        {
            if (_admobEditorPlatform == AdMobIdEditorPlatform.Android) _admobInterstitialIdAndroid = val;
            else _admobInterstitialIdIOS = val;
        }

        private void SetSelectedAdmobRewardedId(string val)
        {
            if (_admobEditorPlatform == AdMobIdEditorPlatform.Android) _admobRewardedIdAndroid = val;
            else _admobRewardedIdIOS = val;
        }

        private void SetSelectedAdmobAppOpenId(string val)
        {
            if (_admobEditorPlatform == AdMobIdEditorPlatform.Android) _admobAppOpenIdAndroid = val;
            else _admobAppOpenIdIOS = val;
        }

        private List<GameUp.SDK.AdUnitIdEntry> GetSelectedAdmobAdUnitIdList() =>
            _admobEditorPlatform == AdMobIdEditorPlatform.Android ? _admobAdUnitIdsAndroid : _admobAdUnitIdsIOS;

        private void SetSelectedAdmobAdUnitIdList(List<GameUp.SDK.AdUnitIdEntry> list)
        {
            if (_admobEditorPlatform == AdMobIdEditorPlatform.Android) _admobAdUnitIdsAndroid = list;
            else _admobAdUnitIdsIOS = list;
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private string GetSelectedIronSourceBannerId() => _ironSourceEditorPlatform == AdMobIdEditorPlatform.Android
            ? _ironSourceBannerIdAndroid
            : _ironSourceBannerIdIOS;

        private string GetSelectedIronSourceInterstitialId() =>
            _ironSourceEditorPlatform == AdMobIdEditorPlatform.Android
                ? _ironSourceInterstitialIdAndroid
                : _ironSourceInterstitialIdIOS;

        private string GetSelectedIronSourceRewardedId() => _ironSourceEditorPlatform == AdMobIdEditorPlatform.Android
            ? _ironSourceRewardedIdAndroid
            : _ironSourceRewardedIdIOS;

        private void SetSelectedIronSourceBannerId(string val)
        {
            if (_ironSourceEditorPlatform == AdMobIdEditorPlatform.Android) _ironSourceBannerIdAndroid = val;
            else _ironSourceBannerIdIOS = val;
        }

        private void SetSelectedIronSourceInterstitialId(string val)
        {
            if (_ironSourceEditorPlatform == AdMobIdEditorPlatform.Android) _ironSourceInterstitialIdAndroid = val;
            else _ironSourceInterstitialIdIOS = val;
        }

        private void SetSelectedIronSourceRewardedId(string val)
        {
            if (_ironSourceEditorPlatform == AdMobIdEditorPlatform.Android) _ironSourceRewardedIdAndroid = val;
            else _ironSourceRewardedIdIOS = val;
        }

        private List<GameUp.SDK.AdUnitIdEntry> GetSelectedIronSourceAdUnitIdList() =>
            _ironSourceEditorPlatform == AdMobIdEditorPlatform.Android
                ? _ironSourceAdUnitIdsAndroid
                : _ironSourceAdUnitIdsIOS;

        private void SetSelectedIronSourceAdUnitIdList(List<GameUp.SDK.AdUnitIdEntry> list)
        {
            if (_ironSourceEditorPlatform == AdMobIdEditorPlatform.Android) _ironSourceAdUnitIdsAndroid = list;
            else _ironSourceAdUnitIdsIOS = list;
        }
#endif

#if MAXSDK_DEPENDENCIES_INSTALLED
        private string GetSelectedMaxBannerId() => _maxEditorPlatform == AdMobIdEditorPlatform.Android
            ? _maxBannerIdAndroid
            : _maxBannerIdIOS;

        private string GetSelectedMaxInterstitialId() => _maxEditorPlatform == AdMobIdEditorPlatform.Android
            ? _maxInterstitialIdAndroid
            : _maxInterstitialIdIOS;

        private string GetSelectedMaxRewardedId() => _maxEditorPlatform == AdMobIdEditorPlatform.Android
            ? _maxRewardedIdAndroid
            : _maxRewardedIdIOS;

        private string GetSelectedMaxAppOpenId() => _maxEditorPlatform == AdMobIdEditorPlatform.Android
            ? _maxAppOpenIdAndroid
            : _maxAppOpenIdIOS;

        private void SetSelectedMaxBannerId(string val)
        {
            if (_maxEditorPlatform == AdMobIdEditorPlatform.Android) _maxBannerIdAndroid = val;
            else _maxBannerIdIOS = val;
        }

        private void SetSelectedMaxInterstitialId(string val)
        {
            if (_maxEditorPlatform == AdMobIdEditorPlatform.Android) _maxInterstitialIdAndroid = val;
            else _maxInterstitialIdIOS = val;
        }

        private void SetSelectedMaxRewardedId(string val)
        {
            if (_maxEditorPlatform == AdMobIdEditorPlatform.Android) _maxRewardedIdAndroid = val;
            else _maxRewardedIdIOS = val;
        }

        private void SetSelectedMaxAppOpenId(string val)
        {
            if (_maxEditorPlatform == AdMobIdEditorPlatform.Android) _maxAppOpenIdAndroid = val;
            else _maxAppOpenIdIOS = val;
        }

        private List<GameUp.SDK.AdUnitIdEntry> GetSelectedMaxAdUnitIdList() =>
            _maxEditorPlatform == AdMobIdEditorPlatform.Android ? _maxAdUnitIdsAndroid : _maxAdUnitIdsIOS;

        private void SetSelectedMaxAdUnitIdList(List<GameUp.SDK.AdUnitIdEntry> list)
        {
            if (_maxEditorPlatform == AdMobIdEditorPlatform.Android) _maxAdUnitIdsAndroid = list;
            else _maxAdUnitIdsIOS = list;
        }
#endif

        private void LoadFromPrefabs()
        {
            var errors = new List<string>();
            var pm = GetPrimaryMediationFromDefines();
            if (!LoadAppsFlyer()) errors.Add("Prefab missing: " + PathAppsFlyer);
            if (!LoadAppmetrica()) errors.Add("Prefab missing: " + PathAppmetrica);
            LoadFirebaseRemoteConfigUtils();
            LoadAdsManagerFromPrefab();

#if LEVELPLAY_DEPENDENCIES_INSTALLED
            if (pm == GameUp.SDK.AdsManager.PrimaryMediation.LevelPlay && !LoadIronSource())
                errors.Add("Prefab missing: " + PathIronSource);
#endif
#if MAXSDK_DEPENDENCIES_INSTALLED
            if (pm == GameUp.SDK.AdsManager.PrimaryMediation.Max && !LoadMax()) errors.Add("Prefab missing: " + PathMax);
#endif
            if (!LoadAdMob()) errors.Add("Prefab missing: " + PathAdMob);

            LoadGoogleMobileAdsSettings();
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            LoadLevelPlayMediationSettings();
#endif
            LoadFacebookSettings();
            _loadErrors = errors.Count > 0 ? string.Join("\n", errors) : null;
        }

        private void LoadFromSceneOrPrefabs()
        {
            _loadErrors = null;
            if (!CanSetupFromWritablePrefabs(out var lockReason))
            {
                _loadErrors = lockReason;
                return;
            }

            LoadFromPrefabs();
        }

        private bool LoadAppsFlyer()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PathAppsFlyer);
            if (go == null) return false;
            var type = Type.GetType("AppsFlyerObjectScript, AppsFlyer");
            if (type == null) return false;
            var comp = go.GetComponent(type);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            _appsFlyerDevKey = so.FindProperty("devKey")?.stringValue ?? "";
            _appsFlyerAppId = so.FindProperty("appID")?.stringValue ?? "";
            _appsFlyerIsDebug = so.FindProperty("isDebug")?.boolValue ?? false;
            return true;
        }
        
        private bool LoadAppmetrica()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PathAppmetrica);
            if (go == null) return false;
            var comp = go.GetComponent<AppMetricaActivator>();
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            _appmetricaApikey = so.FindProperty("apiKey")?.stringValue ?? "";
            _appmetricaEnbaleLogs = so.FindProperty("enableLogs")?.boolValue ?? false;
            return true;
        }


        private void LoadFirebaseRemoteConfigUtils()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PathSDK);
            if (go == null) return;
            var comp = go.GetComponent<GameUp.SDK.FirebaseRemoteConfigUtils>();
            if (comp == null) return;
            var so = new SerializedObject(comp);
            AssignInt(so, "inter_capping_time", ref _rcInterCappingTime);
            AssignInt(so, "inter_start_level", ref _rcInterStartLevel);
            AssignBool(so, "enable_banner", ref _rcEnableBanner);
        }

        private void LoadAdsManagerFromPrefab()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PathSDK);
            if (go == null) return;
            var comp = go.GetComponent<GameUp.SDK.AdsManager>();
            if (comp == null) return;
            var so = new SerializedObject(comp);
            AssignBool(so, "showBannerAfterInit", ref _adsShowBannerAfterInit);
            Assign(so, "showBannerPlacementAfterInit", ref _adsShowBannerPlacementAfterInit);
            AssignFloat(so, "showBannerDelaySeconds", ref _adsShowBannerDelaySeconds);
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private bool LoadIronSource()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PathIronSource);
            if (go == null) return false;
            var comp = go.GetComponentInChildren<GameUp.SDK.IronSourceAds>(true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            Assign(so, "levelPlayAppKey", ref _ironSourceAppKey);
            AssignIronSourceSingleIds(so);
            AssignBool(so, "useMultiAdUnitIds", ref _ironSourceUseMultiAdUnitIds);
            AssignIronSourceMultiIds(so);
            return true;
        }
#endif

#if MAXSDK_DEPENDENCIES_INSTALLED
        private bool LoadMax()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PathMax);
            if (go == null) return false;
            var comp = go.GetComponent<GameUp.SDK.MaxAds>();
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            Assign(so, "sdkKey", ref _maxSdkKey);
            AssignMaxSingleIds(so);
            AssignBool(so, "useMultiAdUnitIds", ref _maxUseMultiAdUnitIds);
            AssignMaxMultiIds(so);
            return true;
        }
#endif

        private bool LoadAdMob()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PathAdMob);
            if (go == null) return false;
            var comp = go.GetComponent<GameUp.SDK.AdmobAds>();
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            AssignAdmobSingleIds(so);
            AssignBool(so, "useMultiAdUnitIds", ref _admobUseMultiAdUnitIds);
            AssignAdmobMultiIds(so);
            return true;
        }

        private void LoadGoogleMobileAdsSettings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PathGoogleMobileAdsSettings);
            if (asset == null) return;
            var so = new SerializedObject(asset);
            Assign(so, "adMobAndroidAppId", ref _googleMobileAdsAndroidAppId);
            Assign(so, "adMobIOSAppId", ref _googleMobileAdsIOSAppId);
        }

        private bool SaveGoogleMobileAdsSettings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PathGoogleMobileAdsSettings);
            if (asset == null) return false;
            var so = new SerializedObject(asset);
            Set(so, "adMobAndroidAppId", _googleMobileAdsAndroidAppId);
            Set(so, "adMobIOSAppId", _googleMobileAdsIOSAppId);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return true;
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private void LoadLevelPlayMediationSettings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PathLevelPlayMediationSettings);
            if (asset == null) return;
            var so = new SerializedObject(asset);
            Assign(so, "AndroidAppKey", ref _levelPlayAndroidAppKey);
            Assign(so, "IOSAppKey", ref _levelPlayIOSAppKey);
        }

        private bool SaveLevelPlayMediationSettings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PathLevelPlayMediationSettings);
            if (asset == null) return false;
            var so = new SerializedObject(asset);
            Set(so, "AndroidAppKey", _levelPlayAndroidAppKey);
            Set(so, "IOSAppKey", _levelPlayIOSAppKey);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return true;
        }
#endif

        private static void Assign(SerializedObject so, string prop, ref string target)
        {
            var p = so.FindProperty(prop);
            if (p != null) target = p.stringValue ?? "";
        }

        private static void AssignInt(SerializedObject so, string prop, ref int target)
        {
            var p = so.FindProperty(prop);
            if (p != null) target = p.intValue;
        }

        private static void AssignBool(SerializedObject so, string prop, ref bool target)
        {
            var p = so.FindProperty(prop);
            if (p != null) target = p.boolValue;
        }

        private static void AssignFloat(SerializedObject so, string prop, ref float target)
        {
            var p = so.FindProperty(prop);
            if (p != null) target = p.floatValue;
        }

        private static void AssignAdUnitIdList(SerializedObject so, string propName,
            List<GameUp.SDK.AdUnitIdEntry> target)
        {
            if (target == null) return;
            target.Clear();
            var p = so.FindProperty(propName);
            if (p == null || !p.isArray) return;
            for (int i = 0; i < p.arraySize; i++)
            {
                var el = p.GetArrayElementAtIndex(i);
                if (el == null) continue;
                target.Add(new GameUp.SDK.AdUnitIdEntry
                {
                    adType = (GameUp.SDK.AdUnitType)(el.FindPropertyRelative("adType")?.enumValueIndex ?? 0),
                    intId = el.FindPropertyRelative("intId")?.intValue ?? 0,
                    nameId = el.FindPropertyRelative("nameId")?.stringValue ?? "",
                    id = el.FindPropertyRelative("id")?.stringValue ?? ""
                });
            }

            NormalizeIntIds(target);
        }

        private static void SetAdUnitIdList(SerializedObject so, string propName, List<GameUp.SDK.AdUnitIdEntry> source)
        {
            var p = so.FindProperty(propName);
            if (p == null || !p.isArray) return;
            source ??= new List<GameUp.SDK.AdUnitIdEntry>();
            NormalizeIntIds(source);
            p.arraySize = source.Count;
            for (int i = 0; i < source.Count; i++)
            {
                var el = p.GetArrayElementAtIndex(i);
                var e = source[i] ?? new GameUp.SDK.AdUnitIdEntry();
                if (el.FindPropertyRelative("adType") != null)
                    el.FindPropertyRelative("adType").enumValueIndex = (int)e.adType;
                if (el.FindPropertyRelative("intId") != null) el.FindPropertyRelative("intId").intValue = e.intId;
                if (el.FindPropertyRelative("nameId") != null)
                    el.FindPropertyRelative("nameId").stringValue = e.nameId ?? "";
                if (el.FindPropertyRelative("id") != null) el.FindPropertyRelative("id").stringValue = e.id ?? "";
            }
        }

        private void SaveConfiguration()
        {
            if (!CanSetupFromWritablePrefabs(out var lockReason))
            {
                _saveErrors = lockReason;
                return;
            }

            var errors = new List<string>();
            var pm = GetPrimaryMediationFromDefines();

            SaveConfigurationToWritablePrefabAssets(errors);

            if (!SaveGoogleMobileAdsSettings()) errors.Add(PathGoogleMobileAdsSettings);
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            if (!SaveLevelPlayMediationSettings()) errors.Add(PathLevelPlayMediationSettings);
#endif
            SaveGameAnalyticsSettingsAsset(); // ĐÃ KHÔI PHỤC: Lưu file asset cấu hình GA
            SaveFacebookSettingsAsset();

            if (TryGetSdkSceneRootMatchingPrefab(PathSDK, out var sdkRoot))
            {
                SaveSceneAppsFlyerObject(sdkRoot);
                SaveSceneAppmetricaObject(sdkRoot);
                SaveSceneFirebaseRemoteConfigUtils(sdkRoot);
                SaveSceneAdsManager(sdkRoot);
#if LEVELPLAY_DEPENDENCIES_INSTALLED
                if (pm == GameUp.SDK.AdsManager.PrimaryMediation.LevelPlay) SaveSceneIronSource(sdkRoot);
#endif
#if MAXSDK_DEPENDENCIES_INSTALLED
                if (pm == GameUp.SDK.AdsManager.PrimaryMediation.Max) SaveSceneMax(sdkRoot);
#endif
                SaveSceneAdMob(sdkRoot);
                EditorSceneManager.MarkSceneDirty(sdkRoot.scene);
                EditorSceneManager.SaveOpenScenes();
            }

            if (errors.Count > 0) _saveErrors = string.Join("\n", errors);
            else Debug.Log("[GameUpSDK] Configuration Saved Successfully!");
        }

        private static bool TryGetSdkSceneRootMatchingPrefab(string prefabPath, out GameObject sdkRoot)
        {
            sdkRoot = null;
            var normalized = prefabPath.Replace('\\', '/');
            var managers = Resources.FindObjectsOfTypeAll<GameUp.SDK.AdsManager>();
            foreach (var am in managers)
            {
                if (am == null || EditorUtility.IsPersistent(am)) continue;
                var go = am.gameObject;
                if (go == null || !go.scene.IsValid()) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (src != null && string.Equals(AssetDatabase.GetAssetPath(src).Replace('\\', '/'), normalized,
                        StringComparison.OrdinalIgnoreCase))
                {
                    sdkRoot = go;
                    return true;
                }
            }

            return false;
        }

        private bool SaveSceneAppsFlyerObject(GameObject sdkRoot)
        {
            var type = Type.GetType("AppsFlyerObjectScript, AppsFlyer");
            if (type == null) return true;
            var comp = sdkRoot.GetComponentInChildren(type, true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            Set(so, "devKey", _appsFlyerDevKey);
            Set(so, "appID", _appsFlyerAppId);
            SetBool(so, "isDebug", _appsFlyerIsDebug);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            return true;
        }
        
        private bool SaveSceneAppmetricaObject(GameObject sdkRoot)
        {
            var comp = sdkRoot.GetComponentInChildren<AppMetricaActivator>(true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            Set(so, "apiKey", _appmetricaApikey);
            SetBool(so, "enableLogs", _appmetricaEnbaleLogs);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            return true;
        }

        private bool SaveSceneAdsManager(GameObject sdkRoot)
        {
            var comp = sdkRoot.GetComponent<GameUp.SDK.AdsManager>();
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            SetBool(so, "showBannerAfterInit", _adsShowBannerAfterInit);
            Set(so, "showBannerPlacementAfterInit", _adsShowBannerPlacementAfterInit);
            SetFloat(so, "showBannerDelaySeconds", _adsShowBannerDelaySeconds);
            so.ApplyModifiedPropertiesWithoutUndo();
            return PersistAdsManagerLists(sdkRoot, true);
        }

        private static bool PersistAdsManagerLists(GameObject sdkRoot, bool record)
        {
            var comp = sdkRoot.GetComponent<GameUp.SDK.AdsManager>();
            if (comp == null) return false;
            var lpList = new List<GameUp.SDK.IronSourceAds>();
            var amList = new List<GameUp.SDK.AdmobAds>();
            var mxList = new List<GameUp.SDK.MaxAds>();

            foreach (var c in sdkRoot.GetComponentsInChildren<GameUp.SDK.IronSourceAds>(true))
                if (c.gameObject != sdkRoot)
                    lpList.Add(c);
            foreach (var c in sdkRoot.GetComponentsInChildren<GameUp.SDK.AdmobAds>(true))
                if (c.gameObject != sdkRoot)
                    amList.Add(c);
            foreach (var c in sdkRoot.GetComponentsInChildren<GameUp.SDK.MaxAds>(true))
                if (c.gameObject != sdkRoot)
                    mxList.Add(c);

            var so = new SerializedObject(comp);
            var lp = so.FindProperty("levelPlayAdsBehaviours");
            var ad = so.FindProperty("admobAdsBehaviours");
            var mx = so.FindProperty("maxAdsBehaviours");

            if (lp != null)
            {
                lp.arraySize = lpList.Count;
                for (int i = 0; i < lpList.Count; i++) lp.GetArrayElementAtIndex(i).objectReferenceValue = lpList[i];
            }

            if (ad != null)
            {
                ad.arraySize = amList.Count;
                for (int i = 0; i < amList.Count; i++) ad.GetArrayElementAtIndex(i).objectReferenceValue = amList[i];
            }

            if (mx != null)
            {
                mx.arraySize = mxList.Count;
                for (int i = 0; i < mxList.Count; i++) mx.GetArrayElementAtIndex(i).objectReferenceValue = mxList[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            if (record) PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            return true;
        }

        private void SaveConfigurationToWritablePrefabAssets(List<string> errors)
        {
            if (!PathSDK.StartsWith("Assets/")) return;
            var root = PrefabUtility.LoadPrefabContents(PathSDK);
            if (root == null) return;
            var pm = GetPrimaryMediationFromDefines();

            try
            {
                SavePrefabAppsFlyerObject(root);
                SavePrefabFirebaseRemoteConfigUtils(root);
                SavePrefabAdsManager(root);
#if LEVELPLAY_DEPENDENCIES_INSTALLED
                if (pm == GameUp.SDK.AdsManager.PrimaryMediation.LevelPlay) SavePrefabIronSource(root);
#endif
#if MAXSDK_DEPENDENCIES_INSTALLED
                if (pm == GameUp.SDK.AdsManager.PrimaryMediation.Max) SavePrefabMax(root);
#endif
                SavePrefabAdMob(root);
                PrefabUtility.SaveAsPrefabAsset(root, PathSDK);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
            TrySaveIronSourcePrefabAsset(errors);
#endif
#if MAXSDK_DEPENDENCIES_INSTALLED
            TrySaveMaxPrefabAsset(errors);
#endif
            TrySaveAdMobPrefabAsset(errors);
            AssetDatabase.SaveAssets();
        }

        private bool SavePrefabAppsFlyerObject(GameObject root)
        {
            var type = Type.GetType("AppsFlyerObjectScript, AppsFlyer");
            if (type == null) return true;
            var comp = root.GetComponentInChildren(type, true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            Set(so, "devKey", _appsFlyerDevKey);
            Set(so, "appID", _appsFlyerAppId);
            SetBool(so, "isDebug", _appsFlyerIsDebug);
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private bool SavePrefabFirebaseRemoteConfigUtils(GameObject root)
        {
            var comp = root.GetComponent<GameUp.SDK.FirebaseRemoteConfigUtils>();
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            SetInt(so, "inter_capping_time", _rcInterCappingTime);
            SetInt(so, "inter_start_level", _rcInterStartLevel);
            SetBool(so, "enable_banner", _rcEnableBanner);
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private bool SavePrefabAdsManager(GameObject root)
        {
            var comp = root.GetComponent<GameUp.SDK.AdsManager>();
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            SetBool(so, "showBannerAfterInit", _adsShowBannerAfterInit);
            Set(so, "showBannerPlacementAfterInit", _adsShowBannerPlacementAfterInit);
            SetFloat(so, "showBannerDelaySeconds", _adsShowBannerDelaySeconds);
            so.ApplyModifiedPropertiesWithoutUndo();
            return PersistAdsManagerLists(root, false);
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private bool SavePrefabIronSource(GameObject root)
        {
            var comp = root.GetComponentInChildren<GameUp.SDK.IronSourceAds>(true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            Set(so, "levelPlayAppKey", _ironSourceAppKey);
            SetIronSourceSingleIds(so);
            SetBool(so, "useMultiAdUnitIds", _ironSourceUseMultiAdUnitIds);
            SetIronSourceMultiIds(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
#endif

#if MAXSDK_DEPENDENCIES_INSTALLED
        private bool SavePrefabMax(GameObject root)
        {
            var comp = root.GetComponentInChildren<GameUp.SDK.MaxAds>(true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            Set(so, "sdkKey", _maxSdkKey);
            SetMaxSingleIds(so);
            SetBool(so, "useMultiAdUnitIds", _maxUseMultiAdUnitIds);
            SetMaxMultiIds(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
#endif

        private bool SavePrefabAdMob(GameObject root)
        {
            var comp = root.GetComponentInChildren<GameUp.SDK.AdmobAds>(true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            SetAdmobSingleIds(so);
            SetBool(so, "useMultiAdUnitIds", _admobUseMultiAdUnitIds);
            SetAdmobMultiIds(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

#if MAXSDK_DEPENDENCIES_INSTALLED
        private void TrySaveMaxPrefabAsset(List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PathMax) == null) return;
            var root = PrefabUtility.LoadPrefabContents(PathMax);
            try
            {
                var comp = root.GetComponent<GameUp.SDK.MaxAds>();
                var so = new SerializedObject(comp);
                Set(so, "sdkKey", _maxSdkKey);
                SetMaxSingleIds(so);
                SetBool(so, "useMultiAdUnitIds", _maxUseMultiAdUnitIds);
                SetMaxMultiIds(so);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PathMax);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
#endif

        private void TrySaveAdMobPrefabAsset(List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PathAdMob) == null) return;
            var root = PrefabUtility.LoadPrefabContents(PathAdMob);
            try
            {
                var comp = root.GetComponent<GameUp.SDK.AdmobAds>();
                var so = new SerializedObject(comp);
                SetAdmobSingleIds(so);
                SetBool(so, "useMultiAdUnitIds", _admobUseMultiAdUnitIds);
                SetAdmobMultiIds(so);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PathAdMob);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private void TrySaveIronSourcePrefabAsset(List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PathIronSource) == null) return;
            var root = PrefabUtility.LoadPrefabContents(PathIronSource);
            try
            {
                var comp = root.GetComponentInChildren<GameUp.SDK.IronSourceAds>(true);
                var so = new SerializedObject(comp);
                Set(so, "levelPlayAppKey", _ironSourceAppKey);
                SetIronSourceSingleIds(so);
                SetBool(so, "useMultiAdUnitIds", _ironSourceUseMultiAdUnitIds);
                SetIronSourceMultiIds(so);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PathIronSource);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
#endif

        private bool SaveSceneFirebaseRemoteConfigUtils(GameObject sdkRoot)
        {
            var comp = sdkRoot.GetComponent<GameUp.SDK.FirebaseRemoteConfigUtils>();
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            SetInt(so, "inter_capping_time", _rcInterCappingTime);
            SetInt(so, "inter_start_level", _rcInterStartLevel);
            SetBool(so, "enable_banner", _rcEnableBanner);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            return true;
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private bool SaveSceneIronSource(GameObject sdkRoot)
        {
            var comp = sdkRoot.GetComponentInChildren<GameUp.SDK.IronSourceAds>(true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            Set(so, "levelPlayAppKey", _ironSourceAppKey);
            SetIronSourceSingleIds(so);
            SetBool(so, "useMultiAdUnitIds", _ironSourceUseMultiAdUnitIds);
            SetIronSourceMultiIds(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            return true;
        }
#endif

#if MAXSDK_DEPENDENCIES_INSTALLED
        private bool SaveSceneMax(GameObject sdkRoot)
        {
            var comp = sdkRoot.GetComponentInChildren<GameUp.SDK.MaxAds>(true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            Set(so, "sdkKey", _maxSdkKey);
            SetMaxSingleIds(so);
            SetBool(so, "useMultiAdUnitIds", _maxUseMultiAdUnitIds);
            SetMaxMultiIds(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            return true;
        }
#endif

        private bool SaveSceneAdMob(GameObject sdkRoot)
        {
            var comp = sdkRoot.GetComponentInChildren<GameUp.SDK.AdmobAds>(true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            SetAdmobSingleIds(so);
            SetBool(so, "useMultiAdUnitIds", _admobUseMultiAdUnitIds);
            SetAdmobMultiIds(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            return true;
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private void AssignIronSourceSingleIds(SerializedObject so)
        {
            Assign(so, "bannerAdUnitIdAndroid", ref _ironSourceBannerIdAndroid);
            Assign(so, "interstitialAdUnitIdAndroid", ref _ironSourceInterstitialIdAndroid);
            Assign(so, "rewardedVideoAdUnitIdAndroid", ref _ironSourceRewardedIdAndroid);
            Assign(so, "bannerAdUnitIdIOS", ref _ironSourceBannerIdIOS);
            Assign(so, "interstitialAdUnitIdIOS", ref _ironSourceInterstitialIdIOS);
            Assign(so, "rewardedVideoAdUnitIdIOS", ref _ironSourceRewardedIdIOS);
        }

        private void SetIronSourceSingleIds(SerializedObject so)
        {
            Set(so, "bannerAdUnitIdAndroid", _ironSourceBannerIdAndroid);
            Set(so, "interstitialAdUnitIdAndroid", _ironSourceInterstitialIdAndroid);
            Set(so, "rewardedVideoAdUnitIdAndroid", _ironSourceRewardedIdAndroid);
            Set(so, "bannerAdUnitIdIOS", _ironSourceBannerIdIOS);
            Set(so, "interstitialAdUnitIdIOS", _ironSourceInterstitialIdIOS);
            Set(so, "rewardedVideoAdUnitIdIOS", _ironSourceRewardedIdIOS);
        }

        private void AssignIronSourceMultiIds(SerializedObject so)
        {
            AssignAdUnitIdList(so, "adUnitIdsAndroid", _ironSourceAdUnitIdsAndroid);
            AssignAdUnitIdList(so, "adUnitIdsIOS", _ironSourceAdUnitIdsIOS);
        }

        private void SetIronSourceMultiIds(SerializedObject so)
        {
            SetAdUnitIdList(so, "adUnitIdsAndroid", _ironSourceAdUnitIdsAndroid);
            SetAdUnitIdList(so, "adUnitIdsIOS", _ironSourceAdUnitIdsIOS);
        }
#endif

#if MAXSDK_DEPENDENCIES_INSTALLED
        private void AssignMaxSingleIds(SerializedObject so)
        {
            Assign(so, "bannerAdUnitIdAndroid", ref _maxBannerIdAndroid);
            Assign(so, "interstitialAdUnitIdAndroid", ref _maxInterstitialIdAndroid);
            Assign(so, "rewardedAdUnitIdAndroid", ref _maxRewardedIdAndroid);
            Assign(so, "appOpenAdUnitIdAndroid", ref _maxAppOpenIdAndroid);
            Assign(so, "bannerAdUnitIdIOS", ref _maxBannerIdIOS);
            Assign(so, "interstitialAdUnitIdIOS", ref _maxInterstitialIdIOS);
            Assign(so, "rewardedAdUnitIdIOS", ref _maxRewardedIdIOS);
            Assign(so, "appOpenAdUnitIdIOS", ref _maxAppOpenIdIOS);
        }

        private void SetMaxSingleIds(SerializedObject so)
        {
            Set(so, "bannerAdUnitIdAndroid", _maxBannerIdAndroid);
            Set(so, "interstitialAdUnitIdAndroid", _maxInterstitialIdAndroid);
            Set(so, "rewardedAdUnitIdAndroid", _maxRewardedIdAndroid);
            Set(so, "appOpenAdUnitIdAndroid", _maxAppOpenIdAndroid);
            Set(so, "bannerAdUnitIdIOS", _maxBannerIdIOS);
            Set(so, "interstitialAdUnitIdIOS", _maxInterstitialIdIOS);
            Set(so, "rewardedAdUnitIdIOS", _maxRewardedIdIOS);
            Set(so, "appOpenAdUnitIdIOS", _maxAppOpenIdIOS);
        }

        private void AssignMaxMultiIds(SerializedObject so)
        {
            AssignAdUnitIdList(so, "adUnitIdsAndroid", _maxAdUnitIdsAndroid);
            AssignAdUnitIdList(so, "adUnitIdsIOS", _maxAdUnitIdsIOS);
        }

        private void SetMaxMultiIds(SerializedObject so)
        {
            SetAdUnitIdList(so, "adUnitIdsAndroid", _maxAdUnitIdsAndroid);
            SetAdUnitIdList(so, "adUnitIdsIOS", _maxAdUnitIdsIOS);
        }
#endif

        private void AssignAdmobSingleIds(SerializedObject so)
        {
            Assign(so, "bannerAdUnitIdAndroid", ref _admobBannerIdAndroid);
            Assign(so, "interstitialAdUnitIdAndroid", ref _admobInterstitialIdAndroid);
            Assign(so, "rewardedAdUnitIdAndroid", ref _admobRewardedIdAndroid);
            Assign(so, "appOpenAdUnitIdAndroid", ref _admobAppOpenIdAndroid);
            Assign(so, "bannerAdUnitIdIOS", ref _admobBannerIdIOS);
            Assign(so, "interstitialAdUnitIdIOS", ref _admobInterstitialIdIOS);
            Assign(so, "rewardedAdUnitIdIOS", ref _admobRewardedIdIOS);
            Assign(so, "appOpenAdUnitIdIOS", ref _admobAppOpenIdIOS);
        }

        private void SetAdmobSingleIds(SerializedObject so)
        {
            Set(so, "bannerAdUnitIdAndroid", _admobBannerIdAndroid);
            Set(so, "interstitialAdUnitIdAndroid", _admobInterstitialIdAndroid);
            Set(so, "rewardedAdUnitIdAndroid", _admobRewardedIdAndroid);
            Set(so, "appOpenAdUnitIdAndroid", _admobAppOpenIdAndroid);
            Set(so, "bannerAdUnitIdIOS", _admobBannerIdIOS);
            Set(so, "interstitialAdUnitIdIOS", _admobInterstitialIdIOS);
            Set(so, "rewardedAdUnitIdIOS", _admobRewardedIdIOS);
            Set(so, "appOpenAdUnitIdIOS", _admobAppOpenIdIOS);
        }

        private void AssignAdmobMultiIds(SerializedObject so)
        {
            AssignAdUnitIdList(so, "adUnitIdsAndroid", _admobAdUnitIdsAndroid);
            AssignAdUnitIdList(so, "adUnitIdsIOS", _admobAdUnitIdsIOS);
        }

        private void SetAdmobMultiIds(SerializedObject so)
        {
            SetAdUnitIdList(so, "adUnitIdsAndroid", _admobAdUnitIdsAndroid);
            SetAdUnitIdList(so, "adUnitIdsIOS", _admobAdUnitIdsIOS);
        }

        private static void Set(SerializedObject so, string propName, string value)
        {
            var p = so.FindProperty(propName);
            if (p != null) p.stringValue = value ?? "";
        }

        private static void SetInt(SerializedObject so, string propName, int value)
        {
            var p = so.FindProperty(propName);
            if (p != null) p.intValue = value;
        }

        private static void SetBool(SerializedObject so, string propName, bool value)
        {
            var p = so.FindProperty(propName);
            if (p != null) p.boolValue = value;
        }

        private static void SetDrop(SerializedObject so, string propName, int value)
        {
            var p = so.FindProperty(propName);
            if (p != null) p.enumValueIndex = value;
        }

        private static void SetFloat(SerializedObject so, string propName, float value)
        {
            var p = so.FindProperty(propName);
            if (p != null) p.floatValue = value;
        }

        private static void EnsureGameAnalyticsChildUnderSdkObject(GameObject sdkRoot)
        {
#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
            if (sdkRoot == null || SdkRootHasGameAnalyticsDescendant(sdkRoot)) return;
            string gaPath = ResolveGameAnalyticsPrefabPath();
            var gaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(gaPath);
            if (gaPrefab != null)
            {
                var child = (GameObject)PrefabUtility.InstantiatePrefab(gaPrefab, sdkRoot.transform);
                if (child != null) child.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
#endif
        }

        private static void EnsureGameAnalyticsNestedInSdkPrefabAsset(string sdkPrefabPath)
        {
#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
            var root = PrefabUtility.LoadPrefabContents(sdkPrefabPath);
            if (root != null)
            {
                EnsureGameAnalyticsChildUnderSdkObject(root);
                PrefabUtility.SaveAsPrefabAsset(root, sdkPrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
            }
#endif
        }

        private static bool TryClonePackagePrefabsToWritable(out string errorMessage)
        {
            errorMessage = null;
            if (!AssetDatabase.IsValidFolder("Assets/SDK")) AssetDatabase.CreateFolder("Assets", "SDK");
            if (!AssetDatabase.IsValidFolder(WritablePrefabsRoot)) AssetDatabase.CreateFolder("Assets/SDK", "Prefabs");

            var srcDir = GetPackagePrefabDirectory().Replace('\\', '/');
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { srcDir });
            foreach (var g in guids)
            {
                var src = AssetDatabase.GUIDToAssetPath(g);
                var dst = WritablePrefabsRoot + "/" + Path.GetFileName(src);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(dst) == null) AssetDatabase.CopyAsset(src, dst);
            }

            AssetDatabase.Refresh();
            EnsureGameAnalyticsNestedInSdkPrefabAsset(WritablePrefabsRoot + "/SDK.prefab");
            return true;
        }
    }
}