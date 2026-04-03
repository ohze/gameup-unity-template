using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEngine;

namespace GameUp.SDK.Installer
{
    /// <summary>
    /// Auto-sync Scripting Define Symbols dựa trên các assemblies đang có trong project.
    /// Mục tiêu: sau khi pull/update từ git hoặc import deps thủ công, symbols vẫn tự cập nhật
    /// mà không cần mở GameUpDependenciesWindow.
    /// </summary>
    [InitializeOnLoad]
    internal static class GameUpDefineSymbolsAutoSync
    {
        private static readonly BuildTargetGroup[] BuildTargetGroups =
        {
            BuildTargetGroup.Android,
            BuildTargetGroup.iOS,
            BuildTargetGroup.Standalone,
        };

        private const string LevelPlayDepsDefine = "LEVELPLAY_DEPENDENCIES_INSTALLED";
        private const string AdMobDepsDefine = "ADMOB_DEPENDENCIES_INSTALLED";
        private const string FirebaseDepsDefine = "FIREBASE_DEPENDENCIES_INSTALLED";
        private const string AppsFlyerDepsDefine = "APPSFLYER_DEPENDENCIES_INSTALLED";
        private const string GameAnalyticsDepsDefine = "GAMEANALYTICS_DEPENDENCIES_INSTALLED";
        private const string FacebookDepsDefine = "FACEBOOK_DEPENDENCIES_INSTALLED";

        private const string SessionThrottleKey = "GameUpSDK_DefinesAutoSync_Throttled";

        private const string GameAnalyticsRuntimeAsmdefAssetPath = "Assets/GameAnalytics/Plugins/GameAnalyticsSDK.asmdef";
        private const string GameAnalyticsMarkerScriptPath = "Assets/GameAnalytics/Plugins/Scripts/GameAnalytics.cs";

        private const string GameAnalyticsRuntimeAsmdefJson =
            "{\n" +
            "    \"name\": \"GameAnalyticsSDK\",\n" +
            "    \"rootNamespace\": \"GameAnalyticsSDK\",\n" +
            "    \"references\": [],\n" +
            "    \"includePlatforms\": [],\n" +
            "    \"excludePlatforms\": [],\n" +
            "    \"allowUnsafeCode\": false,\n" +
            "    \"overrideReferences\": false,\n" +
            "    \"precompiledReferences\": [],\n" +
            "    \"autoReferenced\": true,\n" +
            "    \"defineConstraints\": [],\n" +
            "    \"versionDefines\": [],\n" +
            "    \"noEngineReferences\": false\n" +
            "}\n";

        static GameUpDefineSymbolsAutoSync()
        {
            // Unity load → schedule 1 lần (đợi domain ổn định)
            EditorApplication.delayCall += TrySyncSoon;

            // Khi compile xong (import package / pull git thường gây recompile)
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            // Khi UPM packages thay đổi (nếu deps được cài bằng UPM)
            Events.registeredPackages -= OnRegisteredPackages;
            Events.registeredPackages += OnRegisteredPackages;
        }

        [MenuItem("GameUp/SDK/Ensure GameAnalytics runtime asmdef", priority = 23)]
        private static void MenuEnsureGameAnalyticsAsmdef()
        {
            if (TryEnsureGameAnalyticsRuntimeAsmdef(out string message, out _))
                Debug.Log("[GameUp] " + message);
            else
                Debug.LogWarning("[GameUp] " + message);
        }

        [MenuItem("GameUp/SDK/Sync Define Symbols", priority = 21)]
        private static void MenuSyncNow()
        {
            try
            {
                SyncDefines();
                Debug.Log("[GameUp] Sync Define Symbols: done.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameUp] Sync Define Symbols failed: " + e);
            }
        }

        private static void OnCompilationFinished(object _)
        {
            TrySyncSoon();
        }

        private static void OnRegisteredPackages(PackageRegistrationEventArgs _)
        {
            TrySyncSoon();
        }

        private static void TrySyncSoon()
        {
            // Throttle trong cùng session để tránh loop khi SetDefine trigger recompile.
            if (SessionState.GetBool(SessionThrottleKey, false))
                return;

            SessionState.SetBool(SessionThrottleKey, true);
            EditorApplication.delayCall += () =>
            {
                // Cho phép chạy lại sau 1 nhịp nếu có sự kiện tiếp theo
                SessionState.SetBool(SessionThrottleKey, false);
                if (EditorApplication.isCompiling)
                {
                    // Nếu vẫn đang compile, thử lại ở tick sau.
                    EditorApplication.delayCall += TrySyncSoon;
                    return;
                }

                try
                {
                    SyncDefines();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[GameUp] Auto-sync define symbols failed: " + e.Message);
                }
            };
        }

        private static void SyncDefines()
        {
            TryEnsureGameAnalyticsRuntimeAsmdef(out _, out _);
            EnsurePrimaryMediationDefines();

            bool levelPlayInstalled = IsAssemblyLoaded("Unity.LevelPlay");
            bool admobInstalled = IsAssemblyLoaded("GoogleMobileAds");
            bool firebaseInstalled = IsAssemblyLoaded("Firebase.App");
            bool appsFlyerInstalled = IsAssemblyLoaded("AppsFlyer");
            bool gameAnalyticsInstalled = GameUpDependenciesWindow.IsGameAnalyticsSdkPresent();
            bool facebookInstalled = IsAssemblyLoaded("Facebook.Unity.Editor");

            SetDefine(LevelPlayDepsDefine, levelPlayInstalled);
            SetDefine(AdMobDepsDefine, admobInstalled);
            SetDefine(FirebaseDepsDefine, firebaseInstalled);
            SetDefine(AppsFlyerDepsDefine, appsFlyerInstalled);
            SetDefine(GameAnalyticsDepsDefine, gameAnalyticsInstalled);
            SetDefine(FacebookDepsDefine, facebookInstalled);

            // Backward compat: bật khi có (Firebase hoặc AppsFlyer hoặc GameAnalytics) AND (AdMob hoặc LevelPlay)
            bool hasAnalytics = firebaseInstalled || appsFlyerInstalled || gameAnalyticsInstalled;
            bool hasMediation = admobInstalled || levelPlayInstalled;
            bool sdkEnabled = hasAnalytics && hasMediation;
            GameUpDependenciesWindow.SetDepsReadyDefine(sdkEnabled);
        }

        /// <summary>
        /// Tạo <c>GameAnalyticsSDK.asmdef</c> tại <c>Assets/GameAnalytics/Plugins/</c> khi đã có script GA chuẩn nhưng thiếu asmdef (thường gặp với .unitypackage cũ).
        /// Gọi ngay sau <c>AssetDatabase.ImportPackage</c> GA + <c>Refresh</c> để tránh pass compile đầu lỗi thiếu assembly <c>GameAnalyticsSDK</c>.
        /// </summary>
        internal static bool TryEnsureGameAnalyticsRuntimeAsmdef(out string message, out bool createdNewAsmdef)
        {
            createdNewAsmdef = false;
            message = null;
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
            {
                message = "Ensure GameAnalytics asmdef: Application.dataPath is empty.";
                return false;
            }

            string markerFull = Path.Combine(dataPath, "GameAnalytics", "Plugins", "Scripts", "GameAnalytics.cs");
            if (!File.Exists(markerFull))
            {
                message =
                    "Ensure GameAnalytics asmdef: không thấy " + GameAnalyticsMarkerScriptPath + ". Import GameAnalytics SDK hoặc layout tương tự.";
                return false;
            }

            string asmdefFull = Path.Combine(dataPath, "GameAnalytics", "Plugins", "GameAnalyticsSDK.asmdef");
            if (File.Exists(asmdefFull))
            {
                message = "GameAnalytics runtime asmdef đã tồn tại: " + GameAnalyticsRuntimeAsmdefAssetPath;
                return true;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(asmdefFull) ?? "");
            File.WriteAllText(asmdefFull, GameAnalyticsRuntimeAsmdefJson);
            AssetDatabase.ImportAsset(GameAnalyticsRuntimeAsmdefAssetPath, ImportAssetOptions.ForceUpdate);
            createdNewAsmdef = true;
            message =
                "Đã tạo " + GameAnalyticsRuntimeAsmdefAssetPath + ". GameUpSDK.Runtime tham chiếu assembly tên GameAnalyticsSDK — đợi Unity recompile.";
            return true;
        }

        private static void EnsurePrimaryMediationDefines()
        {
            bool lp = HasDefine(GUDefinetion.PrimaryMediationLevelPlay);
            bool admob = HasDefine(GUDefinetion.PrimaryMediationAdMob);
            if (!lp && !admob)
            {
                SetDefine(GUDefinetion.PrimaryMediationLevelPlay, true);
                return;
            }

            // Nếu lỡ có cả 2, ưu tiên giữ AdMob (giống logic window).
            if (lp && admob)
                SetDefine(GUDefinetion.PrimaryMediationLevelPlay, false);
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

        private static bool HasDefine(string define)
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            return !string.IsNullOrEmpty(symbols) && symbols.Contains(define);
        }

        private static void SetDefine(string define, bool enabled)
        {
            foreach (var group in BuildTargetGroups)
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

                    if (!changed)
                        continue;

                    // Remove duplicates & normalize order for stability across machines.
                    var normalized = list
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim())
                        .Distinct()
                        .ToList();

                    PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", normalized));
                }
                catch
                {
                    // group không tồn tại trong project này, bỏ qua
                }
            }
        }
    }
}

