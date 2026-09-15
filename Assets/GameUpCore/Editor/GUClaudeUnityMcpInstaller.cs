#if UNITY_EDITOR
using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using StepKind = GameUp.Core.Editor.GUMcpInstallStep.Kind;
using StepStatus = GameUp.Core.Editor.GUMcpInstallStep.Status;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Cài Claude Code × Unity theo <c>Documentation~/unity-mcp-guide.md</c>: plugin skills <c>unity</c>, Unity CLI,
    /// MCP server <c>unity-editor-mcp</c> (các bước của máy dev) và <c>com.unity.pipeline</c> (bước của project).
    /// Máy chạy và bước dùng chung nằm ở <see cref="GUMcpInstallRunner"/>; file này chỉ khai báo bước riêng của Claude Code.
    /// </summary>
    [InitializeOnLoad]
    public static class GUClaudeUnityMcpInstaller
    {
        public const string MinClaudeCodeVersion = "2.1.257";

        private const string ClaudeCodeSetupUrl = "https://docs.claude.com/en/docs/claude-code/setup";
        private const string MarketplaceSource = "Unity-Technologies/unity-agent-plugin";
        private const string MarketplaceName = "unity-agent-plugin";
        private const string PluginId = "unity@unity-agent-plugin";
        private const string McpServerName = "unity-editor-mcp";

        /// <summary>Object JSON của plugin trong <c>claude plugin list --json</c> (không có object lồng nên dừng ở <c>}</c> đầu tiên).</summary>
        private static readonly Regex PluginEntryPattern = new Regex(
            "\"id\"\\s*:\\s*\"" + Regex.Escape(PluginId) + "\"[^}]*", RegexOptions.Compiled);

        private static readonly Regex PluginDisabledPattern = new Regex("\"enabled\"\\s*:\\s*false", RegexOptions.Compiled);

        private static readonly GUMcpInstallStep[] StepList =
        {
            new GUMcpInstallStep(StepKind.ClaudeCode, $"Claude Code ≥ {MinClaudeCodeVersion}", perMachine: true),
            new GUMcpInstallStep(StepKind.Marketplace, $"Marketplace {MarketplaceSource}", perMachine: true),
            new GUMcpInstallStep(StepKind.Plugin, "Plugin unity (skills chính thức của Unity)", perMachine: true),
            new GUMcpInstallStep(StepKind.UnityCli, "Unity CLI (lệnh unity mcp)", perMachine: true),
            new GUMcpInstallStep(StepKind.McpServer, $"MCP server {McpServerName} (scope user)", perMachine: true),
            new GUMcpInstallStep(StepKind.Pipeline, "Package com.unity.pipeline", perMachine: false, requiresUnity6: true),
            new GUMcpInstallStep(StepKind.EditorReady, "Editor ready + MCP Connected", perMachine: false, requiresUnity6: true)
        };

        public static readonly GUMcpInstallRunner Runner =
            new GUMcpInstallRunner("GameUp.Core.UnityMcp", "UnityMCP", StepList, CheckStep, ActStep);

        private static string _claudePath;
        private static bool _pluginDisabled;

        static GUClaudeUnityMcpInstaller()
        {
            if (!Application.isBatchMode)
                EditorApplication.delayCall += Runner.ResumeIfInstalling;
        }

        // ─── Kiểm tra ────────────────────────────────────────────────────────

        private static IEnumerator CheckStep(GUMcpInstallStep step, bool waitUntilReady)
        {
            switch (step.Id)
            {
                case StepKind.ClaudeCode: return CheckClaudeCode(step);
                case StepKind.Marketplace: return CheckMarketplace(step);
                case StepKind.Plugin: return CheckPlugin(step);
                case StepKind.UnityCli: return Runner.CheckUnityCli(step);
                case StepKind.McpServer: return CheckMcpServer(step);
                case StepKind.Pipeline: return Runner.CheckPipeline(step);
                default: return CheckEditorConnected(step, waitUntilReady);
            }
        }

        private static IEnumerator CheckClaudeCode(GUMcpInstallStep step)
        {
            _claudePath = GUExternalCommand.ResolveExecutable("claude");
            if (_claudePath == null)
            {
                Runner.SetStep(step, StepStatus.Blocked, $"chưa cài Claude Code — xem {ClaudeCodeSetupUrl}");
                yield break;
            }

            yield return Runner.RunCommand(_claudePath, "--version", GUMcpInstallRunner.QuickTimeout, logOutput: false);
            var version = Runner.LastSucceeded ? GUMcpInstallRunner.MatchVersion(Runner.LastCommand.Output) : null;
            if (version == null)
            {
                Runner.SetStep(step, StepStatus.Failed, "không đọc được version — xem log");
                yield break;
            }

            var upToDate = CompareVersions(version, MinClaudeCodeVersion) >= 0;
            Runner.SetStep(step, upToDate ? StepStatus.Done : StepStatus.Missing,
                upToDate ? version : $"{version} < {MinClaudeCodeVersion}, cần update");
        }

        private static IEnumerator CheckMarketplace(GUMcpInstallStep step)
        {
            if (!RequireClaude(step))
                yield break;

            yield return Runner.RunCommand(_claudePath, "plugin marketplace list", GUMcpInstallRunner.QuickTimeout, logOutput: false);
            if (!Runner.LastSucceeded)
            {
                Runner.SetStep(step, StepStatus.Failed, "không liệt kê được marketplace — xem log");
                yield break;
            }

            var added = Runner.LastCommand.Output.IndexOf(MarketplaceName, StringComparison.Ordinal) >= 0;
            Runner.SetStep(step, added ? StepStatus.Done : StepStatus.Missing, added ? "đã thêm" : "chưa thêm");
        }

        private static IEnumerator CheckPlugin(GUMcpInstallStep step)
        {
            if (!RequireClaude(step))
                yield break;

            yield return Runner.RunCommand(_claudePath, "plugin list --json", GUMcpInstallRunner.QuickTimeout, logOutput: false);
            if (!Runner.LastSucceeded)
            {
                Runner.SetStep(step, StepStatus.Failed, "không liệt kê được plugin — xem log");
                yield break;
            }

            var entry = PluginEntryPattern.Match(Runner.LastCommand.Output);
            if (!entry.Success)
            {
                _pluginDisabled = false;
                Runner.SetStep(step, StepStatus.Missing, "chưa cài");
                yield break;
            }

            _pluginDisabled = PluginDisabledPattern.IsMatch(entry.Value);
            var version = GUMcpInstallRunner.MatchVersion(entry.Value);
            Runner.SetStep(step, _pluginDisabled ? StepStatus.Missing : StepStatus.Done,
                _pluginDisabled ? "đã cài nhưng đang tắt" : $"v{version} · enabled");
        }

        private static IEnumerator CheckMcpServer(GUMcpInstallStep step)
        {
            if (!RequireClaude(step))
                yield break;

            yield return Runner.RunCommand(_claudePath, $"mcp get {McpServerName}", GUMcpInstallRunner.QuickTimeout, logOutput: false);
            var command = Runner.LastCommand;
            if (command.StartError != null || command.TimedOut)
            {
                Runner.SetStep(step, StepStatus.Failed, "không chạy được claude mcp get — xem log");
                yield break;
            }

            if (command.ExitCode != 0)
            {
                Runner.SetStep(step, StepStatus.Missing, "chưa đăng ký");
                yield break;
            }

            var connected = command.Output.IndexOf("Connected", StringComparison.Ordinal) >= 0;
            Runner.SetStep(step, StepStatus.Done, connected ? "đã đăng ký · Connected" : "đã đăng ký · chưa có Editor kết nối");
        }

        /// <summary>Editor ready (bước chung) rồi xác nhận thêm Claude Code thấy MCP Connected.</summary>
        private static IEnumerator CheckEditorConnected(GUMcpInstallStep step, bool waitUntilReady)
        {
            var ready = Runner.CheckEditorReady(step, waitUntilReady, "Editor ready");
            while (ready.MoveNext())
                yield return ready.Current;

            if (step.State != StepStatus.Done || !RequireClaude(step))
                yield break;

            yield return Runner.RunCommand(_claudePath, $"mcp get {McpServerName}", GUMcpInstallRunner.QuickTimeout, logOutput: false);
            var connected = Runner.LastCommand.Output.IndexOf("Connected", StringComparison.Ordinal) >= 0;
            Runner.SetStep(step, connected ? StepStatus.Done : StepStatus.Missing,
                connected ? "Editor ready · MCP Connected" : "Editor ready nhưng MCP chưa Connected");
        }

        // ─── Cài ─────────────────────────────────────────────────────────────

        private static IEnumerator ActStep(GUMcpInstallStep step)
        {
            switch (step.Id)
            {
                case StepKind.ClaudeCode: return ActClaudeCode();
                case StepKind.Marketplace: return ActMarketplace();
                case StepKind.Plugin: return ActPlugin();
                case StepKind.UnityCli: return Runner.ActUnityCli();
                case StepKind.McpServer: return ActMcpServer(step);
                case StepKind.Pipeline: return Runner.ActPipeline(step);
                default: return Runner.ActEditorReady();
            }
        }

        private static IEnumerator ActClaudeCode()
        {
            yield return Runner.RunCommand(_claudePath, "update", GUMcpInstallRunner.LongTimeout, logOutput: true);
        }

        private static IEnumerator ActMarketplace()
        {
            yield return Runner.RunCommand(_claudePath, $"plugin marketplace add {MarketplaceSource}", GUMcpInstallRunner.LongTimeout, logOutput: true);
        }

        private static IEnumerator ActPlugin()
        {
            // -y: bắt buộc khi stdin/stdout không phải TTY (tiến trình con của Editor).
            var arguments = _pluginDisabled
                ? $"plugin enable {PluginId}"
                : $"plugin install {PluginId} --scope user -y";
            yield return Runner.RunCommand(_claudePath, arguments, GUMcpInstallRunner.LongTimeout, logOutput: true);
        }

        private static IEnumerator ActMcpServer(GUMcpInstallStep step)
        {
            if (!Runner.RequireUnityCli(step))
                yield break;

            // Đường dẫn tuyệt đối: Claude Code mở từ IDE/launcher có thể không có ~/.local/bin trong PATH.
            yield return Runner.RunCommand(
                _claudePath,
                $"mcp add --scope user --transport stdio {McpServerName} \"{Runner.UnityCliPath}\" mcp",
                GUMcpInstallRunner.QuickTimeout,
                logOutput: true);
        }

        // ─── Tiện ích ────────────────────────────────────────────────────────

        private static bool RequireClaude(GUMcpInstallStep step)
        {
            _claudePath = _claudePath ?? GUExternalCommand.ResolveExecutable("claude");
            if (_claudePath != null)
                return true;

            Runner.SetStep(step, StepStatus.Blocked, "cần Claude Code");
            return false;
        }

        private static int CompareVersions(string left, string right)
        {
            var a = ParseVersionNumbers(left);
            var b = ParseVersionNumbers(right);
            for (var i = 0; i < a.Length; i++)
            {
                var compared = a[i].CompareTo(b[i]);
                if (compared != 0)
                    return compared;
            }

            return 0;
        }

        private static int[] ParseVersionNumbers(string version)
        {
            var numbers = new int[3];
            var matches = Regex.Matches(version, @"\d+");
            for (var i = 0; i < numbers.Length && i < matches.Count; i++)
                int.TryParse(matches[i].Value, out numbers[i]);

            return numbers;
        }
    }
}
#endif
