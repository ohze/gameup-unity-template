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

        private const string LevelPlayDepsDefine = GUDefinetion.LevelPlayDepsInstalled;
        private const string AdMobDepsDefine = GUDefinetion.AdMobDepsInstalled;
        private const string MaxSdkDepsDefine = GUDefinetion.MaxDepsInstalled;
        private const string FirebaseDepsDefine = GUDefinetion.FirebaseDepsInstalled;
        private const string AppsFlyerDepsDefine = GUDefinetion.AppsFlyerDepsInstalled;
        private const string GameAnalyticsDepsDefine = GUDefinetion.GameAnalyticsDepsInstalled;
        private const string FacebookDepsDefine = GUDefinetion.FacebookDepsInstalled;
        private const string AppmetricaDepsDefine = GUDefinetion.AppMetricaDepsInstalled;

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
            // Installer đang gỡ pack: define đã được clear có chủ đích, sync lúc này sẽ set lại chúng.
            if (GameUpDependenciesWindow.IsDependencyRemovalInProgress)
                return;

            TryEnsureGameAnalyticsRuntimeAsmdef(out _, out _);
            EnsurePrimaryMediationDefines();

            // Dựa vào asset trên disk, không dựa vào AppDomain: assembly của SDK vừa gỡ vẫn còn load
            // cho tới lần domain reload kế tiếp, nên IsAssemblyLoaded sẽ bật lại define vừa clear.
            bool levelPlayInstalled = IsDependencyInstalled("Unity.LevelPlay");
            bool admobInstalled = IsDependencyInstalled("GoogleMobileAds");
            bool maxInstalled = IsDependencyInstalled("MaxSdk.Scripts");
            bool firebaseInstalled = IsDependencyInstalled("Firebase.App");
            bool appsFlyerInstalled = IsDependencyInstalled("AppsFlyer");
            bool gameAnalyticsInstalled = IsDependencyInstalled("GameAnalyticsSDK");
            bool facebookInstalled = IsDependencyInstalled("Facebook.Unity.Editor");
            bool appMetricaInstalled = IsDependencyInstalled("AppMetrica");

            SetDefine(LevelPlayDepsDefine, levelPlayInstalled);
            SetDefine(AdMobDepsDefine, admobInstalled);
            SetDefine(MaxSdkDepsDefine, maxInstalled);
            SetDefine(FirebaseDepsDefine, firebaseInstalled);
            SetDefine(AppsFlyerDepsDefine, appsFlyerInstalled);
            SetDefine(GameAnalyticsDepsDefine, gameAnalyticsInstalled);
            SetDefine(FacebookDepsDefine, facebookInstalled);
            SetDefine(AppmetricaDepsDefine, appMetricaInstalled);

            bool hasAnalytics = firebaseInstalled || appsFlyerInstalled || gameAnalyticsInstalled || appMetricaInstalled;
            bool hasMediation = admobInstalled || levelPlayInstalled || maxInstalled;
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
                "Đã tạo " + GameAnalyticsRuntimeAsmdefAssetPath + ". GameUp.SDK.Runtime tham chiếu assembly tên GameAnalyticsSDK — đợi Unity recompile.";
            return true;
        }

        /// <summary>Đảm bảo có đúng một define mediation (mặc định AdMob nếu chưa có).</summary>
        private static void EnsurePrimaryMediationDefines()
        {
            bool lp = HasDefine(GUDefinetion.PrimaryMediationLevelPlay);
            bool admob = HasDefine(GUDefinetion.PrimaryMediationAdMob);
            bool max = HasDefine(GUDefinetion.PrimaryMediationMax);
            int active = (lp ? 1 : 0) + (admob ? 1 : 0) + (max ? 1 : 0);
            if (active == 0)
            {
                SetDefine(GUDefinetion.PrimaryMediationAdMob, true);
                return;
            }

            if (active <= 1)
                return;

            if (admob)
            {
                SetDefine(GUDefinetion.PrimaryMediationLevelPlay, false);
                SetDefine(GUDefinetion.PrimaryMediationMax, false);
            }
            else if (max)
            {
                SetDefine(GUDefinetion.PrimaryMediationLevelPlay, false);
                SetDefine(GUDefinetion.PrimaryMediationAdMob, false);
            }
            else
            {
                SetDefine(GUDefinetion.PrimaryMediationAdMob, false);
                SetDefine(GUDefinetion.PrimaryMediationMax, false);
            }
        }

        private static bool IsDependencyInstalled(string assemblyName)
        {
            return GameUpDependenciesWindow.IsDependencyInstalledByAssembly(assemblyName);
        }

        private static bool HasDefine(string define)
        {
            if (string.IsNullOrEmpty(define))
                return false;

            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            // So khớp từng symbol thay vì Contains: tránh khớp nhầm khi define là chuỗi con của define khác.
            foreach (string symbol in symbols.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (symbol.Trim().Equals(define, StringComparison.Ordinal))
                    return true;
            }

            return false;
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

                    bool changed;
                    if (enabled)
                    {
                        changed = !list.Contains(define);
                        if (changed)
                            list.Add(define);
                    }
                    else
                    {
                        changed = list.RemoveAll(s => s.Trim().Equals(define, StringComparison.Ordinal)) > 0;
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

