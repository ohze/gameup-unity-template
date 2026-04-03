using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace GameUp.SDK.Installer
{
    /// <summary>
    /// Cửa sổ hướng dẫn cài đặt tất cả package phụ thuộc của GameUp SDK.
    /// Tự động xuất hiện khi SDK được cài lần đầu tiên qua Git URL Package.
    /// </summary>
    public class GameUpDependenciesWindow : EditorWindow
    {
        // ─── Định nghĩa các package phụ thuộc ────────────────────────────────────

        private enum InstallMethod
        {
            /// <summary>Cài qua Unity Package Manager bằng Git URL</summary>
            GitUrl,

            /// <summary>Cài qua scoped registry trong manifest.json</summary>
            ScopedRegistry,

            /// <summary>Import .unitypackage đã được bundle trong thư mục Packages~</summary>
            UnityPackage,

            /// <summary>Chỉ mở trang web — cài thủ công</summary>
            OpenUrl,
        }

        private class PackageDef
        {
            public string DisplayName;
            public string Description;
            public bool Required;

            /// <summary>Tên assembly để detect xem package đã cài chưa</summary>
            public string AssemblyName;

            /// <summary>
            /// Nếu set: coi là đã cài khi tìm thấy type này trong bất kỳ assembly đã load (vd GameAnalytics .unitypackage → Assembly-CSharp).
            /// Vẫn kết hợp với <see cref="AssemblyName"/> nếu có assembly UPM riêng.
            /// </summary>
            public string InstalledTypeFullName;

            public InstallMethod Method;

            // Git URL (dùng khi Method == GitUrl)
            public string GitUrl;

            // Scoped registry (dùng khi Method == ScopedRegistry)
            public string RegistryName;
            public string RegistryUrl;
            public string[] RegistryScopes;
            public string PackageId;

            /// <summary>
            /// Danh sách file .unitypackage trong thư mục Packages~.
            /// Hỗ trợ subfolder: vd "Firebase/FirebaseAnalytics.unitypackage".
            /// Tất cả file sẽ được import theo thứ tự.
            /// </summary>
            public string[] BundledFileNames;

            /// <summary>
            /// URL để tải từng file tương ứng với BundledFileNames.
            /// Dùng khi file không có trong Packages~ (vd: cài từ .unitypackage).
            /// Index phải khớp 1-1 với BundledFileNames.
            /// </summary>
            public string[] HostedUrls;

            // URL trang tải thủ công (fallback cuối khi cả local lẫn hosted URL đều thất bại)
            public string DownloadUrl;
            public string DownloadLabel;

            /// <summary>
            /// Đường dẫn asset (vd Assets/FacebookSDK/Examples) sẽ xóa ngay sau khi import .unitypackage
            /// (bỏ sample/examples gây lỗi compile hoặc không cần trong production).
            /// </summary>
            public string[] DeleteAssetPathsAfterImport;

            /// <summary>
            /// Thứ tự cài khuyến nghị (số nhỏ trước): Facebook → Firebase (EDM) → AdMob/LevelPlay → adapters AdMob → AppsFlyer → GameAnalytics.
            /// Batch install, import sau download và danh sách UI đều sort theo trường này.
            /// </summary>
            public int InstallPriority;

            // ── Runtime state ──
            public bool IsInstalled;
            public bool IsInstalling;
            public string InstallError;
        }

        // ─── Thay đổi URL ở đây khi cập nhật phiên bản SDK ─────────────────────────
        // Đặt file vào Assets/GameUpSDK/Packages~/ để dùng local (Git URL install).
        // Nếu không có file local, installer tự download từ HostedUrls (unitypackage install).

        private static readonly PackageDef[] s_packages =
        {
            new PackageDef
            {
                DisplayName = "Facebook Unity SDK 18.0.0",
                Description =
                    "Bắt buộc. Facebook SDK cho Unity (login, sharing, v.v.). Khi cài qua installer, thư mục Examples sẽ tự bỏ để tránh lỗi.",
                Required = true,
                // Facebook.Unity.dll thường tắt trên Editor; assembly Editor luôn load khi đã import SDK.
                AssemblyName = "Facebook.Unity.Editor",
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "Facebook/facebook-unity-sdk-18.0.0.unitypackage" },
                HostedUrls = new[]
                {
                    "https://github.com/ohze/gameup-unity-template/releases/download/deps/facebook-unity-sdk-18.0.0.unitypackage",
                },
                DownloadUrl = "https://developers.facebook.com/docs/unity/downloads/",
                DownloadLabel = "Tải Facebook Unity SDK →",
                DeleteAssetPathsAfterImport = new[] { "Assets/FacebookSDK/Examples" },
                InstallPriority = 10,
            },
            new PackageDef
            {
                DisplayName = "Firebase SDK  (Analytics + Crashlytics + Remote Config)",
                Description = "Bắt buộc. Analytics, crash reporting, remote configuration. Bao gồm EDM4U.",
                Required = false,
                AssemblyName = "Firebase.App",
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[]
                {
                    "Firebase/FirebaseAnalytics.unitypackage",
                    "Firebase/FirebaseCrashlytics.unitypackage",
                    "Firebase/FirebaseRemoteConfig.unitypackage",
                },
                HostedUrls = new[]
                {
                    "https://github.com/ohze/gameup-unity-template/releases/download/deps/FirebaseAnalytics.unitypackage",
                    "https://github.com/ohze/gameup-unity-template/releases/download/deps/FirebaseCrashlytics.unitypackage",
                    "https://github.com/ohze/gameup-unity-template/releases/download/deps/FirebaseRemoteConfig.unitypackage",
                },
                DownloadUrl = "https://firebase.google.com/docs/unity/setup",
                DownloadLabel = "Tải Firebase Unity SDK →",
                InstallPriority = 20,
            },
            new PackageDef
            {
                DisplayName = "Google Mobile Ads — AdMob",
                Description = "Bắt buộc nếu dùng AdMob standalone (Interstitial/Rewarded/AppOpen) hoặc muốn bắt paid event để log ad_impression.",
                Required = false,
                AssemblyName = "GoogleMobileAds",
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "GoogleMobileAds-v10.7.0.unitypackage" },
                HostedUrls = new[]
                {
                    "https://github.com/ohze/gameup-unity-template/releases/download/deps/GoogleMobileAds-v10.7.0.unitypackage",
                },
                DownloadUrl = "https://github.com/googlesamples/unity-admob-sdk/releases",
                DownloadLabel = "Tải AdMob Plugin →",
                InstallPriority = 30,
            },
            new PackageDef
            {
                DisplayName = "IronSource LevelPlay SDK",
                Description = "Tùy chọn. Cần nếu bạn chọn Primary Mediation = LevelPlay trong AdsManager.",
                Required = false,
                AssemblyName = "Unity.LevelPlay",
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "UnityLevelPlay_v9.2.0.unitypackage" },
                HostedUrls = new[]
                {
                    "https://github.com/ohze/gameup-unity-template/releases/download/deps/UnityLevelPlay_v9.2.0.unitypackage",
                },
                DownloadUrl = "https://developers.is.com/ironsource-mobile/unity/unity-plugin/",
                DownloadLabel = "Tải IronSource SDK →",
                InstallPriority = 30,
            },
            new PackageDef
            {
                // Firebase gồm 3 file riêng trong subfolder Firebase/
                // EDM4U (Google.VersionHandler) được bundle kèm trong FirebaseAnalytics
                DisplayName      = "AppsFlyer Attribution SDK",
                Description      = "Tùy chọn. Mobile measurement & attribution.",
                Required         = false,
                AssemblyName     = "AppsFlyer",
                Method           = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "appsflyer-unity-plugin-6.17.81.unitypackage" },
                HostedUrls       = new[]
                {
                    "https://github.com/ohze/gameup-unity-template/releases/download/deps/appsflyer-unity-plugin-6.17.81.unitypackage",
                },
                DownloadUrl      = "https://github.com/AppsFlyerSDK/appsflyer-unity-plugin/releases",
                DownloadLabel    = "Tải AppsFlyer SDK →",
                InstallPriority = 45,
            },
            new PackageDef
            {
                DisplayName = "GameAnalytics SDK",
                Description = "Tùy chọn. Analytics sản phẩm (funnels, progression). GameUpAnalytics gửi design event (tiền tố gameup:) khi bật define GAMEANALYTICS_DEPENDENCIES_INSTALLED. Cần GameObject GameAnalytics + keys trong scene (Window → GameAnalytics).",
                Required = false,
                AssemblyName = "GameAnalyticsSDK",
                InstalledTypeFullName = "GameAnalyticsSDK.GameAnalytics",
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "GameAnalytics/GA_SDK_UNITY_7.10.6.unitypackage" },
                HostedUrls = new[]
                {
                    "https://download.gameanalytics.com/unity/7.10.6/GA_SDK_UNITY.unitypackage",
                },
                DownloadUrl = "https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/",
                DownloadLabel = "GameAnalytics Unity SDK →",
                InstallPriority = 46,
            },

            new PackageDef
            {
                DisplayName      = "Admob Mediation Adapter (Unity + Ironsource + Liftoff)",
                Description      = "Dùng khi sử dụng Admob Mediation",
                Required         = false,
                // Detect một trong các adapters đã được import.
                // (Không có built-in multi-assembly check; ưu tiên 1 adapter phổ biến để "đánh dấu đã cài").
                AssemblyName     = "GoogleMobileAds.Mediation.IronSource.Api",
                Method           = InstallMethod.UnityPackage,
                BundledFileNames = new[]
                {
                    "GoogleMobileAdsUnityAdsMediation.unitypackage",
                    "GoogleMobileAdsIronSourceMediation.unitypackage",
                    "GoogleMobileAdsLiftoffMonetizeMediation.unitypackage",
                },
                HostedUrls       = new[]
                {
                    "https://github.com/ohze/gameup-unity-template/releases/download/sdk/GoogleMobileAdsUnityAdsMediation.unitypackage",
                    "https://github.com/ohze/gameup-unity-template/releases/download/sdk/GoogleMobileAdsIronSourceMediation.unitypackage",
                    "https://github.com/ohze/gameup-unity-template/releases/download/deps/GoogleMobileAdsLiftoffMonetizeMediation.unitypackage",
                },
                DownloadUrl      = "https://firebase.google.com/docs/unity/setup",
                DownloadLabel    = "Admob Mediation Adapter →",
                InstallPriority = 35,
            },
        };


        // ─── State ────────────────────────────────────────────────────────────────

        private Vector2 _scroll;
        private bool _isBatchInstalling;

        /// <summary>Package sẽ cài trong lần batch hiện tại (null = toàn bộ s_packages — chỉ dùng nội bộ).</summary>
        private List<PackageDef> _batchScope;

        /// <summary>Bật khi batch bắt đầu từ &quot;Cài tất cả&quot; — gợi ý menu Ensure GameAnalytics asmdef khi xong.</summary>
        private bool _gameAnalyticsSetupHintAfterBatch;
        private bool _wasCompiling;

        // Queue PackageManager (GitUrl / ScopedRegistry)
        private readonly Queue<PackageDef> _installQueue = new Queue<PackageDef>();
        private AddRequest _currentAddRequest;
        private PackageDef _currentInstallingPackage;

        // ── Parallel download state ──
        private class DownloadTask
        {
            public PackageDef Pkg;
            public string FileName;
            public string TempPath;
            public UnityWebRequest Request;
            public bool IsDone;
            public bool HasError;
            public string ErrorMessage;
        }

        private List<DownloadTask> _parallelTasks;
        private Action _parallelDoneCallback;
        private float _downloadProgress;
        private string _downloadStatus;

        // Kept for backward compat with OnDisable / PollDownloadQueue references — sẽ không dùng nữa
        private UnityWebRequest _activeDownload;
        private PackageDef _downloadingPkg;
        private bool _foldoutUgUiPackageCacheHelp;
        private const string LevelPlayDepsDefine = "LEVELPLAY_DEPENDENCIES_INSTALLED";
        private const string AdMobDepsDefine = "ADMOB_DEPENDENCIES_INSTALLED";
        private const string FirebaseDepsDefine = "FIREBASE_DEPENDENCIES_INSTALLED";
        private const string AppsFlyerDepsDefine = "APPSFLYER_DEPENDENCIES_INSTALLED";
        private const string GameAnalyticsDepsDefine = "GAMEANALYTICS_DEPENDENCIES_INSTALLED";
        private const string FacebookDepsDefine = "FACEBOOK_DEPENDENCIES_INSTALLED";

        // ─── Static helpers ───────────────────────────────────────────────────────

        private static int PackageIndexInCatalog(PackageDef pkg)
        {
            for (int i = 0; i < s_packages.Length; i++)
            {
                if (ReferenceEquals(s_packages[i], pkg))
                    return i;
            }

            return int.MaxValue;
        }

        private static bool IsGameAnalyticsSdkPackage(PackageDef pkg) =>
            pkg != null && string.Equals(pkg.AssemblyName, "GameAnalyticsSDK", StringComparison.OrdinalIgnoreCase);

        private static IEnumerable<PackageDef> OrderedInstallSequence(IEnumerable<PackageDef> items)
        {
            return items.OrderBy(p => p.InstallPriority).ThenBy(PackageIndexInCatalog);
        }

        [MenuItem("GameUp/SDK/Setup Dependencies")]
        public static void ShowWindow()
        {
            var win = GetWindow<GameUpDependenciesWindow>(true, "GameUp SDK — Setup Dependencies");
            win.minSize = new Vector2(560, 520);
            // Đặt kích thước ban đầu nếu window chưa được mở
            if (win.position.width < 560)
                win.position = new Rect(win.position.x, win.position.y, 620, 580);
            win.RefreshStatus();
        }

        /// <summary>
        /// Xóa <c>Library/PackageCache</c> và <c>Library/ScriptAssemblies</c> để Unity tải lại
        /// <c>com.unity.ugui</c> khớp với bản Editor (tránh lỗi GraphicRaycaster / Dropdown / ListPool).
        /// </summary>
        [MenuItem("GameUp/SDK/Troubleshooting/Fix Unity UI package cache (com.unity.ugui errors)…", false, 50)]
        public static void MenuRepairUnityPackageCache()
        {
            RepairUnityPackageCacheWithConfirmation();
        }

        /// <summary>
        /// Hiển thị hộp thoại xác nhận rồi xóa cache; dùng chung cho menu và nút trong cửa sổ Setup Dependencies.
        /// </summary>
        public static void RepairUnityPackageCacheWithConfirmation()
        {
            if (!EditorUtility.DisplayDialog(
                    "GameUp SDK — Sửa cache gói Unity UI",
                    "Sẽ xóa thư mục:\n• Library/PackageCache\n• Library/ScriptAssemblies\n\n" +
                    "Unity sẽ tải lại các gói (gồm com.unity.ugui) cho khớp với phiên bản Editor hiện tại. " +
                    "Dùng khi Console báo lỗi compile trong PackageCache (vd. GraphicRaycaster, Dropdown, ListPool).\n\n" +
                    "Đóng các ứng dụng khác đang giữ file trong Library (hiếm). Tiếp tục?",
                    "Xóa cache",
                    "Hủy"))
                return;

            if (!TryDeleteUnityLibraryPackageCaches(out string err))
            {
                EditorUtility.DisplayDialog("GameUp SDK — Không xóa được cache", err, "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "GameUp SDK — Đã xóa cache",
                "Đã xóa PackageCache và ScriptAssemblies. Unity sẽ resolve package và compile lại.\n\n" +
                "Nếu vẫn lỗi: đóng Unity hoàn toàn, xóa cả thư mục Library, mở lại project bằng đúng Unity trong ProjectSettings/ProjectVersion.txt.",
                "OK");

            AssetDatabase.Refresh();
            Client.Resolve();
            CompilationPipeline.RequestScriptCompilation();
        }

        /// <summary>Xóa PackageCache + ScriptAssemblies; trả false và message nếu thất bại.</summary>
        internal static bool TryDeleteUnityLibraryPackageCaches(out string errorMessage)
        {
            errorMessage = null;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string packageCache = Path.Combine(projectRoot, "Library", "PackageCache");
                string scriptAssemblies = Path.Combine(projectRoot, "Library", "ScriptAssemblies");

                if (Directory.Exists(packageCache))
                    FileUtil.DeleteFileOrDirectory(packageCache);
                if (Directory.Exists(scriptAssemblies))
                    FileUtil.DeleteFileOrDirectory(scriptAssemblies);

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra nhanh (đồng bộ) xem tất cả package bắt buộc đã cài chưa.
        /// Dùng bởi GameUpPackageInstaller để quyết định có mở window không.
        /// </summary>
        public static bool AreAllRequiredPackagesInstalled()
        {
            return s_packages
                .Where(p => p.Required)
                .All(IsPackageInstalled);
        }

        // ─── Lifecycle ────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            RefreshStatus();
            _wasCompiling = EditorApplication.isCompiling;
            EditorApplication.update += EditorUpdateRepaintWhenBusy;

            // Khi đổi Scripting Define Symbols, Unity sẽ trigger compile + domain reload.
            // Rely vào _wasCompiling đôi khi miss edge (window bị recreate sau reload),
            // nên subscribe thêm các events này để luôn refresh UI/state sau khi compile/reload xong.
            AssemblyReloadEvents.afterAssemblyReload -= AfterAssemblyReloadRefresh;
            AssemblyReloadEvents.afterAssemblyReload += AfterAssemblyReloadRefresh;
            CompilationPipeline.compilationFinished -= OnCompilationFinishedRefresh;
            CompilationPipeline.compilationFinished += OnCompilationFinishedRefresh;
            AssetDatabase.importPackageCompleted -= OnImportPackageRefresh;
            AssetDatabase.importPackageCompleted += OnImportPackageRefresh;
            AssetDatabase.importPackageFailed -= OnImportPackageFailedRefresh;
            AssetDatabase.importPackageFailed += OnImportPackageFailedRefresh;
            AssetDatabase.importPackageCancelled -= OnImportPackageRefresh;
            AssetDatabase.importPackageCancelled += OnImportPackageRefresh;

            // Sau domain reload/restore window, timing load assemblies có thể trễ hơn compilationFinished.
            // DelayCall 1 nhịp là đủ để tránh scan quá sớm.
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                RefreshStatus();
            };
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdateRepaintWhenBusy;
            EditorApplication.update -= PollInstallQueue;
            EditorApplication.update -= PollParallelDownloads;
            AssemblyReloadEvents.afterAssemblyReload -= AfterAssemblyReloadRefresh;
            CompilationPipeline.compilationFinished -= OnCompilationFinishedRefresh;
            AssetDatabase.importPackageCompleted -= OnImportPackageRefresh;
            AssetDatabase.importPackageFailed -= OnImportPackageFailedRefresh;
            AssetDatabase.importPackageCancelled -= OnImportPackageRefresh;
            if (_parallelTasks != null)
            {
                foreach (var t in _parallelTasks)
                    t.Request?.Dispose();
                _parallelTasks = null;
            }

            _activeDownload?.Dispose();
            _activeDownload = null;
        }

        private void AfterAssemblyReloadRefresh()
        {
            // DelayCall để đảm bảo assemblies đã available đầy đủ trước khi scan IsAssemblyLoaded.
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                RefreshStatus();
            };
        }

        private void OnCompilationFinishedRefresh(object _)
        {
            // compilationFinished có thể bắn khi window vừa được recreate,
            // nên chỉ cần schedule refresh + repaint an toàn.
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                RefreshStatus();
            };
        }

        private void OnImportPackageRefresh(string _)
        {
            // ImportPackage hoàn thành có thể trigger refresh/compile; delayCall để tránh scan quá sớm.
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                RefreshStatus();
            };
        }

        private void OnImportPackageFailedRefresh(string _, string __)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                RefreshStatus();
            };
        }

        /// <summary>Làm mới UI khi đang compile hoặc đang cài để nút bật/tắt đúng lúc compile xong.</summary>
        private void EditorUpdateRepaintWhenBusy()
        {
            bool compiling = EditorApplication.isCompiling;

            // Compile vừa kết thúc → assemblies đã reload, refresh trạng thái package một lần.
            if (_wasCompiling && !compiling)
                RefreshStatus();

            _wasCompiling = compiling;

            if (compiling || IsInstallOrDownloadBusy())
                Repaint();
        }

        private bool IsInstallOrDownloadBusy()
        {
            if (_isBatchInstalling) return true;
            if (_installQueue.Count > 0) return true;
            if (_currentAddRequest != null) return true;
            if (_parallelTasks != null && _parallelTasks.Count > 0) return true;
            foreach (var p in s_packages)
            {
                if (p.IsInstalling) return true;
            }

            return false;
        }

        /// <summary>Khóa mọi thao tác: đang compile hoặc đang cài/tải package.</summary>
        private bool IsInteractionLocked()
        {
            // Theo yêu cầu: luôn mở UI để tránh trường hợp trạng thái nút không kịp cập nhật sau compile/import.
            return false;
        }

        // ─── GUI ─────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (IsInstallOrDownloadBusy())
            {
                EditorGUILayout.HelpBox("Đang xử lý cài/tải dependency…", MessageType.Info);
                EditorGUILayout.Space(4);
            }

            DrawHeader();
            DrawMediationInfo();
            DrawUgUiPackageCacheTroubleshootFoldout();
            DrawFacebookExamplesCleanupSection();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawPackageList();
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private static bool HasDefine(string define)
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            return symbols.Contains(define);
        }

        private static void SetDefine(string define, bool enabled)
        {
            foreach (var group in s_buildTargetGroups)
            {
                try
                {
                    string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                    var list = new List<string>(
                        current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

                    bool changed = false;

                    if (enabled && !list.Contains(define))
                    {
                        list.Add(define);
                        changed = true;
                    }
                    else if (!enabled && list.Remove(define))
                    {
                        changed = true;
                    }

                    if (changed)
                    {
                        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", list));
                    }
                }
                catch { }
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField(
                "GameUp SDK — Setup Dependencies",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 });

            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "Có thể cài nhanh bằng \"Cài tất cả\" trong khung Mediation (đúng thứ tự khuyến nghị), hoặc cài từng bước bằng \"Cài pack\" trên từng dòng — nên chờ Unity compile (và EDM/Android Resolver nếu bật) giữa các bước khi cài lẻ.\n" +
                "Khi đang compile hoặc đang cài/tải, nút Mediation và \"Cài pack\" đều bị khóa.",
                MessageType.Info);

            EditorGUILayout.Space(6);
        }

        private void DrawMediationInfo()
        {
            EditorGUILayout.Space(6);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Mediation Settings", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(IsInteractionLocked());
            var current = GetPrimaryMediationFromDefines();
            var next = (AdsManager.PrimaryMediation)EditorGUILayout.EnumPopup("Primary Mediation", current);
            if (next != current)
            {
                SetPrimaryMediationDefines(next);
                RefreshStatus();
            }

            EditorGUI.EndDisabledGroup();

            var pm = GetPrimaryMediationFromDefines();
            var planned = GetPackagesForSdkSetup(pm);
            var missingAuto = planned.Where(p => !p.IsInstalled && CanAutoInstall(p)).ToList();
            var missingManual = planned.Where(p => !p.IsInstalled && !CanAutoInstall(p)).ToList();

            string planDesc = pm == AdsManager.PrimaryMediation.AdMob
                ? "Facebook, Firebase, AppsFlyer, GameAnalytics, Google Mobile Ads, AdMob Mediation Adapters."
                : "Facebook, Firebase, AppsFlyer, GameAnalytics, IronSource LevelPlay.";

            EditorGUILayout.HelpBox(
                "Primary Mediation chọn bộ pack quảng cáo (AdMob + adapters hay LevelPlay). " +
                "Dùng nút bên dưới để cài một lần mọi mục còn thiếu trong bộ đó (đúng thứ tự), hoặc \"Cài pack\" từng dòng trong danh sách.\n" +
                "Bộ theo mediation hiện tại: " + planDesc,
                MessageType.Info);

            EditorGUILayout.HelpBox(
                "Thứ tự nên cài: (1) Facebook → (2) Firebase (kèm EDM) — chờ compile/resolve xong — → " +
                "(3) Google Mobile Ads hoặc LevelPlay (trùng với Primary Mediation) → " +
                "(4) AdMob Mediation adapters (chỉ khi dùng AdMob) → (5) AppsFlyer → (6) GameAnalytics. " +
                "Các mục tùy chọn có thể bỏ qua nếu không dùng.",
                MessageType.None);

            if (missingManual.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Có package không cài tự động được (thiếu file trong Packages~ và không có URL tải). Cần tải/import thủ công theo mô tả từng dòng.",
                    MessageType.Warning);
            }

            if (missingAuto.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Còn {missingAuto.Count} mục có thể cài tự động — bấm \"Cài tất cả\" hoặc \"Cài pack\" lần lượt từ trên xuống trong danh sách.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Theo Primary Mediation, không còn mục nào thiếu mà installer tự cài được (hoặc đã đủ).",
                    MessageType.None);
            }

            EditorGUI.BeginDisabledGroup(IsInteractionLocked() || missingAuto.Count == 0);
            if (GUILayout.Button(
                    missingAuto.Count > 0
                        ? $"⬇ Cài tất cả còn thiếu ({missingAuto.Count}) — theo thứ tự khuyến nghị"
                        : "✓ Đã đủ package (tự động) cho Primary Mediation",
                    GUILayout.Height(28)))
            {
                if (missingAuto.Count > 0)
                    StartBatchInstall(planned, showGameAnalyticsSetupHintWhenComplete: true);
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.HelpBox(
                "Primary Mediation lưu bằng Scripting Define Symbols (`" + GUDefinetion.PrimaryMediationLevelPlay + "` / `" + GUDefinetion.PrimaryMediationAdMob + "`) — phù hợp khi GameUp SDK cài dạng UPM package (không tạo asset trong Assets/).",
                MessageType.Info
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawUgUiPackageCacheTroubleshootFoldout()
        {
            EditorGUILayout.Space(4);
            _foldoutUgUiPackageCacheHelp = EditorGUILayout.Foldout(
                _foldoutUgUiPackageCacheHelp,
                "Gỡ lỗi: lỗi compile trong com.unity.ugui (PackageCache)",
                true);

            if (!_foldoutUgUiPackageCacheHelp)
                return;

            EditorGUILayout.HelpBox(
                "Nếu Console báo lỗi trong Library/PackageCache/com.unity.ugui (vd. GraphicRaycaster, Dropdown, ListPool): " +
                "đó thường do cache gói Unity lệch với bản Editor — không phải do mã GameUp SDK. " +
                "Mở project luôn bằng đúng phiên bản Unity trong ProjectSettings/ProjectVersion.txt.",
                MessageType.Warning);

            if (GUILayout.Button("Xóa Package Cache + ScriptAssemblies (tải lại gói Unity UI)", GUILayout.Height(26)))
                RepairUnityPackageCacheWithConfirmation();
        }

        private const string FacebookExamplesAssetPath = "Assets/FacebookSDK/Examples";

        private static bool FacebookSdkExamplesFolderExists()
        {
            return AssetDatabase.IsValidFolder(FacebookExamplesAssetPath);
        }

        private void DrawFacebookExamplesCleanupSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Facebook SDK — Examples", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Thư mục Examples thường không cần cho production và có thể gây lỗi compile. " +
                "Khi cài Facebook qua installer, Examples đã được xóa tự động; nếu bạn import tay hoặc còn sót, dùng nút bên dưới.",
                MessageType.None);

            EditorGUI.BeginDisabledGroup(IsInstallOrDownloadBusy() || !FacebookSdkExamplesFolderExists());
            if (GUILayout.Button("Xóa thủ công: Assets/FacebookSDK/Examples", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog(
                        "GameUp SDK — Xóa Facebook Examples",
                        "Xóa toàn bộ thư mục Assets/FacebookSDK/Examples?\n\n" +
                        "SDK Facebook chính (ngoài Examples) không bị gỡ. Có thể hoàn tác qua Git/VCS nếu cần.",
                        "Xóa",
                        "Hủy"))
                {
                    TryDeleteFacebookExamplesFolder();
                    Repaint();
                }
            }

            EditorGUI.EndDisabledGroup();

            if (!FacebookSdkExamplesFolderExists())
            {
                EditorGUILayout.LabelField(
                    "Không thấy thư mục (đã xóa hoặc chưa import Facebook SDK).",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>Xóa <c>Assets/FacebookSDK/Examples</c> qua AssetDatabase (nút thủ công trong installer).</summary>
        internal static void TryDeleteFacebookExamplesFolder()
        {
            if (!FacebookSdkExamplesFolderExists())
            {
                EditorUtility.DisplayDialog(
                    "GameUp SDK",
                    "Không có thư mục " + FacebookExamplesAssetPath + ".",
                    "OK");
                return;
            }

            if (!AssetDatabase.DeleteAsset(FacebookExamplesAssetPath))
            {
                Debug.LogWarning("[GameUpSDK] Không xóa được: " + FacebookExamplesAssetPath);
                EditorUtility.DisplayDialog(
                    "GameUp SDK",
                    "Xóa thất bại. Kiểm tra Console hoặc đóng file đang mở trong thư mục đó.",
                    "OK");
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log("[GameUpSDK] Đã xóa " + FacebookExamplesAssetPath);
        }

        /// <summary>Firebase + AppsFlyer + bộ mediation theo lựa chọn (AdMob: GMA + adapters; LevelPlay: LevelPlay), đã sort <see cref="PackageDef.InstallPriority"/>.</summary>
        private static List<PackageDef> GetPackagesForSdkSetup(AdsManager.PrimaryMediation mediation)
        {
            var list = new List<PackageDef>();

            void AddByAssembly(string assemblyName)
            {
                var p = s_packages.FirstOrDefault(x => x.AssemblyName == assemblyName);
                if (p != null && !list.Contains(p))
                    list.Add(p);
            }

            AddByAssembly("Facebook.Unity.Editor");
            AddByAssembly("Firebase.App");
            AddByAssembly("AppsFlyer");
            AddByAssembly("GameAnalyticsSDK");

            if (mediation == AdsManager.PrimaryMediation.AdMob)
            {
                AddByAssembly("GoogleMobileAds");
                AddByAssembly("GoogleMobileAds.Mediation.IronSource.Api");
            }
            else
            {
                AddByAssembly("Unity.LevelPlay");
            }

            return OrderedInstallSequence(list).ToList();
        }

        private static AdsManager.PrimaryMediation GetPrimaryMediationFromDefines()
        {
            if (HasDefine(GUDefinetion.PrimaryMediationAdMob)) return AdsManager.PrimaryMediation.AdMob;
            return AdsManager.PrimaryMediation.LevelPlay;
        }

        private static void SetPrimaryMediationDefines(AdsManager.PrimaryMediation mediation)
        {
            SetDefine(GUDefinetion.PrimaryMediationAdMob, mediation == AdsManager.PrimaryMediation.AdMob);
            SetDefine(GUDefinetion.PrimaryMediationLevelPlay, mediation == AdsManager.PrimaryMediation.LevelPlay);
        }

        /// <summary>Đảm bảo có đúng một define mediation (mặc định LevelPlay nếu chưa có).</summary>
        private static void EnsurePrimaryMediationDefines()
        {
            bool lp = HasDefine(GUDefinetion.PrimaryMediationLevelPlay);
            bool admob = HasDefine(GUDefinetion.PrimaryMediationAdMob);
            if (!lp && !admob)
                SetDefine(GUDefinetion.PrimaryMediationLevelPlay, true);
            else if (lp && admob)
                SetDefine(GUDefinetion.PrimaryMediationAdMob, false);
        }

        private void DrawPackageList()
        {
            bool drewRequired = false, drewOptional = false;

            foreach (var pkg in OrderedInstallSequence(s_packages))
            {
                // Section headers
                if (pkg.Required && !drewRequired)
                {
                    DrawSectionHeader("BẮT BUỘC");
                    drewRequired = true;
                }

                if (!pkg.Required && !drewOptional)
                {
                    EditorGUILayout.Space(8);
                    DrawSectionHeader("TÙY CHỌN");
                    drewOptional = true;
                }

                DrawPackageRow(pkg);
            }
        }

        private static void DrawSectionHeader(string title)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(4, 0, 6, 2),
            };
            EditorGUILayout.LabelField(title, style);
        }

        private void DrawPackageRow(PackageDef pkg)
        {
            bool isDownloading = _parallelTasks?.Any(t => t.Pkg == pkg && !t.IsDone) == true;
            bool isInstalling = pkg.IsInstalling
                                || (_isBatchInstalling && _installQueue.Contains(pkg))
                                || isDownloading;
            Color boxColor = pkg.IsInstalled ? new Color(0.18f, 0.45f, 0.18f, 0.3f)
                : isInstalling ? new Color(0.3f, 0.3f, 0.6f, 0.3f)
                : new Color(0.45f, 0.18f, 0.18f, 0.3f);

            // Row background
            var rect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height + 2), boxColor);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);

            // Status icon
            string icon = pkg.IsInstalled ? "✓" : isInstalling ? "⟳" : "✗";
            var iconStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = pkg.IsInstalled ? Color.green : isInstalling ? Color.yellow : Color.red },
                fixedWidth = 24,
            };
            GUILayout.Label(icon, iconStyle, GUILayout.Width(24));

            // Name + description
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(pkg.DisplayName, EditorStyles.boldLabel);
            var descStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            EditorGUILayout.LabelField(pkg.Description, descStyle);
            if (!string.IsNullOrEmpty(pkg.InstallError))
                EditorGUILayout.HelpBox(pkg.InstallError, MessageType.Error);
            EditorGUILayout.EndVertical();

            // Trạng thái / Cài pack (từng dòng, không cài gom)
            GUILayout.Space(4);
            if (pkg.IsInstalled)
            {
                var greenStyle = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = Color.green }, fontStyle = FontStyle.Bold };
                GUILayout.Label("Đã cài", greenStyle, GUILayout.Width(64));
            }
            else if (isDownloading)
            {
                var pkgTasks = _parallelTasks?.Where(t => t.Pkg == pkg).ToList();
                int total = pkgTasks?.Count ?? 0;
                int done = pkgTasks?.Count(t => t.IsDone) ?? 0;
                float prog = total > 0
                    ? pkgTasks.Average(t => t.IsDone ? 1f : t.Request?.downloadProgress ?? 0f)
                    : 0f;

                EditorGUILayout.BeginVertical(GUILayout.Width(190));
                EditorGUILayout.LabelField(
                    total > 1 ? $"Đang tải... {done}/{total} files" : "Đang tải...",
                    EditorStyles.miniLabel, GUILayout.Width(190));
                var barRect = EditorGUILayout.GetControlRect(GUILayout.Width(190), GUILayout.Height(6));
                EditorGUI.ProgressBar(barRect, prog, "");
                EditorGUILayout.EndVertical();
            }
            else if (isInstalling)
            {
                EditorGUILayout.LabelField("Đang cài...", GUILayout.Width(100));
            }
            else
            {
                bool canAuto = CanAutoInstall(pkg);
                if (canAuto)
                {
                    //EditorGUI.BeginDisabledGroup(true);
                    if (GUILayout.Button("Cài pack", GUILayout.Width(88), GUILayout.Height(24)))
                        StartSinglePackageInstall(pkg);
                    //EditorGUI.EndDisabledGroup();
                }
                else if (pkg.Method == InstallMethod.OpenUrl)
                {
                    if (GUILayout.Button("Mở trang tải", GUILayout.Width(100), GUILayout.Height(24))
                        && !string.IsNullOrEmpty(pkg.DownloadUrl))
                        Application.OpenURL(pkg.DownloadUrl);
                }
                else
                {
                    var manualStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = true,
                        normal = { textColor = new Color(0.55f, 0.55f, 0.55f) },
                    };
                    GUILayout.Label("Cần file Packages~/URL", manualStyle, GUILayout.Width(118));
                }
            }

            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(2);
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            // Nút refresh thủ công luôn active.
            // Nếu đang compile thì delay để refresh sau khi compile/reload xong.
            if (GUILayout.Button("↻  Làm mới trạng thái", GUILayout.Height(30)))
                RequestManualRefresh();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Manual install hint
            bool hasManualUninstalled = s_packages.Any(p => !p.IsInstalled && p.Method == InstallMethod.OpenUrl);
            if (hasManualUninstalled)
            {
                EditorGUILayout.HelpBox(
                    "Một số package chỉ cài được thủ công: tải .unitypackage từ trang nhà cung cấp, " +
                    "rồi Assets → Import Package → Custom Package…, sau đó \"Làm mới trạng thái\".",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(4);

            // Continue button
            bool allRequiredDone = AreAllRequiredPackagesInstalled();
            if (allRequiredDone)
            {
                EditorGUILayout.HelpBox(
                    "Tất cả package bắt buộc đã được cài đặt! " +
                    "Nhấn bên dưới để mở cửa sổ cấu hình SDK.",
                    MessageType.None);

                // Khi đang compile/cài, không cho bấm (không trigger delay-call) để đúng luồng UX.
                EditorGUI.BeginDisabledGroup(IsInteractionLocked());
                if (GUILayout.Button("→  Mở cấu hình SDK (GameUp SDK Setup)", GUILayout.Height(36)))
                    RequestOpenSetup();
                EditorGUI.EndDisabledGroup();
            }
        }

        private void RequestManualRefresh()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += () =>
                {
                    if (this == null) return;
                    RefreshStatus();
                };
                Repaint();
                return;
            }

            RefreshStatus();
        }

        private void RequestOpenSetup()
        {
            if (IsInteractionLocked())
            {
                EditorApplication.delayCall += () =>
                {
                    if (this == null) return;
                    RequestOpenSetup();
                };
                Repaint();
                return;
            }

            GameUpPackageInstaller.MarkSetupComplete();
            Close();
            EditorApplication.ExecuteMenuItem("GameUp SDK/Setup");
        }

        // ─── Install logic ────────────────────────────────────────────────────────

        private void StartInstall(PackageDef pkg)
        {
            if (pkg.IsInstalling) return;
            pkg.IsInstalling = true;
            pkg.InstallError = null;
            Repaint();

            switch (pkg.Method)
            {
                case InstallMethod.GitUrl:
                    EnqueueGitInstall(pkg);
                    break;

                case InstallMethod.ScopedRegistry:
                    AddScopedRegistryAndPackage(pkg);
                    break;
            }
        }

        private void StartBatchInstall(IReadOnlyList<PackageDef> scope, bool showGameAnalyticsSetupHintWhenComplete = false)
        {
            _gameAnalyticsSetupHintAfterBatch = showGameAnalyticsSetupHintWhenComplete;
            _batchScope = OrderedInstallSequence(
                    scope != null && scope.Count > 0
                        ? scope.Distinct()
                        : s_packages)
                .ToList();
            _isBatchInstalling = true;
            _installQueue.Clear();

            IEnumerable<PackageDef> InScope() => _batchScope;

            // 1) Import các UnityPackage đã có file local (đồng bộ, nhanh)
            foreach (var pkg in InScope())
            {
                if (pkg.IsInstalled) continue;
                if (pkg.Method != InstallMethod.UnityPackage) continue;

                var localPaths = GetBundledPackagePaths(pkg.BundledFileNames);
                if (localPaths == null) continue;

                pkg.InstallError = null;
                ImportUnityPackage(pkg, localPaths);
            }

            // 2) Cài GitUrl / ScopedRegistry (bất đồng bộ)
            foreach (var pkg in InScope())
            {
                if (pkg.IsInstalled) continue;
                if (pkg.Method != InstallMethod.GitUrl && pkg.Method != InstallMethod.ScopedRegistry) continue;

                pkg.InstallError = null;
                _installQueue.Enqueue(pkg);
            }

            // 3) Download song song; import sau khi tải xong theo InstallPriority (tránh import AdMob trước Firebase).
            var downloadPkgs = OrderedInstallSequence(
                    InScope().Where(p => !p.IsInstalled
                                         && p.Method == InstallMethod.UnityPackage
                                         && GetBundledPackagePaths(p.BundledFileNames) == null
                                         && p.HostedUrls?.Length > 0))
                .ToList();

            void FinishBatch()
            {
                _isBatchInstalling = false;
                bool hintGa = _gameAnalyticsSetupHintAfterBatch;
                _gameAnalyticsSetupHintAfterBatch = false;
                _batchScope = null;
                RefreshStatus();
                if (hintGa)
                    NotifyGameAnalyticsAsmdefHint(fromMediationInstallAllBatch: true);
            }

            if (_installQueue.Count > 0)
            {
                // GitUrl chạy trước (bất đồng bộ), download song song sau khi xong
                ProcessNextInQueueThen(() =>
                {
                    if (downloadPkgs.Count > 0)
                        StartParallelDownloadAndImport(downloadPkgs, onAllDone: FinishBatch);
                    else
                        FinishBatch();
                });
            }
            else if (downloadPkgs.Count > 0)
            {
                // Chỉ có download → chạy ngay song song
                StartParallelDownloadAndImport(downloadPkgs, onAllDone: FinishBatch);
            }
            else
            {
                FinishBatch();
            }
        }

        /// <summary>
        /// Nhắc menu Ensure GameAnalytics runtime asmdef (<see cref="GameUpDefineSymbolsAutoSync"/>).
        /// </summary>
        /// <param name="fromMediationInstallAllBatch">true khi vừa xong &quot;Cài tất cả&quot; Mediation; false khi vừa import xong GA (Cài pack).</param>
        private static void NotifyGameAnalyticsAsmdefHint(bool fromMediationInstallAllBatch)
        {
            EditorApplication.delayCall += () =>
            {
                const string menuItem = "GameUp SDK → Ensure GameAnalytics runtime asmdef";
                string intro = fromMediationInstallAllBatch
                    ? "Cài package (bộ Mediation) đã xong. "
                    : "Game Analytics SDK vừa import xong. ";
                Debug.Log(
                    "[GameUp] " + intro + "Để tạo/đảm bảo asmdef GA, chọn menu: " + menuItem + ".");

                foreach (var w in Resources.FindObjectsOfTypeAll<GameUpDependenciesWindow>())
                {
                    if (w == null)
                        continue;
                    w.ShowNotification(
                        new GUIContent("Game Analytics (asmdef): menu " + menuItem));
                }
            };
        }

        /// <summary>
        /// Cài một package — dùng chung <see cref="StartBatchInstall"/> với scope một phần tử.
        /// </summary>
        private void StartSinglePackageInstall(PackageDef pkg)
        {
            if (pkg == null || pkg.IsInstalled || !CanAutoInstall(pkg))
                return;
            if (IsInstallOrDownloadBusy())
                return;

            StartBatchInstall(new List<PackageDef> { pkg });
        }

        private void EnqueueGitInstall(PackageDef pkg)
        {
            _installQueue.Clear();
            _installQueue.Enqueue(pkg);
            ProcessNextInQueue();
        }

        private Action _onQueueDone;

        private void ProcessNextInQueueThen(Action onDone)
        {
            _onQueueDone = onDone;
            ProcessNextInQueue();
        }

        private void ProcessNextInQueue()
        {
            if (_installQueue.Count == 0)
            {
                _currentInstallingPackage = null;
                _currentAddRequest = null;
                EditorApplication.update -= PollInstallQueue;

                var cb = _onQueueDone;
                _onQueueDone = null;
                if (cb != null) cb();
                else
                {
                    _isBatchInstalling = false;
                    _batchScope = null;
                    RefreshStatus();
                }

                return;
            }

            var pkg = _installQueue.Peek();
            _currentInstallingPackage = pkg;
            pkg.IsInstalling = true;
            Repaint();

            _currentAddRequest = Client.Add(pkg.GitUrl);
            EditorApplication.update += PollInstallQueue;
        }

        private void PollInstallQueue()
        {
            if (_currentAddRequest == null || !_currentAddRequest.IsCompleted) return;

            EditorApplication.update -= PollInstallQueue;

            var pkg = _currentInstallingPackage;
            if (pkg != null)
            {
                pkg.IsInstalling = false;

                if (_currentAddRequest.Status == StatusCode.Success)
                {
                    pkg.IsInstalled = true;
                    pkg.InstallError = null;
                }
                else
                {
                    pkg.InstallError = _currentAddRequest.Error?.message ?? "Cài thất bại.";
                }
            }

            _installQueue.Dequeue();
            _currentAddRequest = null;
            _currentInstallingPackage = null;

            ProcessNextInQueue();
            Repaint();
        }

        // ─── UnityPackage install ─────────────────────────────────────────────────

        /// <summary>
        /// Trả về danh sách đường dẫn tuyệt đối cho các file .unitypackage trong Packages~.
        /// Chỉ trả về file thực sự tồn tại. Trả về null nếu KHÔNG CÓ file nào.
        /// </summary>
        private static List<string> GetBundledPackagePaths(string[] fileNames)
        {
            if (fileNames == null || fileNames.Length == 0) return null;

            var found = new List<string>();
            foreach (string name in fileNames)
            {
                string normalized = name.Replace('/', Path.DirectorySeparatorChar);

                // 1) Packages~ (khi SDK cài dạng UPM package hoặc assets-based fallback)
                string packagesFolder = GetPackagesFolder();
                if (!string.IsNullOrEmpty(packagesFolder))
                {
                    string full = Path.Combine(packagesFolder, normalized);
                    if (File.Exists(full))
                    {
                        found.Add(full);
                        continue;
                    }
                }
            }

            return found.Count > 0 ? found : null;
        }

        // Backward compat helper dùng nội bộ để check có ít nhất 1 file
        private static string GetBundledPackagePath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            string folder = GetPackagesFolder();
            if (string.IsNullOrEmpty(folder)) return null;
            string full = Path.Combine(folder, fileName.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(full) ? full : null;
        }

        /// <summary>
        /// Tìm thư mục Packages~ của package này.
        /// Hỗ trợ cả cài via UPM Git URL (resolvedPath) và .unitypackage (Assets/GameUpSDK).
        /// </summary>
        private static string GetPackagesFolder()
        {
            // Thử tìm qua PackageInfo khi cài via UPM
            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                Type pkgInfoType = Type.GetType("UnityEditor.PackageManager.PackageInfo, UnityEditor");
                if (pkgInfoType != null)
                {
                    MethodInfo findMethod = pkgInfoType.GetMethod(
                        "FindForAssembly",
                        BindingFlags.Static | BindingFlags.Public,
                        null, new[] { typeof(System.Reflection.Assembly) }, null);

                    object info = findMethod?.Invoke(null, new object[] { asm });
                    if (info != null)
                    {
                        string resolved = pkgInfoType.GetProperty("resolvedPath")
                            ?.GetValue(info) as string;
                        if (!string.IsNullOrEmpty(resolved))
                            return Path.Combine(resolved, "Packages~");
                    }
                }
            }
            catch
            {
            }

            // Fallback: cài via .unitypackage → scripts nằm ở Assets/GameUpSDK
            return Path.Combine(Application.dataPath, "GameUpSDK", "Packages~");
        }

        /// <summary>Xóa asset/thư mục sau import .unitypackage (vd bỏ Facebook SDK Examples).</summary>
        private static void ApplyPostImportCleanup(PackageDef pkg)
        {
            if (pkg?.DeleteAssetPathsAfterImport == null || pkg.DeleteAssetPathsAfterImport.Length == 0)
                return;

            foreach (string assetPath in pkg.DeleteAssetPathsAfterImport)
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                    continue;

                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) == null
                    && !AssetDatabase.IsValidFolder(assetPath))
                    continue;

                if (!AssetDatabase.DeleteAsset(assetPath))
                    Debug.LogWarning($"[GameUpSDK] Không xóa được sau import: {assetPath}");
            }
        }

        /// <summary>
        /// Import tất cả file .unitypackage của một package.
        /// interactive=false để không hiện dialog xác nhận cho từng file.
        /// </summary>
        private void ImportUnityPackage(PackageDef pkg, List<string> filePaths)
        {
            pkg.IsInstalling = true;
            pkg.InstallError = null;
            Repaint();

            var errors = new List<string>();
            foreach (string path in filePaths)
            {
                try
                {
                    AssetDatabase.ImportPackage(path, interactive: false);
                    Debug.Log($"[GameUpSDK] Imported: {Path.GetFileName(path)}");
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
                    Debug.LogError($"[GameUpSDK] Import {Path.GetFileName(path)} thất bại: {ex.Message}");
                }
            }

            ApplyPostImportCleanup(pkg);

            // Ép Unity re-scan assets/dll sau khi import để giảm độ trễ load assemblies.
            // (ImportPackage chạy async; refresh thêm nhịp sau giúp state ổn định nhanh hơn.)
            AssetDatabase.Refresh();
            EditorApplication.delayCall += AssetDatabase.Refresh;

            pkg.IsInstalling = false;
            if (errors.Count == 0)
            {
                pkg.IsInstalled = true;
                pkg.InstallError = null;

                // GA .unitypackage không kèm asmdef → pass compile đầu sẽ lỗi thiếu assembly GameAnalyticsSDK.
                // Tạo asmdef ngay sau import (cần đã có GameAnalytics.cs trên disk — không thể tạo trước khi import).
                if (IsGameAnalyticsSdkPackage(pkg))
                {
                    if (GameUpDefineSymbolsAutoSync.TryEnsureGameAnalyticsRuntimeAsmdef(
                            out string asmdefMsg, out bool createdAsmdef))
                    {
                        if (createdAsmdef)
                            Debug.Log("[GameUp] " + asmdefMsg);
                    }
                    else
                    {
                        Debug.LogWarning("[GameUp] " + asmdefMsg);
                        if (!_gameAnalyticsSetupHintAfterBatch)
                            NotifyGameAnalyticsAsmdefHint(fromMediationInstallAllBatch: false);
                    }
                }
            }
            else
            {
                pkg.InstallError = "Một số file import thất bại:\n" + string.Join("\n", errors);
            }

            Repaint();
        }

        // ─── Parallel Download & Import ───────────────────────────────────────────

        /// <summary>Bắt đầu download song song + import một package đơn lẻ.</summary>
        private void StartDownloadAndImport(PackageDef pkg)
        {
            StartParallelDownloadAndImport(new List<PackageDef> { pkg }, onAllDone: null);
        }

        /// <summary>
        /// Tải tất cả file của tất cả packages cùng lúc (parallel).
        /// Khi toàn bộ download xong → import từng package theo nhóm → gọi onAllDone.
        /// </summary>
        private void StartParallelDownloadAndImport(List<PackageDef> pkgs, Action onAllDone)
        {
            if (_parallelTasks != null)
            {
                // Đang có download chạy, dừng lại
                foreach (var old in _parallelTasks) old.Request?.Dispose();
                EditorApplication.update -= PollParallelDownloads;
            }

            _parallelTasks = new List<DownloadTask>();
            _parallelDoneCallback = onAllDone;

            foreach (var pkg in pkgs)
            {
                if (pkg.HostedUrls == null || pkg.HostedUrls.Length == 0) continue;

                pkg.IsInstalling = true;
                pkg.InstallError = null;

                for (int i = 0; i < pkg.HostedUrls.Length; i++)
                {
                    string url = pkg.HostedUrls[i];
                    string fileName = pkg.BundledFileNames != null && i < pkg.BundledFileNames.Length
                        ? Path.GetFileName(pkg.BundledFileNames[i])
                        : $"{i}.unitypackage";
                    string tempPath = Path.Combine(Application.temporaryCachePath, fileName);

                    var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
                    req.downloadHandler = new DownloadHandlerFile(tempPath) { removeFileOnAbort = true };
                    req.SendWebRequest();

                    _parallelTasks.Add(new DownloadTask
                    {
                        Pkg = pkg,
                        FileName = fileName,
                        TempPath = tempPath,
                        Request = req,
                    });
                }
            }

            if (_parallelTasks.Count == 0)
            {
                _parallelTasks = null;
                onAllDone?.Invoke();
                return;
            }

            EditorApplication.update += PollParallelDownloads;
            Repaint();
        }

        private void PollParallelDownloads()
        {
            if (_parallelTasks == null) return;

            bool anyRunning = false;
            foreach (var task in _parallelTasks)
            {
                if (task.IsDone) continue;
                if (!task.Request.isDone)
                {
                    anyRunning = true;
                    continue;
                }

                // Request hoàn thành
                task.IsDone = true;
                if (task.Request.result != UnityWebRequest.Result.Success)
                {
                    task.HasError = true;
                    task.ErrorMessage = task.Request.error;
                }

                task.Request.Dispose();
                task.Request = null;
            }

            // Cập nhật overall progress
            float totalProgress = _parallelTasks.Sum(t =>
                t.IsDone ? 1f : t.Request?.downloadProgress ?? 0f);
            _downloadProgress = totalProgress / _parallelTasks.Count;
            int doneCount = _parallelTasks.Count(t => t.IsDone);
            _downloadStatus = $"Đang tải: {doneCount}/{_parallelTasks.Count} files";
            Repaint();

            if (anyRunning) return;

            // ─── Tất cả done → import theo nhóm package ───────────────────────
            EditorApplication.update -= PollParallelDownloads;

            // Group tasks by package
            var byPkg = _parallelTasks
                .GroupBy(t => t.Pkg)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (PackageDef pkg in OrderedInstallSequence(byPkg.Keys))
            {
                List<DownloadTask> tasks = byPkg[pkg];
                var successPaths = tasks.Where(t => !t.HasError).Select(t => t.TempPath).ToList();
                var errorMsgs = tasks.Where(t => t.HasError)
                    .Select(t => $"{t.FileName}: {t.ErrorMessage}").ToList();

                pkg.IsInstalling = false;
                if (errorMsgs.Count > 0)
                    pkg.InstallError = "Download thất bại:\n" + string.Join("\n", errorMsgs);

                if (successPaths.Count > 0)
                    ImportUnityPackage(pkg, successPaths);
            }

            _parallelTasks = null;
            _downloadProgress = 0;
            _downloadStatus = null;

            var cb = _parallelDoneCallback;
            _parallelDoneCallback = null;
            cb?.Invoke();
        }

        private void AddScopedRegistryAndPackage(PackageDef pkg)
        {
            // Đọc manifest.json, thêm scoped registry + dependency, ghi lại
            string manifestPath = System.IO.Path.Combine(
                Application.dataPath, "..", "Packages", "manifest.json");

            try
            {
                string json = System.IO.File.ReadAllText(manifestPath);
                var manifest = SimpleJsonHelper.ParseObject(json);

                // Thêm scoped registry nếu chưa có
                if (!string.IsNullOrEmpty(pkg.RegistryUrl))
                {
                    if (!manifest.ContainsKey("scopedRegistries"))
                        manifest["scopedRegistries"] = new List<object>();

                    var registries = (List<object>)manifest["scopedRegistries"];
                    bool found = registries.OfType<Dictionary<string, object>>()
                        .Any(r => r.TryGetValue("url", out var u) && u?.ToString() == pkg.RegistryUrl);

                    if (!found)
                    {
                        registries.Add(new Dictionary<string, object>
                        {
                            ["name"] = pkg.RegistryName,
                            ["url"] = pkg.RegistryUrl,
                            ["scopes"] = pkg.RegistryScopes?.ToList<object>() ?? new List<object>(),
                        });
                    }
                }

                // Thêm dependency
                if (!manifest.ContainsKey("dependencies"))
                    manifest["dependencies"] = new Dictionary<string, object>();

                var deps = (Dictionary<string, object>)manifest["dependencies"];
                if (!deps.ContainsKey(pkg.PackageId))
                    deps[pkg.PackageId] = "latest";

                System.IO.File.WriteAllText(manifestPath, SimpleJsonHelper.Serialize(manifest));
                AssetDatabase.Refresh();

                pkg.IsInstalling = false;
                pkg.IsInstalled = true;
            }
            catch (Exception ex)
            {
                pkg.IsInstalling = false;
                pkg.InstallError = "Lỗi khi sửa manifest.json: " + ex.Message;
            }

            Repaint();
        }

        // ─── Scripting Define Symbol management ──────────────────────────────────

        private static readonly BuildTargetGroup[] s_buildTargetGroups =
        {
            BuildTargetGroup.Android,
            BuildTargetGroup.iOS,
            BuildTargetGroup.Standalone,
        };

        /// <summary>
        /// Thêm hoặc xóa define GAMEUP_SDK_DEPS_READY khỏi Player Settings.
        /// Khi define này tồn tại, GameUpSDK.Runtime và GameUpSDK.Editor sẽ được compile.
        /// </summary>
        internal static void SetDepsReadyDefine(bool enabled)
        {
            foreach (var group in s_buildTargetGroups)
            {
                try
                {
                    string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                    var list = new List<string>(
                        current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

                    bool changed = false;
                    if (enabled && !list.Contains(GUDefinetion.DepsReadyDefine))
                    {
                        list.Add(GUDefinetion.DepsReadyDefine);
                        changed = true;
                    }
                    else if (!enabled && list.Remove(GUDefinetion.DepsReadyDefine))
                    {
                        changed = true;
                    }

                    if (changed)
                        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", list));
                }
                catch
                {
                    /* group không tồn tại trong project này, bỏ qua */
                }
            }
        }

        internal static bool IsDepsReadyDefined()
        {
            string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            return current.Contains(GUDefinetion.DepsReadyDefine);
        }

        // ─── Status refresh ───────────────────────────────────────────────────────

        private void RefreshStatus()
        {
            EnsurePrimaryMediationDefines();

            foreach (var pkg in s_packages)
            {
                pkg.IsInstalled = IsPackageInstalled(pkg);
                pkg.IsInstalling = false;
                pkg.InstallError = null;
            }

            // Auto set/clear Facebook define (Editor assembly = SDK đã import)
            bool facebookInstalled = IsAssemblyLoaded("Facebook.Unity.Editor");
            if (facebookInstalled && !HasDefine(FacebookDepsDefine))
                SetDefine(FacebookDepsDefine, true);
            else if (!facebookInstalled && HasDefine(FacebookDepsDefine))
                SetDefine(FacebookDepsDefine, false);

            // Auto set/clear LevelPlay define theo trạng thái package
            bool levelPlayInstalled = IsAssemblyLoaded("Unity.LevelPlay");
            if (levelPlayInstalled && !HasDefine(LevelPlayDepsDefine))
                SetDefine(LevelPlayDepsDefine, true);
            else if (!levelPlayInstalled && HasDefine(LevelPlayDepsDefine))
                SetDefine(LevelPlayDepsDefine, false);

            // Auto set/clear AdMob define theo trạng thái package
            bool admobInstalled = IsAssemblyLoaded("GoogleMobileAds");
            if (admobInstalled && !HasDefine(AdMobDepsDefine))
                SetDefine(AdMobDepsDefine, true);
            else if (!admobInstalled && HasDefine(AdMobDepsDefine))
                SetDefine(AdMobDepsDefine, false);

            // Auto set/clear Firebase define theo trạng thái package
            bool firebaseInstalled = IsAssemblyLoaded("Firebase.App");
            if (firebaseInstalled && !HasDefine(FirebaseDepsDefine))
                SetDefine(FirebaseDepsDefine, true);
            else if (!firebaseInstalled && HasDefine(FirebaseDepsDefine))
                SetDefine(FirebaseDepsDefine, false);

            // Auto set/clear AppsFlyer define theo trạng thái package
            bool appsFlyerInstalled = IsAssemblyLoaded("AppsFlyer");
            if (appsFlyerInstalled && !HasDefine(AppsFlyerDepsDefine))
                SetDefine(AppsFlyerDepsDefine, true);
            else if (!appsFlyerInstalled && HasDefine(AppsFlyerDepsDefine))
                SetDefine(AppsFlyerDepsDefine, false);

            // GameAnalytics: UPM (assembly GameAnalyticsSDK) hoặc .unitypackage cổ điển (type trong Assembly-CSharp)
            bool gameAnalyticsInstalled = IsGameAnalyticsSdkPresent();
            if (gameAnalyticsInstalled && !HasDefine(GameAnalyticsDepsDefine))
                SetDefine(GameAnalyticsDepsDefine, true);
            else if (!gameAnalyticsInstalled && HasDefine(GameAnalyticsDepsDefine))
                SetDefine(GameAnalyticsDepsDefine, false);

            // Tự động set/clear define khi trạng thái thay đổi
            // GAMEUP_SDK_DEPS_READY chỉ còn ý nghĩa "SDK enabled" (backward compat).
            // Bật khi có (Firebase hoặc AppsFlyer hoặc GameAnalytics) AND (AdMob hoặc LevelPlay).
            // Không dùng define này để include SDK bên thứ 3 nữa.
            bool hasAnalytics = firebaseInstalled || appsFlyerInstalled || gameAnalyticsInstalled;
            bool hasMediation = admobInstalled || levelPlayInstalled;
            bool sdkEnabled = hasAnalytics && hasMediation;
            if (sdkEnabled && !IsDepsReadyDefined())
                SetDepsReadyDefine(true);
            else if (!sdkEnabled && IsDepsReadyDefined())
                SetDepsReadyDefine(false);

            Repaint();
        }

        private static bool CanAutoInstall(PackageDef p)
        {
            if (p.Method == InstallMethod.GitUrl || p.Method == InstallMethod.ScopedRegistry)
                return true;
            if (p.Method == InstallMethod.UnityPackage)
                return GetBundledPackagePaths(p.BundledFileNames) != null
                       || (p.HostedUrls?.Length > 0);
            return false;
        }

        private static bool IsAssemblyLoaded(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName)) return false;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(asm.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsPackageInstalled(PackageDef pkg)
        {
            bool byAssembly = !string.IsNullOrEmpty(pkg.AssemblyName) && IsAssemblyLoaded(pkg.AssemblyName);
            bool byType = !string.IsNullOrEmpty(pkg.InstalledTypeFullName) &&
                          IsTypeInAnyLoadedAssembly(pkg.InstalledTypeFullName);
            if (!string.IsNullOrEmpty(pkg.InstalledTypeFullName))
                return byAssembly || byType;
            return byAssembly;
        }

        /// <summary>UPM có .asmdef GameAnalyticsSDK; .unitypackage chuẩn GA nằm trong Assembly-CSharp.</summary>
        internal static bool IsGameAnalyticsSdkPresent()
        {
            return IsAssemblyLoaded("GameAnalyticsSDK") ||
                   IsTypeInAnyLoadedAssembly("GameAnalyticsSDK.GameAnalytics");
        }

        private static bool IsTypeInAnyLoadedAssembly(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return false;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetType(fullTypeName, throwOnError: false, ignoreCase: false) != null)
                        return true;
                }
                catch
                {
                    /* một số dynamic assembly */
                }
            }

            return false;
        }
    }

    // ─── Minimal JSON helper (không dùng Newtonsoft/JsonUtility để giữ assembly sạch) ───

    internal static class SimpleJsonHelper
    {
        public static Dictionary<string, object> ParseObject(string json)
        {
            // Dùng Unity built-in JsonUtility không hỗ trợ Dictionary,
            // nên parse thủ công phần dependencies/scopedRegistries cần thiết.
            // Thực tế: dùng regex-free approach với index tracking.
            json = json.Trim();
            int idx = 0;
            return (Dictionary<string, object>)ParseValue(json, ref idx);
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) return null;

            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't')
            {
                i += 4;
                return true;
            }

            if (c == 'f')
            {
                i += 5;
                return false;
            }

            if (c == 'n')
            {
                i += 4;
                return null;
            }

            return ParseNumber(s, ref i);
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var dict = new Dictionary<string, object>();
            i++; // skip '{'
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}')
            {
                i++;
                return dict;
            }

            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                i++; // skip ':'
                object val = ParseValue(s, ref i);
                dict[key] = val;
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }

                if (i < s.Length && s[i] == '}')
                {
                    i++;
                    break;
                }
            }

            return dict;
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // skip '['
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']')
            {
                i++;
                return list;
            }

            while (i < s.Length)
            {
                list.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }

                if (i < s.Length && s[i] == ']')
                {
                    i++;
                    break;
                }
            }

            return list;
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // skip opening '"'
            var sb = new System.Text.StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\' && i < s.Length)
                {
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(esc); break;
                    }
                }
                else sb.Append(c);
            }

            return sb.ToString();
        }

        private static object ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '.' || s[i] == 'e' || s[i] == 'E' ||
                                    s[i] == '+'))
                i++;
            string num = s.Substring(start, i - start);
            if (int.TryParse(num, out int iv)) return iv;
            if (double.TryParse(num, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double dv)) return dv;
            return num;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        public static string Serialize(object obj, int indent = 0)
        {
            string pad = new string(' ', indent * 2);
            string pad1 = new string(' ', (indent + 1) * 2);

            switch (obj)
            {
                case null: return "null";
                case bool b: return b ? "true" : "false";
                case int iv: return iv.ToString();
                case long lv: return lv.ToString();
                case double dv:
                    return dv.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case string sv:
                    return "\"" + sv.Replace("\\", "\\\\").Replace("\"", "\\\"")
                        .Replace("\n", "\\n").Replace("\r", "\\r")
                        .Replace("\t", "\\t") + "\"";

                case Dictionary<string, object> dict:
                    {
                        if (dict.Count == 0) return "{}";
                        var lines = dict.Select(
                            kv => pad1 + "\"" + kv.Key + "\": " + Serialize(kv.Value, indent + 1));
                        return "{\n" + string.Join(",\n", lines) + "\n" + pad + "}";
                    }

                case List<object> list:
                    {
                        if (list.Count == 0) return "[]";
                        var lines = list.Select(item => pad1 + Serialize(item, indent + 1));
                        return "[\n" + string.Join(",\n", lines) + "\n" + pad + "]";
                    }

                default:
                    return "\"" + obj.ToString() + "\"";
            }
        }
    }
}