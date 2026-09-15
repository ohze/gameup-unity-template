#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using StepStatus = GameUp.Core.Editor.GUMcpInstallStep.Status;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Giao diện chung của các trình cài "IDE × Unity MCP": một nút cài, thanh tiến độ, trạng thái từng bước và log lệnh.
    /// Lớp con chỉ khai báo runner và chữ hiển thị riêng của IDE.
    /// </summary>
    public abstract class GUMcpInstallerWindowBase : EditorWindow
    {
        private const string GuideFileName = "unity-mcp-guide.md";
        private const float LogHeight = 220f;

        private Vector2 _scroll;
        private Vector2 _logScroll;
        private int _lastLogLength = -1;
        private string _guidePath;

        protected abstract GUMcpInstallRunner Runner { get; }

        /// <summary>Tên hiển thị, vd "Claude Code × Unity: Plugin + MCP".</summary>
        protected abstract string Heading { get; }

        protected abstract string Description { get; }

        protected abstract string Requirements { get; }

        /// <summary>Danh sách lệnh sẽ chạy (mỗi dòng một "• …"), hiện trong hộp thoại xác nhận.</summary>
        protected abstract string ConfirmCommands { get; }

        /// <summary>Việc người dùng tự làm sau khi xong (mỗi dòng một "• …").</summary>
        protected abstract string ManualFollowUp { get; }

        protected virtual void OnEnable()
        {
            _guidePath = FindGuidePath();
            Runner.Changed += Repaint;

            if (!Runner.IsBusy && !Runner.HasChecked)
                Runner.CheckStatus();
        }

        protected virtual void OnDisable()
        {
            Runner.Changed -= Repaint;
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
                DrawSteps("PROJECT NÀY", "file trong repo + com.unity.pipeline", perMachine: false);
                DrawLog();
                DrawNextSteps();
                EditorGUILayout.Space(8);
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);
            GUILayout.Label(Heading, GUInstallerUI.CardTitle);
            GUInstallerUI.Hint(Description);
            GUInstallerUI.Hint(Requirements);

            if (!GUMcpInstallRunner.IsUnityVersionSupported)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    $"Project đang dùng Unity {Application.unityVersion}. Phần điều khiển Editor (com.unity.pipeline) cần Unity "
                    + $"{GUMcpInstallRunner.MinUnityMajorVersion}.0+ nên các bước đó sẽ bị bỏ qua. "
                    + "Phần của máy dev vẫn cài được và dùng cho các project Unity 6 khác.",
                    MessageType.Warning);
            }

            var done = Runner.CountDone(out var applicable);
            EditorGUILayout.Space(4);
            GUInstallerUI.ProgressBar("Hoàn tất", done, applicable);

            if (Runner.IsBusy)
            {
                var activity = Runner.CurrentActivity;
                GUInstallerUI.Hint(string.IsNullOrEmpty(activity) ? "Đang kiểm tra trạng thái…" : activity);
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(6);
            var busy = Runner.IsBusy;

            if (GUInstallerUI.PrimaryButton(busy ? "Đang chạy…" : "Cài đặt tất cả", !busy, 34f) && ConfirmInstall())
                Runner.Install(Heading);

            EditorGUILayout.BeginHorizontal();
            if (GUInstallerUI.MiniButton("Kiểm tra lại", !busy, 110f))
                Runner.CheckStatus();

            if (GUInstallerUI.MiniButton("Dừng", busy, 70f))
                Runner.Cancel();

            GUILayout.FlexibleSpace();
            if (GUInstallerUI.MiniButton("Mở hướng dẫn", _guidePath != null, 120f))
                EditorUtility.OpenWithDefaultApp(_guidePath);
            EditorGUILayout.EndHorizontal();
        }

        private bool ConfirmInstall()
        {
            return EditorUtility.DisplayDialog(
                Heading,
                $"Chạy tuần tự các bước sau; bước nào đã xong thì bỏ qua:\n\n{ConfirmCommands}\n\nKhông commit, không sửa scene/prefab/script.",
                "Cài đặt",
                "Huỷ");
        }

        private void DrawSteps(string title, string hint, bool perMachine)
        {
            GUInstallerUI.SectionHeader(title, hint);

            using (GUInstallerUI.BeginCard())
            {
                foreach (var step in Runner.Steps)
                {
                    if (step.PerMachine == perMachine)
                        DrawStepRow(step);
                }
            }
        }

        private static void DrawStepRow(GUMcpInstallStep step)
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

            var log = Runner.Log;
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

                if (GUInstallerUI.MiniButton("Xoá log", log.Length > 0 && !Runner.IsBusy, 80f))
                    Runner.ClearLog();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawNextSteps()
        {
            var done = Runner.CountDone(out var applicable);
            if (Runner.IsBusy || applicable == 0 || done < applicable)
                return;

            var commitLine = GUMcpInstallRunner.IsUnityVersionSupported
                ? "• Commit Packages/manifest.json và Packages/packages-lock.json để cả team dùng chung.\n"
                : string.Empty;

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox($"Đã xong. Việc bạn tự làm:\n{ManualFollowUp}\n{commitLine}• Thử hỏi Agent: \"Liệt kê scene đang mở và lỗi trong Console\".", MessageType.Info);
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
