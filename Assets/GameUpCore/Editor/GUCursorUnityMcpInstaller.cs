#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using StepKind = GameUp.Core.Editor.GUMcpInstallStep.Kind;
using StepStatus = GameUp.Core.Editor.GUMcpInstallStep.Status;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Cài Cursor × Unity MCP: đăng ký server <c>unity</c> vào <c>~/.cursor/mcp.json</c> qua <c>unity mcp configure cursor</c>,
    /// chép skill chính thức của Unity vào <c>~/.cursor/skills</c> (Cursor không cài được plugin marketplace của Claude),
    /// bộ rules/skills GameUp cho Cursor và <c>com.unity.pipeline</c>.
    /// Máy chạy và bước dùng chung nằm ở <see cref="GUMcpInstallRunner"/>.
    /// </summary>
    [InitializeOnLoad]
    public static class GUCursorUnityMcpInstaller
    {
        public const string McpServerName = "unity";

        private const string PluginRepoUrl = "https://github.com/Unity-Technologies/unity-agent-plugin.git";
        private const string CloneDirName = "gameup-unity-agent-plugin";
        private const string SkillFileName = "SKILL.md";
        private const string McpBackupFileName = "mcp.gameup-backup.json";
        private const string McpRuleFileName = "unity-mcp.mdc";

        /// <summary>Skill có trong plugin Unity nhưng không trùng tên skill nào của GameUp — dùng làm dấu hiệu "đã chép".</summary>
        private const string UnitySkillMarker = "unity-cli";

        /// <summary>
        /// Cursor chạy server MCP với cwd = thư mục home, nên <c>unity mcp</c> không tìm ra Editor và trả 0 tool
        /// (đo thật: cwd=home → 0 tool, cwd=project → 151). Phải truyền <c>--project-path</c>; dùng biến <c>${workspaceFolder}</c>
        /// (Cursor thay bằng thư mục đang mở — đã kiểm trên Cursor 3.20) để một cấu hình user dùng được cho mọi project.
        /// </summary>
        private const string ProjectPathArgs = "[\"mcp\", \"--project-path\", \"${workspaceFolder}\"]";

        /// <summary>Entry <c>"unity": { … "args": [ … ] }</c>; nhóm <c>head</c> là phần đứng trước mảng args.</summary>
        private static readonly Regex UnityArgsPattern = new Regex(
            "(?<head>\"" + McpServerName + "\"\\s*:\\s*\\{[^}]*?\"args\"\\s*:\\s*)\\[[^\\]]*\\]", RegexOptions.Compiled);

        private static readonly Regex WorkspaceFolderArgPattern = new Regex(
            "\"--project-path\"\\s*,\\s*\"\\$\\{workspaceFolder\\}\"", RegexOptions.Compiled);

        private static readonly GUMcpInstallStep[] StepList =
        {
            new GUMcpInstallStep(StepKind.CursorApp, "Cursor IDE trên máy", perMachine: true),
            new GUMcpInstallStep(StepKind.UnityCli, "Unity CLI (lệnh unity mcp)", perMachine: true),
            new GUMcpInstallStep(StepKind.CursorMcpConfig, $"MCP server {McpServerName} trong ~/.cursor/mcp.json", perMachine: true),
            new GUMcpInstallStep(StepKind.UnitySkills, "Skill chính thức của Unity (~/.cursor/skills)", perMachine: true),
            new GUMcpInstallStep(StepKind.CursorToolkit, "Rules + skills GameUp cho Cursor (.cursor/)", perMachine: false),
            new GUMcpInstallStep(StepKind.Pipeline, "Package com.unity.pipeline", perMachine: false, requiresUnity6: true),
            new GUMcpInstallStep(StepKind.EditorReady, "Editor ready", perMachine: false, requiresUnity6: true)
        };

        public static readonly GUMcpInstallRunner Runner =
            new GUMcpInstallRunner("GameUp.Core.CursorMcp", "CursorMCP", StepList, CheckStep, ActStep);

        static GUCursorUnityMcpInstaller()
        {
            if (!Application.isBatchMode)
                EditorApplication.delayCall += Runner.ResumeIfInstalling;
        }

        private static string CursorHomeDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cursor");

        private static string McpConfigPath => Path.Combine(CursorHomeDir, "mcp.json");

        private static string UserSkillsDir => Path.Combine(CursorHomeDir, "skills");

        private static string McpRulePath =>
            Path.Combine(GUClaudeToolkitInstaller.ProjectRoot, ".cursor", "rules", McpRuleFileName);

        // ─── Kiểm tra ────────────────────────────────────────────────────────

        private static IEnumerator CheckStep(GUMcpInstallStep step, bool waitUntilReady)
        {
            switch (step.Id)
            {
                case StepKind.CursorApp: return CheckCursorApp(step);
                case StepKind.UnityCli: return Runner.CheckUnityCli(step);
                case StepKind.CursorMcpConfig: return CheckMcpConfig(step);
                case StepKind.UnitySkills: return CheckUnitySkills(step);
                case StepKind.CursorToolkit: return CheckCursorToolkit(step);
                case StepKind.Pipeline: return Runner.CheckPipeline(step);
                default:
                    return Runner.CheckEditorReady(step, waitUntilReady, $"Editor ready — mở lại Cursor, bật server {McpServerName}, chat mới");
            }
        }

        /// <summary>Cursor không bắt buộc cài lệnh <c>cursor</c> vào PATH; đã mở Cursor ít nhất một lần thì có <c>~/.cursor</c>.</summary>
        private static IEnumerator CheckCursorApp(GUMcpInstallStep step)
        {
            var cli = GUExternalCommand.ResolveExecutable("cursor");
            if (cli != null || Directory.Exists(CursorHomeDir))
            {
                Runner.SetStep(step, StepStatus.Done, cli ?? CursorHomeDir);
                yield break;
            }

            Runner.SetStep(step, StepStatus.Blocked, "chưa thấy Cursor — cài và mở Cursor một lần rồi bấm lại");
        }

        private static IEnumerator CheckMcpConfig(GUMcpInstallStep step)
        {
            var entry = File.Exists(McpConfigPath) ? UnityArgsPattern.Match(File.ReadAllText(McpConfigPath)) : Match.Empty;
            if (!entry.Success)
                Runner.SetStep(step, StepStatus.Missing, "chưa đăng ký");
            else if (!WorkspaceFolderArgPattern.IsMatch(entry.Value))
                Runner.SetStep(step, StepStatus.Missing, "thiếu --project-path ${workspaceFolder} (Cursor sẽ thấy 0 tool)");
            else
                Runner.SetStep(step, StepStatus.Done, "server unity · --project-path ${workspaceFolder}");

            yield break;
        }

        private static IEnumerator CheckUnitySkills(GUMcpInstallStep step)
        {
            var installed = File.Exists(Path.Combine(UserSkillsDir, UnitySkillMarker, SkillFileName));
            Runner.SetStep(step, installed ? StepStatus.Done : StepStatus.Missing,
                installed ? $"{CountSkills(UserSkillsDir)} skill trong ~/.cursor/skills" : "chưa chép");
            yield break;
        }

        private static IEnumerator CheckCursorToolkit(GUMcpInstallStep step)
        {
            var installed = GUCursorRulesInstaller.IsInstalled() && File.Exists(McpRulePath);
            Runner.SetStep(step, installed ? StepStatus.Done : StepStatus.Missing,
                installed ? ".cursor/rules + skills + rule MCP" : "thiếu rules/skills hoặc rule unity-mcp.mdc");
            yield break;
        }

        // ─── Cài ─────────────────────────────────────────────────────────────

        private static IEnumerator ActStep(GUMcpInstallStep step)
        {
            switch (step.Id)
            {
                case StepKind.CursorApp: return Runner.ActEditorReady();
                case StepKind.UnityCli: return Runner.ActUnityCli();
                case StepKind.CursorMcpConfig: return ActMcpConfig(step);
                case StepKind.UnitySkills: return ActUnitySkills();
                case StepKind.CursorToolkit: return ActCursorToolkit();
                case StepKind.Pipeline: return Runner.ActPipeline(step);
                default: return Runner.ActEditorReady();
            }
        }

        /// <summary>
        /// <c>unity mcp configure cursor</c> tạo entry và gộp vào file có sẵn (giữ nguyên server khác) — đã thử thật; vẫn backup
        /// trước vì đây là cấu hình cá nhân ngoài project. Sau đó tự đặt args có <c>--project-path ${workspaceFolder}</c>.
        /// </summary>
        private static IEnumerator ActMcpConfig(GUMcpInstallStep step)
        {
            if (!Runner.RequireUnityCli(step))
                yield break;

            if (File.Exists(McpConfigPath))
            {
                var backup = Path.Combine(CursorHomeDir, McpBackupFileName);
                File.Copy(McpConfigPath, backup, overwrite: true);
                Runner.AppendLog($"Đã backup {McpConfigPath} → {backup}");
            }

            var hasEntry = File.Exists(McpConfigPath) && UnityArgsPattern.IsMatch(File.ReadAllText(McpConfigPath));
            if (!hasEntry)
            {
                yield return Runner.RunCommand(Runner.UnityCliPath, "mcp configure cursor --yes", GUMcpInstallRunner.QuickTimeout, logOutput: true);
                if (!Runner.LastSucceeded)
                    yield break;
            }

            PinProjectPathToWorkspace();
        }

        /// <summary>
        /// CLI không nhận được <c>${workspaceFolder}</c> (nó coi là đường dẫn tương đối và ghi thành <c>/home/&lt;user&gt;/${workspaceFolder}</c>),
        /// nên sửa thẳng mảng args của entry <c>unity</c>, không đụng phần còn lại của file.
        /// </summary>
        private static void PinProjectPathToWorkspace()
        {
            var json = File.ReadAllText(McpConfigPath);
            if (!UnityArgsPattern.IsMatch(json))
            {
                Runner.AppendLog($"Không tìm thấy entry \"{McpServerName}\" có args trong {McpConfigPath} — sửa tay theo guide mục 10.");
                return;
            }

            File.WriteAllText(McpConfigPath, UnityArgsPattern.Replace(json, ReplaceUnityArgs, 1));
            Runner.AppendLog($"Đặt args server {McpServerName} = {ProjectPathArgs} (Cursor chạy server từ thư mục home, thiếu --project-path sẽ không có tool).");
        }

        private static string ReplaceUnityArgs(Match match)
        {
            return match.Groups["head"].Value + ProjectPathArgs;
        }

        /// <summary>
        /// Lấy từ cache plugin của Claude Code nếu máy đã cài (không cần mạng), không thì clone repo plugin.
        /// Skill theo Unity Companion License — chỉ chép vào thư mục của người dùng, không đưa vào repo project.
        /// </summary>
        private static IEnumerator ActUnitySkills()
        {
            var source = FindClaudePluginSkillsDir();
            if (source == null)
            {
                var git = GUExternalCommand.ResolveExecutable("git");
                if (git == null)
                {
                    Runner.AppendLog("Cần Git để tải skill của Unity (hoặc cài plugin unity cho Claude Code trước).");
                    yield break;
                }

                var cloneDir = Path.Combine(Path.GetTempPath(), CloneDirName);
                if (Directory.Exists(cloneDir))
                    Directory.Delete(cloneDir, recursive: true);

                yield return Runner.RunCommand(git, $"clone --depth 1 {PluginRepoUrl} \"{cloneDir}\"", GUMcpInstallRunner.LongTimeout, logOutput: true);
                if (!Runner.LastSucceeded)
                    yield break;

                source = Path.Combine(cloneDir, "skills");
            }

            var copied = CopySkills(source, UserSkillsDir);
            Runner.AppendLog($"Đã chép {copied} skill từ {source} → {UserSkillsDir}");
        }

        private static IEnumerator ActCursorToolkit()
        {
            GUCursorRulesInstaller.InstallAll(overwrite: false, log: true, addIdePackage: false);
            yield break;
        }

        // ─── Tiện ích ────────────────────────────────────────────────────────

        /// <summary>Bản mới nhất trong <c>~/.claude/plugins/cache/unity-agent-plugin/unity/&lt;version&gt;/skills</c>, null nếu không có.</summary>
        private static string FindClaudePluginSkillsDir()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var pluginDir = Path.Combine(home, ".claude", "plugins", "cache", "unity-agent-plugin", "unity");
            if (!Directory.Exists(pluginDir))
                return null;

            string newest = null;
            var newestTime = DateTime.MinValue;
            foreach (var versionDir in Directory.GetDirectories(pluginDir))
            {
                var skills = Path.Combine(versionDir, "skills");
                var time = Directory.GetLastWriteTimeUtc(versionDir);
                if (CountSkills(skills) > 0 && time > newestTime)
                {
                    newest = skills;
                    newestTime = time;
                }
            }

            return newest;
        }

        private static int CopySkills(string sourceDir, string destinationDir)
        {
            var copied = 0;
            foreach (var skillDir in Directory.GetDirectories(sourceDir))
            {
                if (!File.Exists(Path.Combine(skillDir, SkillFileName)))
                    continue;

                GUCursorRulesInstaller.CopyDirectoryRecursive(skillDir, Path.Combine(destinationDir, Path.GetFileName(skillDir)), overwrite: true);
                copied++;
            }

            return copied;
        }

        private static int CountSkills(string skillsDir)
        {
            if (!Directory.Exists(skillsDir))
                return 0;

            var count = 0;
            foreach (var skillDir in Directory.GetDirectories(skillsDir))
            {
                if (File.Exists(Path.Combine(skillDir, SkillFileName)))
                    count++;
            }

            return count;
        }
    }
}
#endif
