#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Cửa sổ tổng của GameUp Core: xem trạng thái mọi bước cài đặt, bật bộ công cụ AI
    /// (Claude Code / Cursor), và mở nhanh các tool lẻ. Đây là nơi duy nhất cần mở
    /// khi vừa thêm GameUpCore vào một project mới.
    /// </summary>
    public sealed class GUSettingsWindow : EditorWindow
    {
        public const string MenuPath = "GameUp/Settings";

        private const string CoreInstallerMenu = "GameUp/Project/GameUpCore Installer";
        private const string FolderSetupMenu = "GameUp/Project/Folder Setup";
        private const string HelperPackagesMenu = "GameUp/Project/Helper Package Installer";
        private const string CursorRulesMenu = "GameUp/Project/Install Cursor IDE rules (from GameUp Core)";
        private const string DataSaveMenu = "GameUp/Data/Data Save Viewer";
        private const string AudioSetupMenu = "GameUp/Audio/Setup AudioManager";
        private const string LevelTrackingMenu = "GameUp/Level Tracking Viewer";

        private Vector2 _scroll;
        private string _version;

        [MenuItem(MenuPath, false, 0)]
        public static void Open()
        {
            var window = GetWindow<GUSettingsWindow>(utility: false, title: "GameUp Settings", focus: true);
            window.minSize = new Vector2(560f, 520f);
            window.Show();
        }

        /// <summary>Các bước bắt buộc đã xong hết chưa (không tính trạng thái scene đang mở).</summary>
        public static bool IsProjectFullySetUp()
        {
            return GUDotweenDependencyUtility.IsDotweenDependencyInstalled()
                   && GUProjectFolderSetupWindow.IsSetupCompleted()
                   && IsAiToolkitReady();
        }

        /// <summary>
        /// Chỉ tính những công cụ dev đã chọn. Người chỉ dùng Cursor không bị coi là "thiếu Claude"
        /// và ngược lại; chưa chọn thì coi như còn việc phải làm.
        /// </summary>
        private static bool IsAiToolkitReady()
        {
            if (!GUCoreUserPrefs.AiToolkitChoiceMade)
                return false;

            if (GUCoreUserPrefs.UseClaudeToolkit && !GUClaudeToolkitInstaller.GetStatus().IsComplete)
                return false;

            if (GUCoreUserPrefs.UseCursorToolkit && !GUCursorRulesInstaller.IsInstalled())
                return false;

            return true;
        }

        private void OnEnable()
        {
            _version = ReadCoreVersion();
        }

        private void OnFocus() => Repaint();

        private void OnProjectChange() => Repaint();

        private void OnGUI()
        {
            GUInstallerUI.EnsureStyles();

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                DrawHeader();
                DrawRequiredSteps();
                DrawAiToolkit();
                DrawAutomation();
                DrawTools();
                DrawFooter();
            }
        }

        // ─── Header ──────────────────────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("GameUp Core", GUInstallerUI.CardTitle);
            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(_version))
                GUILayout.Label($"v{_version}", GUInstallerUI.PathLabel);
            EditorGUILayout.EndHorizontal();

            GUInstallerUI.Hint("Trung tâm cài đặt: dependency, cấu trúc dự án và bộ công cụ AI cho team.");

            var done = 0;
            var total = 3;
            if (GUDotweenDependencyUtility.IsDotweenDependencyInstalled()) done++;
            if (GUProjectFolderSetupWindow.IsSetupCompleted()) done++;
            if (IsAiToolkitReady()) done++;

            EditorGUILayout.Space(4);
            GUInstallerUI.ProgressBar("Hoàn tất", done, total);
        }

        // ─── Bước bắt buộc ───────────────────────────────────────────────────

        private void DrawRequiredSteps()
        {
            GUInstallerUI.SectionHeader("BƯỚC BẮT BUỘC", "làm theo thứ tự");

            using (GUInstallerUI.BeginCard())
            {
                var dotweenOk = GUDotweenDependencyUtility.IsDotweenDependencyInstalled();
                if (GUInstallerUI.StatusRow(
                        "1. DOTween + define DOTween__DEPENDENCIES_INSTALLED",
                        dotweenOk ? GUSetupState.Done : GUSetupState.Missing,
                        dotweenOk ? null : "UI Runtime cần define này",
                        "Mở Installer"))
                {
                    EditorApplication.ExecuteMenuItem(CoreInstallerMenu);
                }

                var folderOk = GUProjectFolderSetupWindow.IsSetupCompleted();
                var folderProgress = GUProjectFolderSetupWindow.GetSetupProgress();
                if (GUInstallerUI.StatusRow(
                        "2. Cấu trúc thư mục _MainProject",
                        folderOk ? GUSetupState.Done : dotweenOk ? GUSetupState.Missing : GUSetupState.Blocked,
                        $"{folderProgress.folders}/{folderProgress.totalFolders} thư mục · {folderProgress.assets}/{folderProgress.totalAssets} asset",
                        "Mở Folder Setup"))
                {
                    GUProjectFolderSetupWindow.OpenWindowFromInstaller();
                }

                var sceneOk = GUCoreProjectSetup.HasCoreObjectsInScene();
                var canCoreSetup = dotweenOk && folderOk;
                if (GUInstallerUI.StatusRow(
                        "3. Prefab Core trên scene hiện tại",
                        sceneOk ? GUSetupState.Done : canCoreSetup ? GUSetupState.Missing : GUSetupState.Blocked,
                        sceneOk ? "====Manager==== + =====UI=====" : "chạy lại cho mỗi scene mới",
                        "Chạy Core setup",
                        canCoreSetup))
                {
                    GUCoreProjectSetup.RunCoreSetup(log: true);
                }
            }
        }

        // ─── AI toolkit ──────────────────────────────────────────────────────

        private void DrawAiToolkit()
        {
            var chosen = GUCoreUserPrefs.AiToolkitChoiceMade;
            GUInstallerUI.SectionHeader(
                "BỘ CÔNG CỤ AI",
                chosen ? "quy ước dự án cho IDE bạn dùng" : "chọn IDE — có thể chọn cả hai");

            using (GUInstallerUI.BeginCard())
            {
                if (!chosen)
                {
                    EditorGUILayout.HelpBox(
                        "Chưa chọn công cụ AI cho project này. GameUp Core sẽ KHÔNG tự sinh .claude/ hay .cursor/ "
                        + "cho tới khi bạn chọn ở dưới — dev nào không dùng AI thì cứ bỏ qua.",
                        MessageType.Info);
                    EditorGUILayout.Space(4);
                }

                DrawClaudeRow();
                EditorGUILayout.Space(6);
                GUInstallerUI.Separator();
                EditorGUILayout.Space(4);
                DrawCursorRow();

                EditorGUILayout.Space(8);
                DrawAiToolkitActions(chosen);
            }
        }

        private void DrawClaudeRow()
        {
            GUCoreUserPrefs.UseClaudeToolkit = EditorGUILayout.ToggleLeft(
                "Claude Code",
                GUCoreUserPrefs.UseClaudeToolkit,
                EditorStyles.boldLabel);

            var claude = GUClaudeToolkitInstaller.GetStatus();
            var enabled = GUCoreUserPrefs.UseClaudeToolkit;
            var state = !enabled
                ? GUSetupState.Optional
                : !claude.TemplatesAvailable
                    ? GUSetupState.Blocked
                    : claude.IsComplete
                        ? GUSetupState.Done
                        : GUSetupState.Missing;

            if (GUInstallerUI.StatusRow(
                    "CLAUDE.md · .claude/{agents, skills, commands, hooks, settings.json}",
                    state,
                    claude.TemplatesAvailable ? $"{claude.Installed}/{claude.Total} file" : "thiếu Documentation~/claude",
                    claude.Installed > 0 ? "Cập nhật" : "Cài đặt",
                    enabled && claude.TemplatesAvailable))
            {
                InstallClaudeToolkit(claude.Installed > 0);
            }

            GUInstallerUI.Hint(
                "4 agent · 10 skill · 11 lệnh /gu-* · hook chặn lệnh phá huỷ và bắt logger sai chuẩn. "
                + ".claude/settings.local.json cá nhân không bị đụng tới.");
        }

        private void DrawCursorRow()
        {
            GUCoreUserPrefs.UseCursorToolkit = EditorGUILayout.ToggleLeft(
                "Cursor IDE",
                GUCoreUserPrefs.UseCursorToolkit,
                EditorStyles.boldLabel);

            var cursorOk = GUCursorRulesInstaller.IsInstalled();
            var enabled = GUCoreUserPrefs.UseCursorToolkit;
            var state = !enabled
                ? GUSetupState.Optional
                : cursorOk
                    ? GUSetupState.Done
                    : GUSetupState.Missing;

            if (GUInstallerUI.StatusRow(
                    ".cursorrules · .cursorignore · .cursor/{rules, skills, hooks}",
                    state,
                    cursorOk ? ".cursor/rules" : "chưa cài",
                    cursorOk ? "Cập nhật" : "Cài đặt",
                    enabled))
            {
                EditorApplication.ExecuteMenuItem(CursorRulesMenu);
            }

            GUInstallerUI.Hint(
                "7 rule .mdc · skills CCGS-lite · hook chặn shell nguy hiểm. "
                + "Kèm thêm package IDE Cursor cho Unity (com.boxqkrtm.ide.cursor).");
        }

        private void DrawAiToolkitActions(bool chosen)
        {
            if (!chosen)
            {
                var anySelected = GUCoreUserPrefs.UseClaudeToolkit || GUCoreUserPrefs.UseCursorToolkit;
                if (GUInstallerUI.PrimaryButton(
                        anySelected ? "Cài các mục đã chọn" : "Chọn ít nhất một mục ở trên",
                        anySelected))
                {
                    ApplyAiToolkitChoice();
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUInstallerUI.MiniButton("Không dùng công cụ AI cho project này", true, 260f))
                {
                    GUCoreUserPrefs.UseClaudeToolkit = false;
                    GUCoreUserPrefs.UseCursorToolkit = false;
                    GUCoreUserPrefs.AiToolkitChoiceMade = true;
                    GULogger.Log("GameUpSetup", "Đã tắt bộ công cụ AI cho project này. Bật lại bất cứ lúc nào trong GameUp → Settings.");
                }
                EditorGUILayout.EndHorizontal();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUInstallerUI.MiniButton("Mở thư mục .claude", GUCoreUserPrefs.UseClaudeToolkit, 150f))
                GUClaudeToolkitInstaller.RevealClaudeFolder();

            if (GUInstallerUI.MiniButton("Mở CLAUDE.md", File.Exists(GUClaudeToolkitInstaller.MemoryFilePath), 120f))
                EditorUtility.OpenWithDefaultApp(GUClaudeToolkitInstaller.MemoryFilePath);

            GUILayout.FlexibleSpace();
            if (GUInstallerUI.MiniButton("Chọn lại", true, 90f))
            {
                GUCoreUserPrefs.ResetAiToolkitChoice();
                GULogger.Log("GameUpSetup", "Đã xoá lựa chọn công cụ AI — chọn lại trong mục Bộ công cụ AI.");
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Chốt lựa chọn và bù file cho những công cụ được chọn. Dùng chế độ không ghi đè
        /// để bản team đã chỉnh (vd .cursorrules riêng) không bị mất.
        /// </summary>
        private void ApplyAiToolkitChoice()
        {
            GUCoreUserPrefs.AiToolkitChoiceMade = true;

            if (GUCoreUserPrefs.UseClaudeToolkit)
                GUClaudeToolkitInstaller.Install(overwrite: false, log: true);

            if (GUCoreUserPrefs.UseCursorToolkit)
                GUCursorRulesInstaller.InstallAll(overwrite: false, log: true, addIdePackage: true);

            Repaint();
        }

        private void InstallClaudeToolkit(bool alreadyPartlyInstalled)
        {
            if (alreadyPartlyInstalled
                && !EditorUtility.DisplayDialog(
                    "GameUp Claude Toolkit",
                    "Ghi đè CLAUDE.md và .claude/{agents,skills,commands,hooks,settings.json} bằng bản mẫu của GameUp Core.\n\n"
                    + "Thay đổi bạn tự sửa trong các file đó sẽ mất.\n"
                    + ".claude/settings.local.json không bị đụng tới.",
                    "Ghi đè",
                    "Huỷ"))
            {
                return;
            }

            GUCoreUserPrefs.AiToolkitChoiceMade = true;
            GUClaudeToolkitInstaller.Install(overwrite: true, log: true);
            Repaint();
        }

        // ─── Tự động hoá ─────────────────────────────────────────────────────

        private void DrawAutomation()
        {
            GUInstallerUI.SectionHeader("TỰ ĐỘNG", "áp dụng cho project này trên máy hiện tại");

            using (GUInstallerUI.BeginCard())
            {
                GUCoreUserPrefs.AutoInstallAiToolkit = EditorGUILayout.ToggleLeft(
                    "Tự bù file còn thiếu của công cụ AI đã chọn khi mở project",
                    GUCoreUserPrefs.AutoInstallAiToolkit);
                GUInstallerUI.Hint(
                    "Chỉ tạo file còn thiếu — không bao giờ ghi đè file bạn đã sửa, và chỉ chạm vào công cụ bạn đã bật ở trên.");

                EditorGUILayout.Space(3);

                GUCoreUserPrefs.OpenSettingsOnFirstRun = EditorGUILayout.ToggleLeft(
                    "Mở cửa sổ này ở lần đầu mở project",
                    GUCoreUserPrefs.OpenSettingsOnFirstRun);

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUInstallerUI.MiniButton("Cho hiện lại lần đầu", true, 170f))
                {
                    GUCoreUserPrefs.ResetFirstRun();
                    GULogger.Log("GameUpSetup", "Đã reset cờ 'đã mở lần đầu' — cửa sổ Settings sẽ hiện lại ở lần mở project sau.");
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // ─── Công cụ ─────────────────────────────────────────────────────────

        private void DrawTools()
        {
            GUInstallerUI.SectionHeader("CÔNG CỤ", "mở nhanh");

            using (GUInstallerUI.BeginCard())
            {
                EditorGUILayout.BeginHorizontal();
                if (GUInstallerUI.MiniButton("GameUpCore Installer")) EditorApplication.ExecuteMenuItem(CoreInstallerMenu);
                if (GUInstallerUI.MiniButton("Helper Packages")) EditorApplication.ExecuteMenuItem(HelperPackagesMenu);
                if (GUInstallerUI.MiniButton("Folder Setup")) GUProjectFolderSetupWindow.OpenWindowFromInstaller();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(3);
                EditorGUILayout.BeginHorizontal();
                if (GUInstallerUI.MiniButton("Data Save Viewer")) EditorApplication.ExecuteMenuItem(DataSaveMenu);
                if (GUInstallerUI.MiniButton("Audio Setup")) EditorApplication.ExecuteMenuItem(AudioSetupMenu);
                if (GUInstallerUI.MiniButton("Level Tracking")) EditorApplication.ExecuteMenuItem(LevelTrackingMenu);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(6);
                GUInstallerUI.Separator();
                EditorGUILayout.Space(3);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Logger", GUInstallerUI.Desc);
                GUILayout.FlexibleSpace();
                if (GUInstallerUI.MiniButton("Bật log (Debug)", true, 130f)) GULoggerMenu.EnableLogs();
                if (GUInstallerUI.MiniButton("Tắt log (Release)", true, 140f)) GULoggerMenu.DisableLogs();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(8);
            GUInstallerUI.Hint(
                "Luồng khuyến nghị trong Claude Code: /gu-kickoff → /gu-tasks → /gu-story → /gu-review → /gu-test → /gu-release. "
                + "Gặp bug: /gu-bug · Game nặng: /gu-perf · Không rõ Core có sẵn gì: /gu-core.");
            EditorGUILayout.Space(6);
        }

        // ─── Tiện ích ────────────────────────────────────────────────────────

        [System.Serializable]
        private sealed class PackageManifest
        {
            public string version;
        }

        private static string ReadCoreVersion()
        {
            if (!GUCursorRulesInstaller.TryGetGameUpCorePackageRoot(out var root))
                return null;

            var manifestPath = Path.Combine(root, "package.json");
            if (!File.Exists(manifestPath))
                return null;

            try
            {
                return JsonUtility.FromJson<PackageManifest>(File.ReadAllText(manifestPath))?.version;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>Đưa GameUp vào Project Settings để người mới tìm thấy theo thói quen Unity.</summary>
    internal static class GUSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/GameUp", SettingsScope.Project)
            {
                label = "GameUp Core",
                keywords = new[] { "GameUp", "Core", "Claude", "Cursor", "AI", "Setup", "DOTween" },
                guiHandler = _ =>
                {
                    GUInstallerUI.EnsureStyles();
                    EditorGUILayout.Space(6);

                    var claude = GUClaudeToolkitInstaller.GetStatus();
                    EditorGUILayout.LabelField(
                        "Claude toolkit",
                        claude.TemplatesAvailable ? $"{claude.Installed}/{claude.Total} file" : "không tìm thấy mẫu");
                    EditorGUILayout.LabelField(
                        "Cursor rules",
                        GUCursorRulesInstaller.IsInstalled() ? "đã cài" : "chưa cài");

                    EditorGUILayout.Space(8);
                    EditorGUILayout.LabelField("Công cụ AI dùng cho project này", EditorStyles.boldLabel);
                    GUCoreUserPrefs.UseClaudeToolkit = EditorGUILayout.ToggleLeft(
                        "Claude Code", GUCoreUserPrefs.UseClaudeToolkit);
                    GUCoreUserPrefs.UseCursorToolkit = EditorGUILayout.ToggleLeft(
                        "Cursor IDE", GUCoreUserPrefs.UseCursorToolkit);
                    if (!GUCoreUserPrefs.AiToolkitChoiceMade)
                        EditorGUILayout.HelpBox("Chưa chốt lựa chọn — mở GameUp Settings để cài.", MessageType.Info);

                    EditorGUILayout.Space(8);
                    GUCoreUserPrefs.AutoInstallAiToolkit = EditorGUILayout.ToggleLeft(
                        "Tự bù file còn thiếu của công cụ đã chọn khi mở project",
                        GUCoreUserPrefs.AutoInstallAiToolkit);
                    GUCoreUserPrefs.OpenSettingsOnFirstRun = EditorGUILayout.ToggleLeft(
                        "Mở cửa sổ GameUp Settings ở lần đầu mở project",
                        GUCoreUserPrefs.OpenSettingsOnFirstRun);

                    EditorGUILayout.Space(10);
                    if (GUILayout.Button("Mở GameUp Settings", GUILayout.Height(26f)))
                        GUSettingsWindow.Open();
                }
            };
        }
    }
}
#endif
