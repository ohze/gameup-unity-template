#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using StepStatus = GameUp.Core.Editor.GUClaudeUnityMcpStep.Status;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Trình cài một nút cho Claude Code × Unity (plugin skills <c>unity</c> + Unity MCP): hiện tiến độ, trạng thái
    /// từng bước và log các lệnh đã chạy. Logic nằm ở <see cref="GUClaudeUnityMcpInstaller"/>.
    /// </summary>
    public sealed class GUClaudeUnityMcpWindow : EditorWindow
    {
        public const string MenuPath = "GameUp/Project/Claude Code × Unity (Plugin + MCP)";

        private const string GuideFileName = "unity-mcp-guide.md";
        private const float LogHeight = 220f;

        private Vector2 _scroll;
        private Vector2 _logScroll;
        private int _lastLogLength = -1;
        private string _guidePath;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<GUClaudeUnityMcpWindow>(utility: false, title: "Claude × Unity MCP", focus: true);
            window.minSize = new Vector2(580f, 600f);
            window.Show();
        }

        private void OnEnable()
        {
            _guidePath = FindGuidePath();
            GUClaudeUnityMcpInstaller.Changed += Repaint;

            if (!GUClaudeUnityMcpInstaller.IsBusy && !GUClaudeUnityMcpInstaller.HasChecked)
                GUClaudeUnityMcpInstaller.CheckStatus();
        }

        private void OnDisable()
        {
            GUClaudeUnityMcpInstaller.Changed -= Repaint;
        }

        private void OnGUI()
        {
            GUInstallerUI.EnsureStyles();

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                DrawHeader();
                DrawActions();
                DrawSteps("MỖI MÁY DEV", "làm một lần, dùng cho mọi project", perMachine: true);
                DrawSteps("PROJECT NÀY", "com.unity.pipeline trong Packages/manifest.json", perMachine: false);
                DrawLog();
                DrawNextSteps();
                EditorGUILayout.Space(8);
            }
        }

        private static void DrawHeader()
        {
            EditorGUILayout.Space(6);
            GUILayout.Label("Claude Code × Unity: Plugin + MCP", GUInstallerUI.CardTitle);
            GUInstallerUI.Hint(
                "Claude nhìn thấy và thao tác trên Editor đang mở: đọc scene, sửa GameObject qua API Editor (không sửa YAML), "
                + "bấm Play, đọc Console, chụp Game View, chạy test — kèm bộ skill chính thức của Unity.");
            GUInstallerUI.Hint(
                $"Yêu cầu: Claude Code ≥ {GUClaudeUnityMcpInstaller.MinClaudeCodeVersion} · Unity {GUClaudeUnityMcpInstaller.MinUnityMajorVersion}.0+ · Git · mạng. "
                + "Plugin và pipeline đang beta/experimental.");

            if (!GUClaudeUnityMcpInstaller.IsUnityVersionSupported)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    $"Project đang dùng Unity {Application.unityVersion}. Phần điều khiển Editor ({"com.unity.pipeline"}) cần Unity "
                    + $"{GUClaudeUnityMcpInstaller.MinUnityMajorVersion}.0+ nên bước của project sẽ bị bỏ qua. "
                    + "Phần của máy dev (plugin skills, Unity CLI, đăng ký MCP) vẫn cài được và dùng cho các project Unity 6 khác.",
                    MessageType.Warning);
            }

            var done = GUClaudeUnityMcpInstaller.CountDone(out var applicable);
            EditorGUILayout.Space(4);
            GUInstallerUI.ProgressBar("Hoàn tất", done, applicable);

            if (GUClaudeUnityMcpInstaller.IsBusy)
            {
                var activity = GUClaudeUnityMcpInstaller.CurrentActivity;
                GUInstallerUI.Hint(string.IsNullOrEmpty(activity) ? "Đang kiểm tra trạng thái…" : activity);
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(6);
            var busy = GUClaudeUnityMcpInstaller.IsBusy;

            if (GUInstallerUI.PrimaryButton(busy ? "Đang chạy…" : "Cài đặt tất cả (Plugin + MCP)", !busy, 34f)
                && ConfirmInstall())
            {
                GUClaudeUnityMcpInstaller.Install();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUInstallerUI.MiniButton("Kiểm tra lại", !busy, 110f))
                GUClaudeUnityMcpInstaller.CheckStatus();

            if (GUInstallerUI.MiniButton("Dừng", busy, 70f))
                GUClaudeUnityMcpInstaller.Cancel();

            GUILayout.FlexibleSpace();
            if (GUInstallerUI.MiniButton("Mở hướng dẫn", _guidePath != null, 120f))
                EditorUtility.OpenWithDefaultApp(_guidePath);
            EditorGUILayout.EndHorizontal();
        }

        private static bool ConfirmInstall()
        {
            var pipelineLine = GUClaudeUnityMcpInstaller.IsUnityVersionSupported
                ? "• unity pipeline install — sửa Packages/manifest.json, Editor sẽ import và compile lại\n"
                : string.Empty;

            return EditorUtility.DisplayDialog(
                "Claude Code × Unity",
                "Chạy tuần tự các lệnh sau; bước nào đã xong thì bỏ qua:\n\n"
                + $"• claude update — chỉ khi Claude Code < {GUClaudeUnityMcpInstaller.MinClaudeCodeVersion}\n"
                + "• claude plugin marketplace add Unity-Technologies/unity-agent-plugin\n"
                + "• claude plugin install unity@unity-agent-plugin --scope user\n"
                + "• Cài Unity CLI (channel beta): tải script từ public-cdn.cloud.unity3d.com và chạy bằng bash/PowerShell\n"
                + "• claude mcp add --scope user unity-editor-mcp → unity mcp (ghi vào cấu hình Claude Code của tài khoản)\n"
                + pipelineLine
                + "\nKhông commit, không sửa scene/prefab/script.",
                "Cài đặt",
                "Huỷ");
        }

        private static void DrawSteps(string title, string hint, bool perMachine)
        {
            GUInstallerUI.SectionHeader(title, hint);

            using (GUInstallerUI.BeginCard())
            {
                foreach (var step in GUClaudeUnityMcpInstaller.Steps)
                {
                    if (step.PerMachine == perMachine)
                        DrawStepRow(step);
                }
            }
        }

        private static void DrawStepRow(GUClaudeUnityMcpStep step)
        {
            EditorGUILayout.BeginHorizontal();
            GUInstallerUI.DrawBadge(LabelOf(step.State), ColorOf(step.State), 96f);
            GUILayout.Space(6);
            GUILayout.Label(step.Title, GUInstallerUI.Desc);
            GUILayout.FlexibleSpace();

            var old = GUI.color;
            GUI.color = GUInstallerUI.MutedColor;
            GUILayout.Label(step.Detail, GUInstallerUI.PathLabel);
            GUI.color = old;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLog()
        {
            GUInstallerUI.SectionHeader("TIẾN TRÌNH", "lệnh đã chạy và kết quả");

            var log = GUClaudeUnityMcpInstaller.Log;
            if (log.Length != _lastLogLength)
            {
                _lastLogLength = log.Length;
                _logScroll.y = float.MaxValue;
            }

            using (GUInstallerUI.BeginCard())
            {
                var text = log.Length > 0 ? log : "Chưa có log — bấm Cài đặt tất cả để bắt đầu.";
                var width = Mathf.Max(200f, position.width - 60f);
                var height = EditorStyles.wordWrappedMiniLabel.CalcHeight(new GUIContent(text), width);

                using (var scroll = new EditorGUILayout.ScrollViewScope(_logScroll, GUILayout.Height(LogHeight)))
                {
                    _logScroll = scroll.scrollPosition;
                    EditorGUILayout.SelectableLabel(text, EditorStyles.wordWrappedMiniLabel, GUILayout.Height(height));
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUInstallerUI.MiniButton("Copy log", log.Length > 0, 90f))
                    EditorGUIUtility.systemCopyBuffer = log;

                if (GUInstallerUI.MiniButton("Xoá log", log.Length > 0 && !GUClaudeUnityMcpInstaller.IsBusy, 80f))
                    GUClaudeUnityMcpInstaller.ClearLog();
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void DrawNextSteps()
        {
            var done = GUClaudeUnityMcpInstaller.CountDone(out var applicable);
            if (GUClaudeUnityMcpInstaller.IsBusy || applicable == 0 || done < applicable)
                return;

            var commitLine = GUClaudeUnityMcpInstaller.IsUnityVersionSupported
                ? "• Commit Packages/manifest.json và Packages/packages-lock.json để cả team dùng chung.\n"
                : string.Empty;

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Đã xong. Việc bạn tự làm:\n"
                + "• Reload Window VS Code (Ctrl+Shift+P → Developer: Reload Window) hoặc mở lại Claude Code để tool MCP hiện ra.\n"
                + commitLine
                + "• Thử hỏi Claude: \"Liệt kê scene đang mở và lỗi trong Console\".",
                MessageType.Info);
        }

        private static string LabelOf(StepStatus status)
        {
            switch (status)
            {
                case StepStatus.Done: return "XONG";
                case StepStatus.Missing: return "CHƯA CÓ";
                case StepStatus.Running: return "ĐANG CHẠY";
                case StepStatus.Blocked: return "BỊ CHẶN";
                case StepStatus.Failed: return "LỖI";
                default: return "CHƯA KIỂM TRA";
            }
        }

        private static Color ColorOf(StepStatus status)
        {
            switch (status)
            {
                case StepStatus.Done: return GUInstallerUI.OkColor;
                case StepStatus.Missing:
                case StepStatus.Failed: return GUInstallerUI.MissingColor;
                case StepStatus.Running: return GUInstallerUI.BusyColor;
                case StepStatus.Blocked: return GUInstallerUI.BlockedColor;
                default: return GUInstallerUI.MutedColor;
            }
        }

        private static string FindGuidePath()
        {
            if (!GUCursorRulesInstaller.TryGetGameUpCorePackageRoot(out var root))
                return null;

            var path = Path.Combine(root, "Documentation~", GuideFileName);
            return File.Exists(path) ? path : null;
        }
    }
}
#endif
