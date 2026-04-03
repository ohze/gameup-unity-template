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
                    var pkgInfoType = Type.GetType(
                        "UnityEditor.PackageManager.PackageInfo, UnityEditor");
                    if (pkgInfoType != null)
                    {
                        var method = pkgInfoType.GetMethod(
                            "FindForAssembly",
                            BindingFlags.Static | BindingFlags.Public,
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
                catch { /* fallback below */ }

                // Fallback: project gốc
                _packageRoot = "Assets/GameUpSDK";
                return _packageRoot;
            }
        }

        /// <summary>Bản prefab có thể chỉnh sửa khi SDK cài qua UPM (Packages read-only).</summary>
        private const string WritablePrefabsRoot = "Assets/_MainProject/Prefabs/Core/SDK";

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
#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private static string PathIronSource => GetPrefabDirectory() + "/IronSourceAds.prefab";
#endif
        private static string PathAdMob => GetPrefabDirectory() + "/AdmobAds.prefab";

        private const string PathGoogleMobileAdsSettings = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
        private const string PathLevelPlayMediationSettings = "Assets/LevelPlay/Resources/LevelPlayMediationSettings.asset";
        private const string PathGameAnalyticsSettings = "Assets/Resources/GameAnalytics/Settings.asset";
        /// <summary>Mặc định GameAnalytics plugin; <see cref="GameAnalytics.WhereIs"/> dùng khi đường dẫn lệch.</summary>
        private const string PathGameAnalyticsPrefabDefault = "Assets/GameAnalytics/Plugins/Prefabs/GameAnalytics.prefab";
        /// <summary>Đồng bộ với Facebook.Unity.Settings.FacebookSettings (SDK 18.x).</summary>
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
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            IronSourceMediation,
#endif
            AdMobAppOpen,
            FirebaseRemoteConfig,
        }

        // FacebookSettings.asset (appLabels[0], appIds[0], clientTokens[0], androidKeystorePath)
        private string _facebookAppLabel = "";
        private string _facebookAppId = "";
        private string _facebookClientToken = "";
        private string _facebookAndroidKeystorePath = "";

        // AppsFlyer — AppsFlyerObjectScript (devKey, appID, isDebug); init SDK trên AppsFlyerObject, không trùng với AppsFlyerUtils
        private string _appsFlyerDevKey = "";
        private string _appsFlyerAppId = "";
        private bool _appsFlyerIsDebug = false;

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        // IronSource (IronSourceAds: levelPlayAppKey, bannerAdUnitId, interstitialAdUnitId, rewardedVideoAdUnitId)
        private string _ironSourceAppKey = "";
        private string _ironSourceBannerId = "";
        private string _ironSourceInterstitialId = "";
        private string _ironSourceRewardedId = "";

        // LevelPlay Mediation Settings (LevelPlayMediationSettings.asset)
        private string _levelPlayAndroidAppKey = "";
        private string _levelPlayIOSAppKey = "";
#endif

        // AdMob (AdmobAds: bannerAdUnitId, interstitialAdUnitId, rewardedAdUnitId, appOpenAdUnitId)
        private string _admobBannerId = "";
        private string _admobInterstitialId = "";
        private string _admobRewardedId = "";
        private string _admobAppOpenId = "";

        // Google Mobile Ads App IDs (GoogleMobileAdsSettings.asset)
        private string _googleMobileAdsAndroidAppId = "";
        private string _googleMobileAdsIOSAppId = "";

        // FirebaseRemoteConfigUtils on SDK.prefab (default values, sync from Remote at runtime)
        private int _rcInterCappingTime = 120;
        private int _rcInterStartLevel = 3;
        private bool _rcEnableRateApp = false;
        private int _rcLevelStartShowRateApp = 5;
        private bool _rcNoInternetPopupEnable = true;
        private bool _rcEnableBanner = true;

        /// <summary>Dropdown "Platform to add" trong tab Game Analytics (GameAnalytics Settings).</summary>
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
            LoadFromSceneOrPrefabs();

            _lastPrimaryMediation = GetPrimaryMediationFromDefines();
            BuildTabsForPrimaryMediation(_lastPrimaryMediation, keepActiveTab: false);
        }

        /// <summary>True khi prefab SDK nằm trong Packages (read-only) và chưa có bản clone trong Assets/SDK/Prefabs.</summary>
        private static bool RequiresPrefabCloneBeforeSetup()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(WritablePrefabsRoot + "/SDK.prefab") == null;
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
                EditorGUILayout.HelpBox(
                    "SDK đang nằm trong Packages (read-only) nên không thể lưu cấu hình vào prefab.\n\n" +
                    "Bạn vẫn có thể cấu hình bình thường. Khi bấm \"Save Configuration\", cấu hình sẽ được lưu " +
                    "trực tiếp lên SDK object hiện có trong Scene (prefab instance overrides).\n\n" +
                    "Nếu bạn muốn có một bản prefab có thể chỉnh sửa trong project, hãy clone prefab từ:\n" +
                    GetPackagePrefabDirectory().Replace('\\', '/') + "\n" +
                    "sang:\n" + WritablePrefabsRoot,
                    MessageType.Info);
                EditorGUILayout.Space(8);
                if (GUILayout.Button("Clone Prefab từ Package → Assets/SDK/Prefabs (tùy chọn)", GUILayout.Height(30)))
                {
                    if (TryClonePackagePrefabsToWritable(out var cloneErr))
                    {
                        LoadFromSceneOrPrefabs();
                        Debug.Log("[GameUpSDK] Đã clone prefab sang " + WritablePrefabsRoot + " — có thể chỉnh sửa và lưu prefab.");
                    }
                    else if (!string.IsNullOrEmpty(cloneErr))
                        _saveErrors = cloneErr;
                }
                EditorGUILayout.Space(6);
            }

            // Nếu user đổi Primary Mediation ở Dependencies window, setup window tự cập nhật tab cho đúng.
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
            if (_tabDrawers.TryGetValue(_activeTab, out var draw))
                draw?.Invoke();

            EditorGUILayout.Space(16);
            if (GUILayout.Button("Save Configuration", GUILayout.Height(32)))
            {
                SaveConfiguration();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox("Thêm SDK vào scene hiện tại (sẽ tạo instance từ prefab SDK).", MessageType.None);
            if (GUILayout.Button("Tạo SDK trong Scene hiện tại", GUILayout.Height(28)))
            {
                CreateSDKInCurrentScene();
            }

            EditorGUILayout.EndScrollView();
        }

        private static GameUp.SDK.AdsManager.PrimaryMediation GetPrimaryMediationFromDefines()
        {
            // Default LevelPlay nếu chưa set gì.
            try
            {
                string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
                if (!string.IsNullOrEmpty(symbols) && symbols.Contains(GameUp.SDK.GUDefinetion.PrimaryMediationAdMob))
                    return GameUp.SDK.AdsManager.PrimaryMediation.AdMob;
            }
            catch
            {
                // ignore
            }

            return GameUp.SDK.AdsManager.PrimaryMediation.LevelPlay;
        }

        /// <summary>Primary Mediation = LevelPlay (scripting defines).</summary>
        private static bool IsPrimaryMediationLevelPlay()
        {
            return GetPrimaryMediationFromDefines() == GameUp.SDK.AdsManager.PrimaryMediation.LevelPlay;
        }

        /// <summary>
        /// Tab IronSource + asset LevelPlay chỉ khi đã cài pack LevelPlay (<see cref="GUDefinetion.LevelPlayDepsInstalled"/>)
        /// và chọn mediation LevelPlay — tránh compile/reference khi chỉ dùng AdMob.
        /// </summary>
        private static bool IsIronSourceSetupSectionAvailable()
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            return IsPrimaryMediationLevelPlay();
#else
            return false;
#endif
        }

        private void BuildTabsForPrimaryMediation(GameUp.SDK.AdsManager.PrimaryMediation pm, bool keepActiveTab)
        {
            // Preserve current visible tab by name when possible
            string previousTabName = (_tabs != null && _activeTab >= 0 && _activeTab < _tabs.Length)
                ? _tabs[_activeTab]
                : null;

            var tabs = new List<SetupTab>
            {
                SetupTab.Facebook,
                SetupTab.AppsFlyer,
                SetupTab.GameAnalytics,
            };

            if (pm == GameUp.SDK.AdsManager.PrimaryMediation.LevelPlay)
            {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
                tabs.Add(SetupTab.IronSourceMediation);
#endif
                tabs.Add(SetupTab.AdMobAppOpen);
            }
            else
            {
                tabs.Add(SetupTab.AdMobAppOpen);
            }

            tabs.Add(SetupTab.FirebaseRemoteConfig);

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
                        case SetupTab.GameAnalytics: DrawGameAnalyticsSection(); break;
#if LEVELPLAY_DEPENDENCIES_INSTALLED
                        case SetupTab.IronSourceMediation: DrawIronSourceSection(); break;
#endif
                        case SetupTab.AdMobAppOpen: DrawAdMobSection(); break;
                        case SetupTab.FirebaseRemoteConfig: DrawFirebaseRemoteConfigSection(); break;
                    }
                };
            }

            if (keepActiveTab && !string.IsNullOrEmpty(previousTabName))
            {
                int idx = Array.IndexOf(_tabs, previousTabName);
                _activeTab = idx >= 0 ? idx : GetDefaultTabIndexFor(pm);
            }
            else
            {
                _activeTab = GetDefaultTabIndexFor(pm);
            }
        }

        private static int GetDefaultTabIndexFor(GameUp.SDK.AdsManager.PrimaryMediation pm)
        {
            // Mở tab ads sau Game Analytics (index 3): IronSource nếu có pack LevelPlay; không thì AdMob (App Open).
            return 3;
        }

        private static string GetTabLabel(SetupTab tab)
        {
            switch (tab)
            {
                case SetupTab.Facebook: return "Facebook";
                case SetupTab.AppsFlyer: return "AppsFlyer";
                case SetupTab.GameAnalytics: return "Game Analytics";
#if LEVELPLAY_DEPENDENCIES_INSTALLED
                case SetupTab.IronSourceMediation: return "IronSource Mediation";
#endif
                case SetupTab.AdMobAppOpen: return "AdMob (App Open)";
                case SetupTab.FirebaseRemoteConfig: return "Firebase RC";
                default: return tab.ToString();
            }
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
                Debug.Log("[GameUpSDK] Đã thêm SDK vào scene hiện tại.");
            }
        }

        private static Type _facebookSettingsType;

        private static Type GetFacebookSettingsType()
        {
            if (_facebookSettingsType != null)
                return _facebookSettingsType;

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
                    // ignore
                }
            }

            return _facebookSettingsType;
        }

        private static string GetFacebookSettingsAssetPath()
        {
            var t = GetFacebookSettingsType();
            if (t == null)
                return PathFacebookSettings;

            try
            {
                var pathField = t.GetField("FacebookSettingsPath", BindingFlags.Public | BindingFlags.Static);
                var nameField = t.GetField("FacebookSettingsAssetName", BindingFlags.Public | BindingFlags.Static);
                var extField = t.GetField("FacebookSettingsAssetExtension", BindingFlags.Public | BindingFlags.Static);
                string rel = (pathField?.GetValue(null) as string ?? "FacebookSDK/SDK/Resources").Replace('\\', '/').Trim('/');
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
                    Type t;
                    try
                    {
                        t = asm.GetType("Facebook.Unity.Editor.ManifestMod", false);
                    }
                    catch
                    {
                        continue;
                    }

                    if (t == null)
                        continue;
                    var m = t.GetMethod("GenerateManifest", BindingFlags.Static | BindingFlags.Public);
                    m?.Invoke(null, null);
                    return;
                }
            }
            catch
            {
                // ignore
            }
        }

        private void LoadFacebookSettings()
        {
            var settingsType = GetFacebookSettingsType();
            if (settingsType == null)
                return;

            string path = GetFacebookSettingsAssetPath();
            var asset = AssetDatabase.LoadAssetAtPath(path, settingsType) as ScriptableObject;
            if (asset == null)
            {
                _facebookAppLabel = "";
                _facebookAppId = "";
                _facebookClientToken = "";
                _facebookAndroidKeystorePath = "";
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

        private static void EnsureFacebookListSize(SerializedObject so, string listName, int minSize)
        {
            var p = so.FindProperty(listName);
            if (p == null || p.isArray == false)
                return;
            if (p.arraySize >= minSize)
                return;
            p.arraySize = minSize;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private bool TryCreateFacebookSettingsAsset()
        {
            var settingsType = GetFacebookSettingsType();
            if (settingsType == null)
                return false;

            string path = GetFacebookSettingsAssetPath();
            if (AssetDatabase.LoadAssetAtPath(path, settingsType) != null)
                return true;

            var pathField = settingsType.GetField("FacebookSettingsPath", BindingFlags.Public | BindingFlags.Static);
            string rel = (pathField?.GetValue(null) as string ?? "FacebookSDK/SDK/Resources")
                .Replace('\\', Path.DirectorySeparatorChar);
            string diskPath = Path.Combine(Application.dataPath, rel);
            if (!Directory.Exists(diskPath))
                Directory.CreateDirectory(diskPath);

            var instance = ScriptableObject.CreateInstance(settingsType);
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TryFacebookManifestRegenerate();
            LoadFacebookSettings();
            return true;
        }

        private bool SaveFacebookSettingsAsset()
        {
            var settingsType = GetFacebookSettingsType();
            if (settingsType == null)
                return true;

            string path = GetFacebookSettingsAssetPath();
            var asset = AssetDatabase.LoadAssetAtPath(path, settingsType) as ScriptableObject;
            if (asset == null)
                return true;

            var so = new SerializedObject(asset);
            EnsureFacebookListSize(so, "appLabels", 1);
            EnsureFacebookListSize(so, "appIds", 1);
            EnsureFacebookListSize(so, "clientTokens", 1);
            so.Update();

            var appLabels = so.FindProperty("appLabels");
            var appIds = so.FindProperty("appIds");
            var clientTokens = so.FindProperty("clientTokens");
            var keystore = so.FindProperty("androidKeystorePath");

            if (appLabels != null && appLabels.arraySize > 0)
                appLabels.GetArrayElementAtIndex(0).stringValue = _facebookAppLabel ?? "";
            if (appIds != null && appIds.arraySize > 0)
                appIds.GetArrayElementAtIndex(0).stringValue = _facebookAppId ?? "";
            if (clientTokens != null && clientTokens.arraySize > 0)
                clientTokens.GetArrayElementAtIndex(0).stringValue = _facebookClientToken ?? "";
            if (keystore != null)
                keystore.stringValue = _facebookAndroidKeystorePath ?? "";

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            TryFacebookManifestRegenerate();
            return true;
        }

        private void DrawFacebookSection()
        {
            EditorGUILayout.LabelField("Facebook Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Cấu hình asset: " + PathFacebookSettings + " (giống menu Facebook → Edit Settings).",
                MessageType.None);

            var settingsType = GetFacebookSettingsType();
            if (settingsType == null)
            {
                EditorGUILayout.HelpBox(
                    "Chưa có Facebook Unity SDK trong project. Cài Facebook SDK 18.x qua cửa sổ Setup Dependencies.",
                    MessageType.Warning);
                if (GUILayout.Button("Mở GameUp SDK — Setup Dependencies", GUILayout.Height(28)))
                    GameUp.SDK.Installer.GameUpDependenciesWindow.ShowWindow();
                return;
            }

            string path = GetFacebookSettingsAssetPath();
            var asset = AssetDatabase.LoadAssetAtPath(path, settingsType) as ScriptableObject;

            if (asset == null)
            {
                EditorGUILayout.HelpBox(
                    "Chưa có FacebookSettings.asset. Tạo file giống khi chọn Facebook → Edit Settings trên menu.",
                    MessageType.Warning);
                if (GUILayout.Button("Tạo FacebookSettings.asset", GUILayout.Height(28)))
                {
                    if (TryCreateFacebookSettingsAsset())
                        Debug.Log("[GameUpSDK] Đã tạo " + path);
                }

                if (GUILayout.Button("Mở GameUp SDK — Setup Dependencies", GUILayout.Height(24)))
                    GameUp.SDK.Installer.GameUpDependenciesWindow.ShowWindow();
                return;
            }

            if (GUILayout.Button("Chọn FacebookSettings trong Project", GUILayout.Height(22)))
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("App #1 (game chính)", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            _facebookAppLabel = EditorGUILayout.TextField(
                new GUIContent("App Name", "Nên trùng tên game; có thể đồng bộ từ Player Settings > Product Name."),
                _facebookAppLabel);
            if (GUILayout.Button("= Product Name", GUILayout.Width(110)))
                _facebookAppLabel = PlayerSettings.productName ?? "";
            EditorGUILayout.EndHorizontal();

            _facebookAppId = EditorGUILayout.TextField(
                new GUIContent("Facebook App Id", "developers.facebook.com → App → Settings."),
                _facebookAppId);
            _facebookClientToken = EditorGUILayout.TextField(
                new GUIContent("Client Token", "App → Settings → Advanced."),
                _facebookClientToken);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Android", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            _facebookAndroidKeystorePath = EditorGUILayout.TextField(
                new GUIContent("Android Keystore Path", "Để trống nếu dùng keystore mặc định của Unity."),
                _facebookAndroidKeystorePath);
            if (GUILayout.Button("Browse…", GUILayout.Width(72)))
            {
                string picked = EditorUtility.OpenFilePanel("Chọn keystore", "", "keystore,jks,ks");
                if (!string.IsNullOrEmpty(picked))
                    _facebookAndroidKeystorePath = picked;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Bấm \"Save Configuration\" ở cuối cửa sổ để ghi các trường trên vào FacebookSettings.asset.",
                MessageType.Info);
        }

        private void DrawAppsFlyerSection()
        {
            EditorGUILayout.LabelField("AppsFlyer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Một lần nhập: Dev Key (AppsFlyer dashboard) và App ID App Store (chỉ iOS; Android để trống). " +
                "Ghi lên AppsFlyerObject trong SDK / " + PathAppsFlyer + ".",
                MessageType.None);
            _appsFlyerDevKey = EditorGUILayout.TextField("Dev Key", _appsFlyerDevKey);
            _appsFlyerAppId = EditorGUILayout.TextField("App ID (iOS)", _appsFlyerAppId);
            _appsFlyerIsDebug = EditorGUILayout.Toggle("Debug (AppsFlyer SDK)", _appsFlyerIsDebug);
        }

        private static Type _gameAnalyticsSettingsType;

        private static Type GetGameAnalyticsSettingsType()
        {
            if (_gameAnalyticsSettingsType != null)
                return _gameAnalyticsSettingsType;

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
                    // ignore bad assemblies
                }
            }

            return _gameAnalyticsSettingsType;
        }

        private static RuntimePlatform? ParseGameAnalyticsPlatformDisplayName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return null;
            if (string.Equals(displayName, "WSA", StringComparison.Ordinal))
                return RuntimePlatform.WSAPlayerARM;
            return Enum.TryParse(displayName, out RuntimePlatform p) ? p : (RuntimePlatform?)null;
        }

        /// <summary>Player Settings &gt; Version (bundleVersion), dùng làm gợi ý build cho GameAnalytics.</summary>
        private static string GetPlayerSettingsVersionHint()
        {
            return PlayerSettings.bundleVersion ?? "";
        }

        private void DrawGameAnalyticsSection()
        {
            EditorGUILayout.LabelField("Game Analytics", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Cấu hình Game Key / Secret Key / Build trên asset:\n" + PathGameAnalyticsSettings,
                MessageType.None);

            var settingsType = GetGameAnalyticsSettingsType();
            if (settingsType == null)
            {
                EditorGUILayout.HelpBox(
                    "Không tìm thấy lớp GameAnalyticsSDK.Setup.Settings. Hãy cài package GameAnalytics vào project.",
                    MessageType.Error);
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath(PathGameAnalyticsSettings, settingsType) as ScriptableObject;
            if (asset == null)
            {
                EditorGUILayout.HelpBox(
                    "Chưa có Settings.asset tại đường dẫn trên. Tạo từ menu GameAnalytics hoặc thêm file vào Resources/GameAnalytics.",
                    MessageType.Warning);
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
                EditorGUILayout.PropertyField(
                    usePlayerBuild,
                    new GUIContent(
                        "Auto build từ Player Settings (Android/iOS)",
                        "Khi bật, runtime gửi Application.version cho Android/iOS (giống inspector GameAnalytics)."));
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Game Setup", EditorStyles.boldLabel);

            var getAvailable = settingsType.GetMethod(
                "GetAvailablePlatforms",
                BindingFlags.Instance | BindingFlags.Public);
            var available = getAvailable?.Invoke(asset, null) as string[];

            if (available == null || available.Length == 0)
            {
                EditorGUILayout.HelpBox("Không còn platform nào để thêm (hoặc danh sách trống).", MessageType.Info);
            }
            else
            {
                _gaAddPlatformDropdownIndex = Mathf.Clamp(_gaAddPlatformDropdownIndex, 0, available.Length - 1);
                _gaAddPlatformDropdownIndex = EditorGUILayout.Popup("Platform to add", _gaAddPlatformDropdownIndex, available);
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

            if (platforms == null || gameKeys == null || secretKeys == null || builds == null)
            {
                EditorGUILayout.HelpBox(
                    "SerializedObject không đọc được Platforms/gameKey/secretKey/Build.",
                    MessageType.Error);
                return;
            }

            if (platforms.arraySize != gameKeys.arraySize || platforms.arraySize != secretKeys.arraySize ||
                platforms.arraySize != builds.arraySize)
            {
                EditorGUILayout.HelpBox("Dữ liệu platform trong Settings không đồng bộ — mở inspector GameAnalytics để sửa.", MessageType.Warning);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Platforms", EditorStyles.boldLabel);

            int removeAt = -1;
            for (int i = 0; i < platforms.arraySize; i++)
            {
                var plat = (RuntimePlatform)platforms.GetArrayElementAtIndex(i).intValue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Platform", plat.ToString());

                var gk = gameKeys.GetArrayElementAtIndex(i);
                var sk = secretKeys.GetArrayElementAtIndex(i);
                var bd = builds.GetArrayElementAtIndex(i);

                EditorGUILayout.PropertyField(gk, new GUIContent("Game Key"));
                EditorGUILayout.PropertyField(sk, new GUIContent("Secret Key"));
                EditorGUILayout.PropertyField(bd, new GUIContent("Build version"));

                string hint = GetPlayerSettingsVersionHint();
                bool autoBuild = usePlayerBuild != null && usePlayerBuild.boolValue &&
                                 (plat == RuntimePlatform.Android || plat == RuntimePlatform.IPhonePlayer);
                if (autoBuild)
                {
                    EditorGUILayout.HelpBox(
                        "Đang bật auto: trên build Android/iOS SDK sẽ gửi Application.version (hiện \"" + hint + "\" trong Player Settings). " +
                        "Giá trị Build version ở trên chủ yếu cho Editor / khi tắt auto.",
                        MessageType.Info);
                }

                if (!string.IsNullOrEmpty(hint) &&
                    !string.Equals((bd.stringValue ?? "").Trim(), hint.Trim(), StringComparison.Ordinal))
                {
                    EditorGUILayout.HelpBox(
                        "Build version khác Player Settings > Version (\"" + hint + "\"). Kiểm tra trước khi release.",
                        MessageType.Warning);
                }

                if (GUILayout.Button("Remove platform"))
                    removeAt = i;

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            if (removeAt >= 0)
            {
                var remove = settingsType.GetMethod("RemovePlatformAtIndex", BindingFlags.Instance | BindingFlags.Public);
                remove?.Invoke(asset, new object[] { removeAt });
                EditorUtility.SetDirty(asset);
                so.Update();
            }

            if (so.ApplyModifiedProperties())
                EditorUtility.SetDirty(asset);
        }

        private static void SaveGameAnalyticsSettingsAsset()
        {
            var t = GetGameAnalyticsSettingsType();
            if (t == null)
                return;
            if (AssetDatabase.LoadAssetAtPath(PathGameAnalyticsSettings, t) == null)
                return;
            AssetDatabase.SaveAssets();
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private void DrawIronSourceSection()
        {
            EditorGUILayout.LabelField("IronSource (LevelPlay) Mediation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Quảng cáo chạy qua IronSource mediation. AdMob và Unity Ads đã được gộp qua LevelPlay.\n" +
                "Chỉ cần nhập App Key (lấy từ LevelPlay dashboard) để lấy quảng cáo.\n" +
                "Target: IronSourceAds trên " + PathIronSource, MessageType.Info);
            _ironSourceAppKey = EditorGUILayout.TextField("App Key (bắt buộc)", _ironSourceAppKey);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Ad Unit / Placement IDs (tùy chọn; để trống = dùng DefaultBanner, DefaultInterstitial, DefaultRewardedVideo)", EditorStyles.miniBoldLabel);
            _ironSourceBannerId = EditorGUILayout.TextField("Banner ID", _ironSourceBannerId);
            _ironSourceInterstitialId = EditorGUILayout.TextField("Interstitial ID", _ironSourceInterstitialId);
            _ironSourceRewardedId = EditorGUILayout.TextField("Rewarded ID", _ironSourceRewardedId);

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("LevelPlay Mediation Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("App Key điền vào " + PathLevelPlayMediationSettings, MessageType.None);
            _levelPlayAndroidAppKey = EditorGUILayout.TextField("Android App Key", _levelPlayAndroidAppKey);
            _levelPlayIOSAppKey = EditorGUILayout.TextField("iOS App Key", _levelPlayIOSAppKey);
        }
#endif

        private void DrawAdMobSection()
        {
            EditorGUILayout.LabelField("AdMob", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "SDK mặc định chỉ dùng IronSource Mediation. Thêm AdmobAds vào adsBehaviours trong prefab SDK nếu cần App Open.\n" +
                "Target: AdmobAds trên " + PathAdMob, MessageType.None);
            EditorGUILayout.LabelField("Ad Unit IDs (chỉ cần nếu dùng App Open)", EditorStyles.miniBoldLabel);
            _admobBannerId = EditorGUILayout.TextField("Banner ID", _admobBannerId);
            _admobInterstitialId = EditorGUILayout.TextField("Interstitial ID", _admobInterstitialId);
            _admobRewardedId = EditorGUILayout.TextField("Rewarded ID", _admobRewardedId);
            _admobAppOpenId = EditorGUILayout.TextField("App Open ID", _admobAppOpenId);

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Google Mobile Ads App ID", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("App ID điền vào " + PathGoogleMobileAdsSettings, MessageType.None);
            _googleMobileAdsAndroidAppId = EditorGUILayout.TextField("Android App ID", _googleMobileAdsAndroidAppId);
            _googleMobileAdsIOSAppId = EditorGUILayout.TextField("iOS App ID", _googleMobileAdsIOSAppId);
        }

        private void DrawFirebaseRemoteConfigSection()
        {
            EditorGUILayout.LabelField("Firebase Remote Config (defaults)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("FirebaseRemoteConfigUtils on " + PathSDK + ". Giá trị mặc định khi chưa fetch hoặc key không có trên Remote.", MessageType.None);
            _rcInterCappingTime = EditorGUILayout.IntField("inter_capping_time (giây)", _rcInterCappingTime);
            _rcInterStartLevel = EditorGUILayout.IntField("inter_start_level", _rcInterStartLevel);
            _rcEnableRateApp = EditorGUILayout.Toggle("enable_rate_app", _rcEnableRateApp);
            _rcLevelStartShowRateApp = EditorGUILayout.IntField("level_start_show_rate_app", _rcLevelStartShowRateApp);
            _rcNoInternetPopupEnable = EditorGUILayout.Toggle("no_internet_popup_enable", _rcNoInternetPopupEnable);
            _rcEnableBanner = EditorGUILayout.Toggle("enable_banner", _rcEnableBanner);
        }

        private void LoadFromPrefabs()
        {
            var errors = new System.Collections.Generic.List<string>();
            if (!LoadAppsFlyer()) errors.Add("Prefab not found at: " + PathAppsFlyer);
            LoadFirebaseRemoteConfigUtils();
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            if (IsIronSourceSetupSectionAvailable() && !LoadIronSource())
                errors.Add("Prefab not found at: " + PathIronSource);
#endif
            if (!LoadAdMob()) errors.Add("Prefab not found at: " + PathAdMob);
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
            if (TryGetSdkSceneRoot(out var sdkRoot))
            {
                LoadFromSceneSdk(sdkRoot);
                return;
            }

            // Không có SDK trong Scene → fallback load từ prefab/assets (read-only vẫn load được).
            if (!RequiresPrefabCloneBeforeSetup())
                LoadFromPrefabs();
            else
                LoadFromPrefabs();
        }

        private void LoadFromSceneSdk(GameObject sdkRoot)
        {
            if (sdkRoot == null) return;

            // AppsFlyerObjectScript nằm ở prefab riêng, thường không có trong SDK root của scene.
            // Chỉ load các component nằm trên SDK root/prefab instance.
            var errors = new System.Collections.Generic.List<string>();

            var afType = Type.GetType("AppsFlyerObjectScript, AppsFlyer");
            if (afType != null)
            {
                var afComp = sdkRoot.GetComponentInChildren(afType, true);
                if (afComp != null)
                {
                    var so = new SerializedObject(afComp);
                    Assign(so, "devKey", ref _appsFlyerDevKey);
                    Assign(so, "appID", ref _appsFlyerAppId);
                    AssignBool(so, "isDebug", ref _appsFlyerIsDebug);
                }
                else if (!LoadAppsFlyer())
                    errors.Add("Prefab not found at: " + PathAppsFlyer);
            }
            else if (!LoadAppsFlyer())
                errors.Add("Prefab not found at: " + PathAppsFlyer);

            var rc = sdkRoot.GetComponent<FirebaseRemoteConfigUtils>();
            if (rc != null)
            {
                var so = new SerializedObject(rc);
                AssignInt(so, "inter_capping_time", ref _rcInterCappingTime);
                AssignInt(so, "inter_start_level", ref _rcInterStartLevel);
                AssignBool(so, "enable_rate_app", ref _rcEnableRateApp);
                AssignInt(so, "level_start_show_rate_app", ref _rcLevelStartShowRateApp);
                AssignBool(so, "no_internet_popup_enable", ref _rcNoInternetPopupEnable);
                AssignBool(so, "enable_banner", ref _rcEnableBanner);
            }

            var admob = sdkRoot.GetComponentInChildren<GameUp.SDK.AdmobAds>(true);
            if (admob != null)
            {
                var so = new SerializedObject(admob);
                Assign(so, "bannerAdUnitId", ref _admobBannerId);
                Assign(so, "interstitialAdUnitId", ref _admobInterstitialId);
                Assign(so, "rewardedAdUnitId", ref _admobRewardedId);
                Assign(so, "appOpenAdUnitId", ref _admobAppOpenId);
            }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
            var iron = sdkRoot.GetComponentInChildren<GameUp.SDK.IronSourceAds>(true);
            if (iron != null)
            {
                var so = new SerializedObject(iron);
                Assign(so, "levelPlayAppKey", ref _ironSourceAppKey);
                Assign(so, "bannerAdUnitId", ref _ironSourceBannerId);
                Assign(so, "interstitialAdUnitId", ref _ironSourceInterstitialId);
                Assign(so, "rewardedVideoAdUnitId", ref _ironSourceRewardedId);
            }
#endif

            LoadGoogleMobileAdsSettings();
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            LoadLevelPlayMediationSettings();
#endif
            LoadFacebookSettings();

            _loadErrors = errors.Count > 0 ? string.Join("\n", errors) : null;
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
            var devKey = so.FindProperty("devKey");
            var appID = so.FindProperty("appID");
            if (devKey != null) _appsFlyerDevKey = devKey.stringValue ?? "";
            if (appID != null) _appsFlyerAppId = appID.stringValue ?? "";
            var isDbg = so.FindProperty("isDebug");
            if (isDbg != null) _appsFlyerIsDebug = isDbg.boolValue;
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
            AssignBool(so, "enable_rate_app", ref _rcEnableRateApp);
            AssignInt(so, "level_start_show_rate_app", ref _rcLevelStartShowRateApp);
            AssignBool(so, "no_internet_popup_enable", ref _rcNoInternetPopupEnable);
            AssignBool(so, "enable_banner", ref _rcEnableBanner);
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
            Assign(so, "bannerAdUnitId", ref _ironSourceBannerId);
            Assign(so, "interstitialAdUnitId", ref _ironSourceInterstitialId);
            Assign(so, "rewardedVideoAdUnitId", ref _ironSourceRewardedId);
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
            Assign(so, "bannerAdUnitId", ref _admobBannerId);
            Assign(so, "interstitialAdUnitId", ref _admobInterstitialId);
            Assign(so, "rewardedAdUnitId", ref _admobRewardedId);
            Assign(so, "appOpenAdUnitId", ref _admobAppOpenId);
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
            AssetDatabase.SaveAssets();
            return true;
        }

        private void LoadLevelPlayMediationSettings()
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PathLevelPlayMediationSettings);
            if (asset == null) return;
            var so = new SerializedObject(asset);
            Assign(so, "AndroidAppKey", ref _levelPlayAndroidAppKey);
            Assign(so, "IOSAppKey", ref _levelPlayIOSAppKey);
#endif
        }

        private bool SaveLevelPlayMediationSettings()
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PathLevelPlayMediationSettings);
            if (asset == null) return false;
            var so = new SerializedObject(asset);
            Set(so, "AndroidAppKey", _levelPlayAndroidAppKey);
            Set(so, "IOSAppKey", _levelPlayIOSAppKey);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return true;
#else
            return true;
#endif
        }

        private static void Assign(SerializedObject so, string propName, ref string target)
        {
            var p = so.FindProperty(propName);
            if (p != null) target = p.stringValue ?? "";
        }

        private static void AssignInt(SerializedObject so, string propName, ref int target)
        {
            var p = so.FindProperty(propName);
            if (p != null) target = p.intValue;
        }

        private static void AssignBool(SerializedObject so, string propName, ref bool target)
        {
            var p = so.FindProperty(propName);
            if (p != null) target = p.boolValue;
        }

        private static string AssetPathToAbsolute(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return null;
            var relative = assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Application.dataPath, relative);
        }

        private static void RewirePrefabYamlGuidReferences(string assetPath, Dictionary<string, string> guidMap)
        {
            if (guidMap == null || guidMap.Count == 0)
                return;

            var abs = AssetPathToAbsolute(assetPath);
            if (string.IsNullOrEmpty(abs) || !File.Exists(abs))
                return;

            var text = File.ReadAllText(abs);
            var changed = false;
            foreach (var kv in guidMap)
            {
                if (string.IsNullOrEmpty(kv.Key) || string.IsNullOrEmpty(kv.Value) || kv.Key == kv.Value)
                    continue;
                var needle = "guid: " + kv.Key;
                if (text.IndexOf(needle, StringComparison.Ordinal) < 0)
                    continue;
                text = text.Replace(needle, "guid: " + kv.Value);
                changed = true;
            }

            if (!changed)
                return;

            File.WriteAllText(abs, text);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static string ResolveGameAnalyticsPrefabPath()
        {
#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
            string p = GameAnalytics.WhereIs("GameAnalytics.prefab", "Prefab");
            if (!string.IsNullOrEmpty(p))
                return p.Replace('\\', '/');
            return PathGameAnalyticsPrefabDefault;
#else
            return null;
#endif
        }

        /// <summary>True nếu đã có object (không phải root SDK) chứa <see cref="GameAnalytics"/>.</summary>
        private static bool SdkRootHasGameAnalyticsDescendant(GameObject sdkRoot)
        {
#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
            if (sdkRoot == null) return false;
            foreach (var ga in sdkRoot.GetComponentsInChildren<GameAnalytics>(true))
            {
                if (ga != null && ga.gameObject != sdkRoot)
                    return true;
            }
            return false;
#else
            return false; // Only compiled if the dependencies are missing
#endif
        }

        /// <summary>Gắn prefab GameAnalytics làm con của root SDK (scene hoặc prefab đang mở trong memory).</summary>
        private static void EnsureGameAnalyticsChildUnderSdkObject(GameObject sdkRoot)
        {
            if (sdkRoot == null || SdkRootHasGameAnalyticsDescendant(sdkRoot))
                return;

            string gaPath = ResolveGameAnalyticsPrefabPath();
            var gaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(gaPath);
            if (gaPrefab == null)
            {
                Debug.LogWarning("[GameUpSDK] Không tìm thấy GameAnalytics.prefab (đã thử: " + gaPath + "). Bỏ qua gắn con GA.");
                return;
            }

            var child = (GameObject)PrefabUtility.InstantiatePrefab(gaPrefab, sdkRoot.transform);
            if (child != null)
                child.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        /// <summary>Mở asset SDK.prefab, thêm nested GameAnalytics nếu thiếu, lưu lại.</summary>
        private static void EnsureGameAnalyticsNestedInSdkPrefabAsset(string sdkPrefabPath)
        {
            if (string.IsNullOrEmpty(sdkPrefabPath) ||
                AssetDatabase.LoadAssetAtPath<GameObject>(sdkPrefabPath) == null)
                return;

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(sdkPrefabPath);
                if (SdkRootHasGameAnalyticsDescendant(root))
                    return;

                string gaPath = ResolveGameAnalyticsPrefabPath();
                var gaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(gaPath);
                if (gaPrefab == null)
                {
                    Debug.LogWarning("[GameUpSDK] Không tìm thấy GameAnalytics.prefab (đã thử: " + gaPath + ").");
                    return;
                }

                var child = (GameObject)PrefabUtility.InstantiatePrefab(gaPrefab, root.transform);
                if (child != null)
                    child.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

                PrefabUtility.SaveAsPrefabAsset(root, sdkPrefabPath);
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Copy mọi prefab trong thư mục Prefab của package sang Assets/SDK/Prefabs và cập nhật guid tham chiếu.</summary>
        private static bool TryClonePackagePrefabsToWritable(out string errorMessage)
        {
            errorMessage = null;
            var srcDir = GetPackagePrefabDirectory().Replace('\\', '/').TrimEnd('/');

            if (!AssetDatabase.IsValidFolder(srcDir))
            {
                errorMessage = "Không tìm thấy thư mục prefab: " + srcDir;
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(WritablePrefabsRoot + "/SDK.prefab") != null)
            {
                EnsureGameAnalyticsNestedInSdkPrefabAsset(WritablePrefabsRoot + "/SDK.prefab");
                return true;
            }

            if (!AssetDatabase.IsValidFolder("Assets/SDK"))
                AssetDatabase.CreateFolder("Assets", "SDK");
            if (!AssetDatabase.IsValidFolder(WritablePrefabsRoot))
                AssetDatabase.CreateFolder("Assets/SDK", "Prefabs");

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { srcDir });
            var guidMap = new Dictionary<string, string>();
            var copiedDestPaths = new List<string>();
            var srcPaths = new List<string>();

            foreach (var g in prefabGuids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g).Replace('\\', '/');
                var prefix = srcDir.EndsWith("/", StringComparison.Ordinal) ? srcDir : srcDir + "/";
                if (!p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !string.Equals(p, srcDir, StringComparison.OrdinalIgnoreCase))
                    continue;
                srcPaths.Add(p);
            }

            srcPaths.Sort(StringComparer.Ordinal);

            foreach (var src in srcPaths)
            {
                var fileName = Path.GetFileName(src);
                if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                var dst = WritablePrefabsRoot + "/" + fileName;
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(src) == null)
                    continue;

                var oldGuid = AssetDatabase.AssetPathToGUID(src);
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dst) != null)
                {
                    var existingGuid = AssetDatabase.AssetPathToGUID(dst);
                    if (!string.IsNullOrEmpty(oldGuid) && !string.IsNullOrEmpty(existingGuid))
                        guidMap[oldGuid] = existingGuid;
                    copiedDestPaths.Add(dst);
                    continue;
                }

                if (!AssetDatabase.CopyAsset(src, dst))
                {
                    Debug.LogWarning("[GameUpSDK] Không copy được: " + src + " → " + dst);
                    continue;
                }

                var newGuid = AssetDatabase.AssetPathToGUID(dst);
                if (!string.IsNullOrEmpty(oldGuid) && !string.IsNullOrEmpty(newGuid))
                    guidMap[oldGuid] = newGuid;
                copiedDestPaths.Add(dst);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (var dstPath in copiedDestPaths)
            {
                if (dstPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    RewirePrefabYamlGuidReferences(dstPath, guidMap);
            }

            AssetDatabase.Refresh();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(WritablePrefabsRoot + "/SDK.prefab") == null)
            {
                errorMessage = "Clone không tạo được SDK.prefab trong " + WritablePrefabsRoot + ". Xem Console.";
                return false;
            }

            EnsureGameAnalyticsNestedInSdkPrefabAsset(WritablePrefabsRoot + "/SDK.prefab");
            return true;
        }

        private void SaveConfiguration()
        {
            var errors = new System.Collections.Generic.List<string>();

            if (TryGetSdkSceneRoot(out var sdkRoot))
            {
                if (!SaveSceneAppsFlyerObject(sdkRoot)) errors.Add("SDK in Scene (AppsFlyerObjectScript)");
                if (!SaveSceneFirebaseRemoteConfigUtils(sdkRoot)) errors.Add("SDK in Scene (FirebaseRemoteConfigUtils)");
                if (!SaveSceneAdsManager(sdkRoot)) errors.Add("SDK in Scene (AdsManager)");
#if LEVELPLAY_DEPENDENCIES_INSTALLED
                if (IsIronSourceSetupSectionAvailable() && !SaveSceneIronSource(sdkRoot))
                    errors.Add("SDK in Scene (IronSourceAds)");
#endif
                if (!SaveSceneAdMob(sdkRoot)) errors.Add("SDK in Scene (AdmobAds)");

                EditorSceneManager.MarkSceneDirty(sdkRoot.scene);
                EditorSceneManager.SaveOpenScenes();
            }
            else if (!IsPrefabAssetPathWritable(PathSDK))
            {
                errors.Add(
                    "Không tìm thấy SDK trong Scene và prefab SDK không ghi được (thường do nằm trong Packages/). " +
                    "Hãy clone prefab sang Assets/SDK/Prefabs hoặc dùng \"Tạo SDK trong Scene hiện tại\".");
            }

            // Ghi lên file .prefab dưới Assets/ (đồng bộ với scene hoặc khi chỉnh không có instance trong scene).
            SaveConfigurationToWritablePrefabAssets(errors);

            // Các settings asset vẫn lưu như cũ
            if (!SaveGoogleMobileAdsSettings()) errors.Add(PathGoogleMobileAdsSettings);
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            if (!SaveLevelPlayMediationSettings()) errors.Add(PathLevelPlayMediationSettings);
#endif

            SaveGameAnalyticsSettingsAsset();
            SaveFacebookSettingsAsset();

            if (errors.Count > 0)
                _saveErrors = "Asset/Prefab not found at:\n" + string.Join("\n", errors);
            else
                Debug.Log("[GameUpSDK] Configuration Saved!");
        }

        private static bool TryGetSdkSceneRoot(out GameObject sdkRoot)
        {
            sdkRoot = null;
            try
            {
                var all = Resources.FindObjectsOfTypeAll<GameUp.SDK.AdsManager>();
                foreach (var am in all)
                {
                    if (am == null) continue;
                    if (EditorUtility.IsPersistent(am)) continue; // asset/prefab
                    var go = am.gameObject;
                    if (go == null) continue;
                    // Chỉ lấy object thuộc scene hợp lệ
                    if (!go.scene.IsValid()) continue;
                    sdkRoot = go;
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        private bool SaveSceneAppsFlyerObject(GameObject sdkRoot)
        {
            if (sdkRoot == null) return false;
            var type = Type.GetType("AppsFlyerObjectScript, AppsFlyer");
            if (type == null) return true;
            var comp = sdkRoot.GetComponentInChildren(type, true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            Set(so, "devKey", _appsFlyerDevKey);
            Set(so, "appID", _appsFlyerAppId);
            SetBool(so, "isDebug", _appsFlyerIsDebug);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(comp);
            PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            return true;
        }

        private bool SaveSceneAdsManager(GameObject sdkRoot)
        {
            return PersistAdsManagerLists(sdkRoot, recordPrefabInstance: true);
        }

        /// <summary>
        /// Đồng bộ hai list serialized với hierarchy — trùng logic <see cref="GameUp.SDK.AdsManager.CollectAdsFromChildren"/>.
        /// Tên field thực tế là levelPlayAdsBehaviours / admobAdsBehaviours (không phải adsBehaviours).
        /// </summary>
        private static bool PersistAdsManagerLists(GameObject sdkRoot, bool recordPrefabInstance)
        {
            if (sdkRoot == null) return false;
            var comp = sdkRoot.GetComponent<GameUp.SDK.AdsManager>();
            if (comp == null) return false;

            var levelPlay = new List<GameUp.SDK.IronSourceAds>();
            foreach (var c in sdkRoot.GetComponentsInChildren<GameUp.SDK.IronSourceAds>(true))
            {
                if (c.gameObject == sdkRoot) continue;
                levelPlay.Add(c);
            }

            var admob = new List<AdmobAds>();
            foreach (var c in sdkRoot.GetComponentsInChildren<AdmobAds>(true))
            {
                if (c.gameObject == sdkRoot) continue;
                admob.Add(c);
            }

            var so = new SerializedObject(comp);
            var lp = so.FindProperty("levelPlayAdsBehaviours");
            var ad = so.FindProperty("admobAdsBehaviours");
            if (lp == null || ad == null) return false;

            lp.arraySize = levelPlay.Count;
            for (int i = 0; i < levelPlay.Count; i++)
                lp.GetArrayElementAtIndex(i).objectReferenceValue = levelPlay[i];

            ad.arraySize = admob.Count;
            for (int i = 0; i < admob.Count; i++)
                ad.GetArrayElementAtIndex(i).objectReferenceValue = admob[i];

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(comp);
            if (recordPrefabInstance)
                PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            return true;
        }

        /// <summary>True nếu có thể ghi file .prefab (không phải chỉ đọc trong Packages/).</summary>
        private static bool IsPrefabAssetPathWritable(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            var p = assetPath.Replace('\\', '/');
            if (p.StartsWith("Packages/", StringComparison.Ordinal)) return false;
            return p.StartsWith("Assets/", StringComparison.Ordinal);
        }

        /// <summary>Ghi các field setup lên prefab SDK (và prefab ads phụ) khi asset nằm dưới Assets/.</summary>
        private void SaveConfigurationToWritablePrefabAssets(List<string> errors)
        {
            if (!IsPrefabAssetPathWritable(PathSDK))
                return;

            var sdkPath = PathSDK;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(sdkPath) == null)
            {
                errors.Add("SDK.prefab không tồn tại tại: " + sdkPath);
                return;
            }

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(sdkPath);
                if (root == null)
                {
                    errors.Add("Không LoadPrefabContents được: " + sdkPath);
                    return;
                }

                if (!SavePrefabAppsFlyerObject(root)) errors.Add("SDK.prefab (AppsFlyerObjectScript)");
                if (!SavePrefabFirebaseRemoteConfigUtils(root)) errors.Add("SDK.prefab (FirebaseRemoteConfigUtils)");
                if (!PersistAdsManagerLists(root, recordPrefabInstance: false)) errors.Add("SDK.prefab (AdsManager)");
#if LEVELPLAY_DEPENDENCIES_INSTALLED
                if (IsIronSourceSetupSectionAvailable() && !SavePrefabIronSource(root))
                    errors.Add("SDK.prefab (IronSourceAds child)");
#endif
                if (!SavePrefabAdMob(root)) errors.Add("SDK.prefab (AdmobAds child)");

                PrefabUtility.SaveAsPrefabAsset(root, sdkPath);
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }

            TrySaveAppsFlyerObjectPrefab(errors);
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            TrySaveIronSourcePrefabAsset(errors);
#endif
            TrySaveAdMobPrefabAsset(errors);

            AssetDatabase.SaveAssets();
        }

        private bool SavePrefabAppsFlyerObject(GameObject sdkRoot)
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
            EditorUtility.SetDirty(comp);
            return true;
        }

        private bool SavePrefabFirebaseRemoteConfigUtils(GameObject sdkRoot)
        {
            var comp = sdkRoot.GetComponent<GameUp.SDK.FirebaseRemoteConfigUtils>();
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            SetInt(so, "inter_capping_time", _rcInterCappingTime);
            SetInt(so, "inter_start_level", _rcInterStartLevel);
            SetBool(so, "enable_rate_app", _rcEnableRateApp);
            SetInt(so, "level_start_show_rate_app", _rcLevelStartShowRateApp);
            SetBool(so, "no_internet_popup_enable", _rcNoInternetPopupEnable);
            SetBool(so, "enable_banner", _rcEnableBanner);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(comp);
            return true;
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private bool SavePrefabIronSource(GameObject sdkRoot)
        {
            var comp = sdkRoot.GetComponentInChildren<GameUp.SDK.IronSourceAds>(true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            Set(so, "levelPlayAppKey", _ironSourceAppKey);
            Set(so, "bannerAdUnitId", _ironSourceBannerId);
            Set(so, "interstitialAdUnitId", _ironSourceInterstitialId);
            Set(so, "rewardedVideoAdUnitId", _ironSourceRewardedId);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(comp);
            return true;
        }
#endif

        private bool SavePrefabAdMob(GameObject sdkRoot)
        {
            var comp = sdkRoot.GetComponentInChildren<GameUp.SDK.AdmobAds>(true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            Set(so, "bannerAdUnitId", _admobBannerId);
            Set(so, "interstitialAdUnitId", _admobInterstitialId);
            Set(so, "rewardedAdUnitId", _admobRewardedId);
            Set(so, "appOpenAdUnitId", _admobAppOpenId);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(comp);
            return true;
        }

        private void TrySaveAppsFlyerObjectPrefab(List<string> errors)
        {
            if (!IsPrefabAssetPathWritable(PathAppsFlyer)) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PathAppsFlyer) == null) return;

            var type = Type.GetType("AppsFlyerObjectScript, AppsFlyer");
            if (type == null) return;

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(PathAppsFlyer);
                var comp = root != null ? root.GetComponent(type) : null;
                if (comp == null)
                {
                    errors.Add("AppsFlyerObject.prefab (AppsFlyerObjectScript)");
                    return;
                }

                var so = new SerializedObject(comp);
                Set(so, "devKey", _appsFlyerDevKey);
                Set(so, "appID", _appsFlyerAppId);
                SetBool(so, "isDebug", _appsFlyerIsDebug);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(comp);
                PrefabUtility.SaveAsPrefabAsset(root, PathAppsFlyer);
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private void TrySaveIronSourcePrefabAsset(List<string> errors)
        {
            if (!IsIronSourceSetupSectionAvailable()) return;
            if (!IsPrefabAssetPathWritable(PathIronSource)) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PathIronSource) == null) return;

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(PathIronSource);
                var comp = root != null ? root.GetComponentInChildren<GameUp.SDK.IronSourceAds>(true) : null;
                if (comp == null)
                {
                    errors.Add("IronSourceAds.prefab (IronSourceAds)");
                    return;
                }

                var so = new SerializedObject(comp);
                Set(so, "levelPlayAppKey", _ironSourceAppKey);
                Set(so, "bannerAdUnitId", _ironSourceBannerId);
                Set(so, "interstitialAdUnitId", _ironSourceInterstitialId);
                Set(so, "rewardedVideoAdUnitId", _ironSourceRewardedId);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(comp);
                PrefabUtility.SaveAsPrefabAsset(root, PathIronSource);
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }
#endif

        private void TrySaveAdMobPrefabAsset(List<string> errors)
        {
            if (!IsPrefabAssetPathWritable(PathAdMob)) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PathAdMob) == null) return;

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(PathAdMob);
                var comp = root?.GetComponent<GameUp.SDK.AdmobAds>();
                if (comp == null)
                {
                    errors.Add("AdmobAds.prefab (AdmobAds)");
                    return;
                }

                var so = new SerializedObject(comp);
                Set(so, "bannerAdUnitId", _admobBannerId);
                Set(so, "interstitialAdUnitId", _admobInterstitialId);
                Set(so, "rewardedAdUnitId", _admobRewardedId);
                Set(so, "appOpenAdUnitId", _admobAppOpenId);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(comp);
                PrefabUtility.SaveAsPrefabAsset(root, PathAdMob);
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private bool SaveSceneFirebaseRemoteConfigUtils(GameObject sdkRoot)
        {
            if (sdkRoot == null) return false;
            var comp = sdkRoot.GetComponent<FirebaseRemoteConfigUtils>();
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            SetInt(so, "inter_capping_time", _rcInterCappingTime);
            SetInt(so, "inter_start_level", _rcInterStartLevel);
            SetBool(so, "enable_rate_app", _rcEnableRateApp);
            SetInt(so, "level_start_show_rate_app", _rcLevelStartShowRateApp);
            SetBool(so, "no_internet_popup_enable", _rcNoInternetPopupEnable);
            SetBool(so, "enable_banner", _rcEnableBanner);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(comp);
            PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            return true;
        }

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private bool SaveSceneIronSource(GameObject sdkRoot)
        {
            if (sdkRoot == null) return false;
            var comp = sdkRoot.GetComponentInChildren<GameUp.SDK.IronSourceAds>(true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            Set(so, "levelPlayAppKey", _ironSourceAppKey);
            Set(so, "bannerAdUnitId", _ironSourceBannerId);
            Set(so, "interstitialAdUnitId", _ironSourceInterstitialId);
            Set(so, "rewardedVideoAdUnitId", _ironSourceRewardedId);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(comp);
            PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            return true;
        }
#endif

        private bool SaveSceneAdMob(GameObject sdkRoot)
        {
            if (sdkRoot == null) return false;
            var comp = sdkRoot.GetComponentInChildren<GameUp.SDK.AdmobAds>(true);
            if (comp == null) return false;
            var so = new SerializedObject(comp);
            Set(so, "bannerAdUnitId", _admobBannerId);
            Set(so, "interstitialAdUnitId", _admobInterstitialId);
            Set(so, "rewardedAdUnitId", _admobRewardedId);
            Set(so, "appOpenAdUnitId", _admobAppOpenId);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(comp);
            PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            return true;
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
    }
}
