#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Cài bộ công cụ Claude Code của GameUp vào gốc project: <c>CLAUDE.md</c> và
    /// <c>.claude/{agents,skills,commands,hooks,settings.json}</c>.
    /// Mẫu nằm trong <c>Documentation~/claude</c> (thư mục có <c>~</c> nên Unity không import).
    /// </summary>
    public static class GUClaudeToolkitInstaller
    {
        public const string MenuPath = "GameUp/Project/Install Claude Code toolkit";

        private const string TemplatesDirName = "claude";
        private const string ClaudeDirName = ".claude";
        private const string MemoryFileName = "CLAUDE.md";
        private const string SettingsFileName = "settings.json";
        private const string SettingsTemplateFileName = "settings.template.json";
        private const string BackupFileName = "settings.gameup-backup.json";

        /// <summary>Chuỗi chỉ có trong settings.json do GameUp sinh — dùng để biết có được ghi đè an toàn không.</summary>
        private const string GeneratedSignature = "gu-shell-guard";

        private const string ShellGuardName = "gu-shell-guard";
        private const string CSharpGuardName = "gu-csharp-guard";

        private static readonly string[] TemplateSubDirs = { "agents", "skills", "commands" };

        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        public static string ClaudeDir => Path.Combine(ProjectRoot, ClaudeDirName);

        public static string MemoryFilePath => Path.Combine(ProjectRoot, MemoryFileName);

        public static string SettingsFilePath => Path.Combine(ClaudeDir, SettingsFileName);

        /// <summary>Windows chạy hook bằng PowerShell, còn lại dùng <c>sh</c>.</summary>
        private static bool UsePowerShellHooks =>
            Application.platform == RuntimePlatform.WindowsEditor;

        private static string HookExtension => UsePowerShellHooks ? ".ps1" : ".sh";

        // ─── Trạng thái ──────────────────────────────────────────────────────

        /// <summary>Số mục đã cài / tổng số mục của bộ template (dùng cho badge và progress).</summary>
        public readonly struct Status
        {
            public readonly int Installed;
            public readonly int Total;
            public readonly bool TemplatesAvailable;

            public Status(int installed, int total, bool templatesAvailable)
            {
                Installed = installed;
                Total = total;
                TemplatesAvailable = templatesAvailable;
            }

            public bool IsComplete => TemplatesAvailable && Total > 0 && Installed >= Total;
            public bool IsPartial => Installed > 0 && !IsComplete;
        }

        public static Status GetStatus()
        {
            if (!TryGetTemplatesDir(out var templates))
                return new Status(0, 0, false);

            var installed = 0;
            var total = 0;

            foreach (var pair in EnumerateFilePairs(templates))
            {
                total++;
                if (File.Exists(pair.Value))
                    installed++;
            }

            if (File.Exists(Path.Combine(templates, "settings", SettingsTemplateFileName)))
            {
                total++;
                if (File.Exists(SettingsFilePath))
                    installed++;
            }

            return new Status(installed, total, true);
        }

        public static bool IsInstalled() => GetStatus().IsComplete;

        // ─── Menu ────────────────────────────────────────────────────────────

        [MenuItem(MenuPath)]
        private static void InstallFromMenu()
        {
            if (!TryGetTemplatesDir(out _))
            {
                EditorUtility.DisplayDialog(
                    "GameUp Core",
                    "Không tìm thấy thư mục mẫu Documentation~/claude trong GameUp Core.",
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "GameUp Claude Toolkit",
                    "Ghi vào gốc project:\n" +
                    "• CLAUDE.md (quy ước dự án)\n" +
                    "• .claude/agents, .claude/skills, .claude/commands, .claude/hooks\n" +
                    "• .claude/settings.json (hook chặn lệnh nguy hiểm + lint C#)\n\n" +
                    "File .claude/settings.local.json của bạn không bị đụng tới.\n" +
                    "Tiếp tục?",
                    "Cài / Cập nhật",
                    "Huỷ"))
            {
                return;
            }

            Install(overwrite: true, log: true);
        }

        // ─── Cài đặt ─────────────────────────────────────────────────────────

        /// <summary>
        /// Copy toàn bộ template sang gốc project.
        /// <paramref name="overwrite"/> = false dùng cho lần tự chạy đầu tiên: chỉ bù file còn thiếu,
        /// không đè lên bản người dùng đã sửa.
        /// </summary>
        public static bool Install(bool overwrite, bool log)
        {
            if (!TryGetTemplatesDir(out var templates))
            {
                if (log)
                    GULogger.Error("ClaudeToolkit", "Thiếu thư mục mẫu Documentation~/claude trong GameUp Core.");
                return false;
            }

            var written = 0;
            foreach (var pair in EnumerateFilePairs(templates))
            {
                if (CopyFile(pair.Key, pair.Value, overwrite))
                    written++;
            }

            written += WriteSettings(templates, overwrite, log) ? 1 : 0;
            MarkHookScriptsExecutable();

            if (log)
            {
                GULogger.Log(
                    "ClaudeToolkit",
                    written > 0
                        ? $"Đã ghi {written} file vào CLAUDE.md và .claude/ — khởi động lại phiên Claude Code để nạp hook mới."
                        : "Bộ công cụ Claude đã đầy đủ, không có gì để ghi.");
            }

            return true;
        }

        /// <summary>Cặp (file mẫu → file đích) của mọi thứ trừ settings.json (được sinh động).</summary>
        private static IEnumerable<KeyValuePair<string, string>> EnumerateFilePairs(string templates)
        {
            var memorySource = Path.Combine(templates, MemoryFileName);
            if (File.Exists(memorySource))
                yield return new KeyValuePair<string, string>(memorySource, MemoryFilePath);

            foreach (var sub in TemplateSubDirs)
            {
                var sourceDir = Path.Combine(templates, sub);
                if (!Directory.Exists(sourceDir))
                    continue;

                foreach (var file in Directory.GetFiles(sourceDir, "*.md", SearchOption.AllDirectories))
                {
                    var relative = GetRelativePath(sourceDir, file);
                    yield return new KeyValuePair<string, string>(
                        file,
                        Path.Combine(ClaudeDir, sub, relative));
                }
            }

            var hooksDir = Path.Combine(templates, "hooks");
            if (!Directory.Exists(hooksDir))
                yield break;

            // Chỉ cài script hợp với hệ điều hành hiện tại — settings.json trỏ đúng vào bản đó.
            foreach (var name in new[] { ShellGuardName, CSharpGuardName })
            {
                var file = Path.Combine(hooksDir, name + HookExtension);
                if (File.Exists(file))
                {
                    yield return new KeyValuePair<string, string>(
                        file,
                        Path.Combine(ClaudeDir, "hooks", Path.GetFileName(file)));
                }
            }
        }

        private static bool CopyFile(string source, string destination, bool overwrite)
        {
            if (!overwrite && File.Exists(destination))
                return false;

            var dir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.Copy(source, destination, overwrite: true);
            return true;
        }

        /// <summary>
        /// Sinh <c>.claude/settings.json</c> từ template, thay lệnh hook theo hệ điều hành.
        /// File cũ không do GameUp sinh sẽ được backup trước khi ghi đè.
        /// </summary>
        private static bool WriteSettings(string templates, bool overwrite, bool log)
        {
            var templateFile = Path.Combine(templates, "settings", SettingsTemplateFileName);
            if (!File.Exists(templateFile))
            {
                if (log)
                    GULogger.Warning("ClaudeToolkit", $"Thiếu file mẫu: {templateFile}");
                return false;
            }

            var exists = File.Exists(SettingsFilePath);
            if (exists && !overwrite)
                return false;

            if (exists)
            {
                var current = File.ReadAllText(SettingsFilePath);
                if (current.IndexOf(GeneratedSignature, StringComparison.Ordinal) < 0)
                {
                    var backup = Path.Combine(ClaudeDir, BackupFileName);
                    File.Copy(SettingsFilePath, backup, overwrite: true);
                    if (log)
                        GULogger.Warning("ClaudeToolkit", $"settings.json cũ không do GameUp sinh — đã backup sang {backup}.");
                }
            }

            var content = File.ReadAllText(templateFile)
                .Replace("__SHELL_GUARD__", BuildHookCommand(ShellGuardName))
                .Replace("__CSHARP_GUARD__", BuildHookCommand(CSharpGuardName));

            Directory.CreateDirectory(ClaudeDir);
            File.WriteAllText(SettingsFilePath, content);
            return true;
        }

        /// <summary>Lệnh chạy hook, đã escape để nhúng thẳng vào chuỗi JSON.</summary>
        private static string BuildHookCommand(string hookName)
        {
            var path = $"$CLAUDE_PROJECT_DIR/{ClaudeDirName}/hooks/{hookName}{HookExtension}";
            var command = UsePowerShellHooks
                ? $"powershell -NoProfile -ExecutionPolicy Bypass -File \"{path}\""
                : $"sh \"{path}\"";

            return command.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>macOS/Linux: hook không có bit execute thì Claude Code không chạy được.</summary>
        private static void MarkHookScriptsExecutable()
        {
            if (UsePowerShellHooks)
                return;

            var hooksDir = Path.Combine(ClaudeDir, "hooks");
            if (!Directory.Exists(hooksDir))
                return;

            foreach (var file in Directory.GetFiles(hooksDir, "*.sh"))
            {
                try
                {
                    using (var process = Process.Start(new ProcessStartInfo("chmod", $"+x \"{file}\"")
                           {
                               UseShellExecute = false,
                               CreateNoWindow = true
                           }))
                    {
                        process?.WaitForExit(3000);
                    }
                }
                catch (Exception e)
                {
                    GULogger.Warning("ClaudeToolkit", $"Không chmod +x được {file}: {e.Message}");
                }
            }
        }

        // ─── Tiện ích ────────────────────────────────────────────────────────

        internal static bool TryGetTemplatesDir(out string path)
        {
            path = null;
            if (!GUCursorRulesInstaller.TryGetGameUpCorePackageRoot(out var root))
                return false;

            var candidate = Path.Combine(root, "Documentation~", TemplatesDirName);
            if (!Directory.Exists(candidate))
                return false;

            path = candidate;
            return true;
        }

        /// <summary>Path.GetRelativePath không có trên mọi profile Unity 2022 — tự tính.</summary>
        private static string GetRelativePath(string baseDir, string fullPath)
        {
            var normalized = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath
                .Substring(normalized.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>Mở thư mục .claude trong file explorer (nút phụ của cửa sổ Settings).</summary>
        public static void RevealClaudeFolder()
        {
            Directory.CreateDirectory(ClaudeDir);
            EditorUtility.RevealInFinder(ClaudeDir);
        }
    }
}
#endif
