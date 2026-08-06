#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace GameUp.Core.Editor
{
    public static class GUDotweenDependencyUtility
    {
        public const string DotweenInstalledDefineSymbol = "DOTween__DEPENDENCIES_INSTALLED";
        public const string DotweenProDownloadUrl = "https://github.com/ohze/gameup-unity-template/releases/download/deps/DOTween.Pro.v1.0.381.unitypackage";
        public const string DotweenFolderPath = "Assets/Plugins/Demigiant/DOTween";
        public const string DotweenModulesAsmdefPath = "Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef";
        public const string GameUpSdkGitUpmUrl = "https://github.com/ohze/gameup-unity-template.git?path=Assets/GameUpSDK";
        public const string GameUpSdkPackageName = "com.ohze.gameup.sdk";
        public const string GameUpIapGitUpmUrl = "https://github.com/ohze/gameup-unity-template.git?path=Assets/GameUpIAP";
        public const string GameUpIapPackageName = "com.ohze.gameup.iap";

        private static readonly NamedBuildTarget[] SupportTargets =
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS
        };

        public static bool CanUseCoreTools()
        {
            return IsDotweenDependencyInstalled();
        }

        public static bool IsDotweenDependencyInstalled()
        {
            return HasDotweenFolder() && HasDotweenModulesAsmdef() && HasDefineSymbolOnAllTargets();
        }

        public static bool HasDotweenFolder()
        {
            return AssetDatabase.IsValidFolder(DotweenFolderPath);
        }

        public static bool HasDotweenModulesAsmdef()
        {
            return File.Exists(DotweenModulesAsmdefPath);
        }

        public static bool HasDefineSymbolOnAllTargets()
        {
            return MissingDefineTargets().Count == 0;
        }

        /// <summary>Platform còn thiếu define — dùng để báo trạng thái chính xác thay vì chỉ true/false.</summary>
        public static List<string> MissingDefineTargets()
        {
            var missing = new List<string>();
            for (var index = 0; index < SupportTargets.Length; index++)
            {
                var target = SupportTargets[index];
                PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
                if (defines == null || !defines.Contains(DotweenInstalledDefineSymbol))
                    missing.Add(target.TargetName);
            }

            return missing;
        }

        public static bool EnableDefineSymbolOnAllTargets()
        {
            var changed = false;
            for (var index = 0; index < SupportTargets.Length; index++)
            {
                var target = SupportTargets[index];
                PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
                var defineList = defines?.ToList() ?? new List<string>();
                if (defineList.Contains(DotweenInstalledDefineSymbol))
                    continue;

                defineList.Add(DotweenInstalledDefineSymbol);
                PlayerSettings.SetScriptingDefineSymbols(target, defineList.ToArray());
                changed = true;
            }

            if (changed)
                AssetDatabase.Refresh();

            return changed;
        }

        public static bool DisableDefineSymbolOnAllTargets()
        {
            var changed = false;
            for (var index = 0; index < SupportTargets.Length; index++)
            {
                var target = SupportTargets[index];
                PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
                var defineList = defines?.ToList() ?? new List<string>();
                if (!defineList.Contains(DotweenInstalledDefineSymbol))
                    continue;

                defineList.Remove(DotweenInstalledDefineSymbol);
                PlayerSettings.SetScriptingDefineSymbols(target, defineList.ToArray());
                changed = true;
            }

            if (changed)
                AssetDatabase.Refresh();

            return changed;
        }

        public static bool OpenDotweenUtilityPanel()
        {
            // Hỗ trợ nhiều variant menu path của DOTween Utility Panel.
            return EditorApplication.ExecuteMenuItem("Tools/Demigiant/DOTween Utility Panel")
                   || EditorApplication.ExecuteMenuItem("Demigiant/DOTween Utility Panel");
        }

        public static bool CreateDotweenModulesAsmdefIfMissing()
        {
            if (HasDotweenModulesAsmdef())
                return true;

            if (!HasDotweenFolder())
                return false;

            var parentFolder = Path.GetDirectoryName(DotweenModulesAsmdefPath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(parentFolder))
                return false;

            if (!AssetDatabase.IsValidFolder(parentFolder))
                return false;

            var asmdefJson = "{\n    \"name\": \"DOTween.Modules\"\n}\n";
            File.WriteAllText(DotweenModulesAsmdefPath, asmdefJson);
            AssetDatabase.ImportAsset(DotweenModulesAsmdefPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh();
            return true;
        }

        // ─── Trạng thái package UPM ──────────────────────────────────────────

        /// <summary>
        /// Package cài qua Git UPM nằm trong Library/PackageCache chứ không phải Packages/&lt;name&gt;,
        /// nên phải hỏi AssetDatabase (Unity map mọi package vào path ảo "Packages/&lt;name&gt;")
        /// thay vì chỉ kiểm tra thư mục vật lý như trước.
        /// </summary>
        public static bool IsPackageInstalled(string packageName, string embeddedAssetsFolder)
        {
            if (!string.IsNullOrEmpty(embeddedAssetsFolder) && AssetDatabase.IsValidFolder(embeddedAssetsFolder))
                return true;

            if (string.IsNullOrEmpty(packageName)) return false;

            if (AssetDatabase.IsValidFolder("Packages/" + packageName))
                return true;

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot)) return false;

            return Directory.Exists(Path.Combine(projectRoot, "Packages", packageName));
        }

        /// <summary>Version của package đã cài (rỗng nếu không đọc được / package embedded trong Assets).</summary>
        public static string GetPackageVersion(string packageName)
        {
            if (string.IsNullOrEmpty(packageName)) return string.Empty;
            try
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + packageName);
                return info != null ? info.version : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool IsGameUpSdkInstalled() => IsPackageInstalled(GameUpSdkPackageName, "Assets/GameUpSDK");

        public static bool IsGameUpIapInstalled() => IsPackageInstalled(GameUpIapPackageName, "Assets/GameUpIAP");

        public static bool CanInstallGameUpIap()
        {
            return IsGameUpSdkInstalled() && GUProjectFolderSetupWindow.IsSetupCompleted();
        }
    }

    public sealed class GUDotweenDependencyInstallerWindow : EditorWindow
    {
        private const string MenuPath = "GameUp/Project/GameUpCore Installer";
        private const string WindowTitle = "GameUpCore Installer";
        private const string DotweenPackageFileName = "DOTween.Pro.v1.0.381.unitypackage";
        private const string LogDefineSymbol = "ENABLE_LOG";

        // Message được giữ qua domain reload (bật define / import package đều làm Unity recompile).
        private const string DotweenMessageKey = "GameUp.CoreInstaller.DotweenMessage";
        private const string SdkMessageKey = "GameUp.CoreInstaller.SdkMessage";
        private const string IapMessageKey = "GameUp.CoreInstaller.IapMessage";

        private AddRequest _gameUpSdkInstallRequest;
        private AddRequest _gameUpIapInstallRequest;
        private UnityWebRequest _dotweenDownloadRequest;
        private string _dotweenDownloadedPackagePath;
        private bool _dotweenIsInstalling;
        private Vector2 _scroll;

        private string DotweenMessage
        {
            get => SessionState.GetString(DotweenMessageKey, string.Empty);
            set => SessionState.SetString(DotweenMessageKey, value ?? string.Empty);
        }

        private string SdkMessage
        {
            get => SessionState.GetString(SdkMessageKey, string.Empty);
            set => SessionState.SetString(SdkMessageKey, value ?? string.Empty);
        }

        private string IapMessage
        {
            get => SessionState.GetString(IapMessageKey, string.Empty);
            set => SessionState.SetString(IapMessageKey, value ?? string.Empty);
        }

        [MenuItem(MenuPath)]
        private static void OpenWindow()
        {
            var window = GetWindow<GUDotweenDependencyInstallerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(560f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            // Trạng thái đọc từ project nên phải vẽ lại mỗi khi project/asset đổi,
            // nếu không cửa sổ vẫn hiện "CHƯA XONG" sau khi thao tác xong ở chỗ khác.
            EditorApplication.projectChanged += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= Repaint;

            if (_dotweenDownloadRequest != null)
            {
                _dotweenDownloadRequest.Abort();
                _dotweenDownloadRequest.Dispose();
                _dotweenDownloadRequest = null;
            }

            UnregisterDotweenImportCallbacks();
            _dotweenIsInstalling = false;
        }

        private void OnFocus() => Repaint();

        // ─── Trạng thái từng bước ────────────────────────────────────────────

        private bool DotweenReady => GUDotweenDependencyUtility.IsDotweenDependencyInstalled();
        private bool FolderSetupReady => GUProjectFolderSetupWindow.IsSetupCompleted();
        private static bool CoreSceneReady => GUCoreProjectSetup.HasCoreObjectsInScene();
        private bool DotweenBusy => _dotweenDownloadRequest != null || _dotweenIsInstalling;

        private static bool HasLogDefine()
        {
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android, out var defines);
            return defines != null && defines.Contains(LogDefineSymbol);
        }

        private int CompletedStepCount()
        {
            var done = 0;
            if (DotweenReady) done++;
            if (FolderSetupReady) done++;
            if (CoreSceneReady) done++;
            return done;
        }

        private void OnGUI()
        {
            GUInstallerUI.EnsureStyles();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            DrawStepDotween();
            DrawStepFolderSetup();
            DrawStepCoreScene();
            DrawOptionalSection();

            EditorGUILayout.Space(10);
            EditorGUILayout.EndScrollView();

            if (DotweenBusy
                || (_gameUpSdkInstallRequest != null && !_gameUpSdkInstallRequest.IsCompleted)
                || (_gameUpIapInstallRequest != null && !_gameUpIapInstallRequest.IsCompleted))
            {
                Repaint();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("GameUpCore Installer", EditorStyles.largeLabel);
            GUInstallerUI.Hint("Làm lần lượt 3 bước bắt buộc bên dưới. Mỗi bước tự kiểm tra trạng thái thật trong project, không dựa vào cờ đã bấm.");

            EditorGUILayout.Space(4);
            GUInstallerUI.ProgressBar("Hoàn tất", CompletedStepCount(), 3);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUInstallerUI.MiniButton("Kiểm tra lại", true, 100f))
            {
                AssetDatabase.Refresh();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        // ─── BƯỚC 1 — DOTween ────────────────────────────────────────────────

        private void DrawStepDotween()
        {
            var state = DotweenBusy ? GUSetupState.Busy : DotweenReady ? GUSetupState.Done : GUSetupState.Missing;

            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader("BƯỚC 1", "DOTween Pro + assembly DOTween.Modules", state);
                GUILayout.Label(
                    "GameUp.UI.Runtime reference assembly DOTween.Modules. Thiếu bước này thì mọi menu setup của GameUp đều bị khoá.",
                    GUInstallerUI.Desc);

                EditorGUILayout.Space(4);

                var hasFolder = GUDotweenDependencyUtility.HasDotweenFolder();
                if (GUInstallerUI.StatusRow(
                        "DOTween đã import",
                        hasFolder ? GUSetupState.Done : GUSetupState.Missing,
                        GUDotweenDependencyUtility.DotweenFolderPath,
                        hasFolder ? "Mở thư mục" : null))
                {
                    GUInstallerUI.PingPath(GUDotweenDependencyUtility.DotweenFolderPath);
                }

                var hasAsmdef = GUDotweenDependencyUtility.HasDotweenModulesAsmdef();
                if (GUInstallerUI.StatusRow(
                        "DOTween.Modules.asmdef",
                        hasAsmdef ? GUSetupState.Done : hasFolder ? GUSetupState.Missing : GUSetupState.Blocked,
                        hasAsmdef ? "đã có" : "cần tạo",
                        hasAsmdef ? null : "Tạo asmdef",
                        hasFolder))
                {
                    if (!GUDotweenDependencyUtility.CreateDotweenModulesAsmdefIfMissing())
                    {
                        EditorUtility.DisplayDialog(
                            "Không tạo được asmdef",
                            "Không tạo được DOTween.Modules.asmdef. Hãy mở DOTween Utility Panel và chạy Setup DOTween.",
                            "OK");
                    }
                }

                var missingDefines = GUDotweenDependencyUtility.MissingDefineTargets();
                var defineDetail = missingDefines.Count == 0
                    ? "Standalone, Android, iOS"
                    : "còn thiếu: " + string.Join(", ", missingDefines);
                if (GUInstallerUI.StatusRow(
                        $"Define {GUDotweenDependencyUtility.DotweenInstalledDefineSymbol}",
                        missingDefines.Count == 0 ? GUSetupState.Done : GUSetupState.Missing,
                        defineDetail,
                        missingDefines.Count == 0 ? "Gỡ define" : "Bật define"))
                {
                    if (missingDefines.Count == 0) GUDotweenDependencyUtility.DisableDefineSymbolOnAllTargets();
                    else GUDotweenDependencyUtility.EnableDefineSymbolOnAllTargets();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.Space(6);

                if (!DotweenReady)
                {
                    if (GUInstallerUI.PrimaryButton("Tải & cài tự động DOTween Pro v1.0.381", !DotweenBusy))
                    {
                        StartDotweenAutoInstall();
                    }
                }

                EditorGUILayout.BeginHorizontal();
                if (GUInstallerUI.MiniButton("Mở DOTween Utility Panel"))
                {
                    if (!GUDotweenDependencyUtility.OpenDotweenUtilityPanel())
                    {
                        EditorUtility.DisplayDialog(
                            "Không tìm thấy DOTween Utility Panel",
                            "Hãy cài DOTween trước, sau đó mở thủ công từ menu Tools/Demigiant.",
                            "OK");
                    }
                }

                if (GUInstallerUI.MiniButton("Mở trang tải trên trình duyệt"))
                {
                    Application.OpenURL(GUDotweenDependencyUtility.DotweenProDownloadUrl);
                }
                EditorGUILayout.EndHorizontal();

                DrawDotweenProgress();
            }
        }

        private void DrawDotweenProgress()
        {
            if (_dotweenDownloadRequest != null)
            {
                if (_dotweenDownloadRequest.isDone)
                {
                    CompleteDotweenDownload();
                }
                else
                {
                    var rect = GUILayoutUtility.GetRect(18f, 18f);
                    EditorGUI.ProgressBar(rect, _dotweenDownloadRequest.downloadProgress, "Đang tải DOTween Pro...");
                }
            }
            else if (_dotweenIsInstalling)
            {
                EditorGUILayout.HelpBox("Đang import DOTween vào project...", MessageType.Info);
            }

            DrawMessage(DotweenMessage);
        }

        // ─── BƯỚC 2 — Folder Setup ───────────────────────────────────────────

        private void DrawStepFolderSetup()
        {
            var blocked = !DotweenReady;
            var state = FolderSetupReady ? GUSetupState.Done : blocked ? GUSetupState.Blocked : GUSetupState.Missing;

            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader("BƯỚC 2", "Folder Setup (_MainProject + PopupData/ScreenData)", state);
                GUILayout.Label(
                    "Tạo cây thư mục chuẩn, PopupData/ScreenData và các group Addressables cơ bản.",
                    GUInstallerUI.Desc);

                if (blocked)
                {
                    GUInstallerUI.Hint("Menu Folder Setup chỉ bật sau khi bước 1 xong.");
                }

                EditorGUILayout.Space(4);
                var (existing, totalFolders, assetsOk, totalAssets) = GUProjectFolderSetupWindow.GetSetupProgress();
                GUInstallerUI.StatusRow("Thư mục bắt buộc", existing == totalFolders ? GUSetupState.Done : GUSetupState.Missing, $"{existing}/{totalFolders}");
                GUInstallerUI.StatusRow("ScriptableObject mặc định", assetsOk == totalAssets ? GUSetupState.Done : GUSetupState.Missing, $"{assetsOk}/{totalAssets}");

                EditorGUILayout.Space(4);
                if (GUInstallerUI.PrimaryButton(FolderSetupReady ? "Mở Folder Setup" : "Mở Folder Setup và tạo thư mục", !blocked, 28f))
                {
                    GUProjectFolderSetupWindow.OpenWindowFromInstaller();
                }

                DrawMessage(null);
            }
        }

        // ─── BƯỚC 3 — Core setup trong scene ─────────────────────────────────

        private void DrawStepCoreScene()
        {
            var blocked = !DotweenReady || !FolderSetupReady;
            var state = CoreSceneReady ? GUSetupState.Done : blocked ? GUSetupState.Blocked : GUSetupState.Missing;

            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader("BƯỚC 3", "Core setup — prefab Manager / UI vào scene", state);
                GUILayout.Label(
                    "Copy prefab Core/UI sang _MainProject rồi đặt ====Manager==== và =====UI===== vào scene đang mở.",
                    GUInstallerUI.Desc);

                if (blocked) GUInstallerUI.Hint("Cần xong bước 1 và bước 2 trước.");

                EditorGUILayout.Space(4);
                GUInstallerUI.StatusRow("Scene hiện tại có Manager + UI root", CoreSceneReady ? GUSetupState.Done : GUSetupState.Missing);

                EditorGUILayout.Space(4);
                if (GUInstallerUI.PrimaryButton("Chạy Core setup cho scene đang mở", !blocked, 28f))
                {
                    GUCoreProjectSetup.RunCoreSetup(true);
                    Repaint();
                }
            }
        }

        // ─── Tuỳ chọn ────────────────────────────────────────────────────────

        private void DrawOptionalSection()
        {
            GUInstallerUI.SectionHeader("TÙY CHỌN", "Cài thêm khi dự án cần.");

            DrawPackageCard(
                "GameUpSDK",
                "Quảng cáo, analytics, Firebase Remote Config.",
                GUDotweenDependencyUtility.GameUpSdkPackageName,
                GUDotweenDependencyUtility.GameUpSdkGitUpmUrl,
                GUDotweenDependencyUtility.IsGameUpSdkInstalled(),
                ref _gameUpSdkInstallRequest,
                SdkMessage,
                message => SdkMessage = message,
                null);

            var iapBlockedReason = !GUDotweenDependencyUtility.IsGameUpSdkInstalled()
                ? "Cần cài GameUpSDK trước."
                : !FolderSetupReady
                    ? "Cần hoàn tất bước 2 (Folder Setup)."
                    : null;

            DrawPackageCard(
                "GameUpIAP",
                "In-app purchase dựng sẵn trên nền GameUpSDK.",
                GUDotweenDependencyUtility.GameUpIapPackageName,
                GUDotweenDependencyUtility.GameUpIapGitUpmUrl,
                GUDotweenDependencyUtility.IsGameUpIapInstalled(),
                ref _gameUpIapInstallRequest,
                IapMessage,
                message => IapMessage = message,
                iapBlockedReason);

            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader(null, "Tiện ích khác", GUSetupState.Optional);

                if (GUInstallerUI.StatusRow(
                        "Helper packages (CoinFly, Tutorial...)",
                        GUSetupState.Optional,
                        null,
                        "Mở cửa sổ",
                        DotweenReady))
                {
                    EditorApplication.ExecuteMenuItem("GameUp/Project/Helper Package Installer");
                }

                var logOn = HasLogDefine();
                if (GUInstallerUI.StatusRow(
                        $"Logger — define {LogDefineSymbol}",
                        logOn ? GUSetupState.Done : GUSetupState.Optional,
                        logOn ? "đang bật (Debug)" : "đang tắt (Release)",
                        logOn ? "Tắt log" : "Bật log",
                        DotweenReady))
                {
                    if (logOn) GULoggerMenu.DisableLogs();
                    else GULoggerMenu.EnableLogs();
                    GUIUtility.ExitGUI();
                }

                if (GUInstallerUI.StatusRow(
                        "Audio setup (AudioManager + AudioID)",
                        GUSetupState.Optional,
                        FolderSetupReady ? null : "cần xong bước 2",
                        "Mở cửa sổ",
                        DotweenReady && FolderSetupReady))
                {
                    EditorApplication.ExecuteMenuItem("GameUp/Audio/Setup AudioManager");
                }
            }
        }

        private void DrawPackageCard(
            string title,
            string description,
            string packageName,
            string gitUrl,
            bool installed,
            ref AddRequest request,
            string message,
            Action<string> setMessage,
            string blockedReason)
        {
            var busy = request != null && !request.IsCompleted;
            var state = installed ? GUSetupState.Done
                : busy ? GUSetupState.Busy
                : blockedReason != null ? GUSetupState.Blocked
                : GUSetupState.Optional;

            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader(null, title, state);
                GUILayout.Label(description, GUInstallerUI.Desc);

                var version = installed ? GUDotweenDependencyUtility.GetPackageVersion(packageName) : string.Empty;
                GUInstallerUI.StatusRow(
                    packageName,
                    installed ? GUSetupState.Done : GUSetupState.Missing,
                    installed ? (string.IsNullOrEmpty(version) ? "đã cài" : "v" + version) : "chưa cài");

                if (blockedReason != null) GUInstallerUI.Hint(blockedReason);

                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                if (GUInstallerUI.MiniButton(installed ? "Cài lại / cập nhật" : "Cài qua Git UPM", !busy && blockedReason == null, 160f))
                {
                    setMessage($"Đang cài {title}...");
                    request = Client.Add(gitUrl);
                }

                if (GUInstallerUI.MiniButton("Copy Git URL", true, 110f))
                {
                    EditorGUIUtility.systemCopyBuffer = gitUrl;
                    ShowNotification(new GUIContent("Đã copy Git URL"));
                }
                EditorGUILayout.EndHorizontal();

                if (request != null && request.IsCompleted)
                {
                    if (request.Status == StatusCode.Success)
                    {
                        var result = request.Result;
                        setMessage(result != null
                            ? $"Đã cài: {result.name} {result.version}"
                            : $"Đã cài {title}.");
                    }
                    else if (request.Status >= StatusCode.Failure)
                    {
                        var error = request.Error != null ? request.Error.message : "lỗi Package Manager không xác định.";
                        setMessage($"Cài thất bại: {error}");
                    }

                    request = null;
                    AssetDatabase.Refresh();
                    Repaint(); // message vừa đổi ở trên, vẽ lại ngay thay vì chờ event chuột
                }

                DrawMessage(message);
            }
        }

        private static void DrawMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var isError = message.IndexOf("thất bại", StringComparison.OrdinalIgnoreCase) >= 0;
            EditorGUILayout.HelpBox(message, isError ? MessageType.Error : MessageType.Info);
        }

        // ─── Tải & import DOTween ────────────────────────────────────────────

        private void StartDotweenAutoInstall()
        {
            if (DotweenBusy) return;

            DotweenMessage = "Đang tải package DOTween...";
            _dotweenDownloadedPackagePath = Path.Combine(Path.GetTempPath(), DotweenPackageFileName);

            if (File.Exists(_dotweenDownloadedPackagePath))
                File.Delete(_dotweenDownloadedPackagePath);

            _dotweenDownloadRequest = UnityWebRequest.Get(GUDotweenDependencyUtility.DotweenProDownloadUrl);
            _dotweenDownloadRequest.downloadHandler = new DownloadHandlerFile(_dotweenDownloadedPackagePath);
            _dotweenDownloadRequest.SendWebRequest();
        }

        private void CompleteDotweenDownload()
        {
            if (_dotweenDownloadRequest == null) return;

            var result = _dotweenDownloadRequest.result;
            var errorMessage = _dotweenDownloadRequest.error;
            _dotweenDownloadRequest.Dispose();
            _dotweenDownloadRequest = null;

            if (result != UnityWebRequest.Result.Success)
            {
                DotweenMessage = $"Cài DOTween thất bại: không tải được package ({errorMessage}).";
                return;
            }

            ImportDotweenPackage();
        }

        private void ImportDotweenPackage()
        {
            if (string.IsNullOrWhiteSpace(_dotweenDownloadedPackagePath) || !File.Exists(_dotweenDownloadedPackagePath))
            {
                DotweenMessage = "Cài DOTween thất bại: không tìm thấy file vừa tải.";
                return;
            }

            _dotweenIsInstalling = true;
            DotweenMessage = "Đang import package DOTween...";

            AssetDatabase.importPackageCompleted += OnDotweenImportCompleted;
            AssetDatabase.importPackageFailed += OnDotweenImportFailed;
            AssetDatabase.importPackageCancelled += OnDotweenImportCancelled;
            AssetDatabase.ImportPackage(_dotweenDownloadedPackagePath, false);
        }

        private void OnDotweenImportCompleted(string packageName)
        {
            UnregisterDotweenImportCallbacks();
            _dotweenIsInstalling = false;
            FinalizeDotweenSetup();
            DotweenMessage = $"Đã import DOTween ({packageName}). Bước 1 hoàn tất sau khi Unity compile xong.";
            Repaint();
        }

        private void OnDotweenImportFailed(string packageName, string errorMessage)
        {
            UnregisterDotweenImportCallbacks();
            _dotweenIsInstalling = false;
            DotweenMessage = $"Cài DOTween thất bại: lỗi import ({packageName}) - {errorMessage}";
            Repaint();
        }

        private void OnDotweenImportCancelled(string packageName)
        {
            UnregisterDotweenImportCallbacks();
            _dotweenIsInstalling = false;
            DotweenMessage = $"Cài DOTween thất bại: import bị hủy ({packageName}).";
            Repaint();
        }

        private void UnregisterDotweenImportCallbacks()
        {
            AssetDatabase.importPackageCompleted -= OnDotweenImportCompleted;
            AssetDatabase.importPackageFailed -= OnDotweenImportFailed;
            AssetDatabase.importPackageCancelled -= OnDotweenImportCancelled;
        }

        private void FinalizeDotweenSetup()
        {
            GUDotweenDependencyUtility.CreateDotweenModulesAsmdefIfMissing();
            GUDotweenDependencyUtility.EnableDefineSymbolOnAllTargets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
