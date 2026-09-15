#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using StepKind = GameUp.Core.Editor.GUClaudeUnityMcpStep.Kind;
using StepStatus = GameUp.Core.Editor.GUClaudeUnityMcpStep.Status;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Cài Claude Code × Unity theo <c>Documentation~/unity-mcp-guide.md</c>: plugin skills <c>unity</c>, Unity CLI,
    /// MCP server <c>unity-editor-mcp</c> (các bước của máy dev) và <c>com.unity.pipeline</c> (bước của project).
    /// <para>
    /// Mỗi bước: kiểm tra → cài nếu thiếu → kiểm tra lại; bước đã xong thì bỏ qua nên bấm lại bao nhiêu lần cũng an toàn.
    /// Lệnh ngoài chạy qua <see cref="GUExternalCommand"/> trên <see cref="EditorApplication.update"/>, không chặn Editor.
    /// Cài pipeline làm Editor import + domain reload; cờ trong <see cref="SessionState"/> giúp tiến trình tự chạy tiếp.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class GUClaudeUnityMcpInstaller
    {
        public const string MinClaudeCodeVersion = "2.1.257";
        public const int MinUnityMajorVersion = 6;

        private const string ClaudeCodeSetupUrl = "https://docs.claude.com/en/docs/claude-code/setup";
        private const string MarketplaceSource = "Unity-Technologies/unity-agent-plugin";
        private const string MarketplaceName = "unity-agent-plugin";
        private const string PluginId = "unity@unity-agent-plugin";
        private const string McpServerName = "unity-editor-mcp";
        private const string PipelinePackageName = "com.unity.pipeline";
        private const string UnityCliInstallShUrl = "https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh";
        private const string UnityCliInstallPs1Url = "https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1";
        private const string LogTag = "UnityMCP";

        private const string InstallingKey = "GameUp.Core.UnityMcp.Installing";
        private const string LogKey = "GameUp.Core.UnityMcp.Log";
        private const string ReadyDeadlineKey = "GameUp.Core.UnityMcp.ReadyDeadline";

        private const int MaxLogChars = 20000;
        private const int MaxLoggedOutputLines = 40;
        private const double ReadyPollIntervalSeconds = 5d;

        private static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan LongTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CliInstallTimeout = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan ReadyTimeout = TimeSpan.FromMinutes(5);

        private static readonly Regex VersionPattern = new Regex(@"\d+\.\d+\.\d+(?:-[0-9A-Za-z.]+)?", RegexOptions.Compiled);

        /// <summary>Object JSON của plugin trong <c>claude plugin list --json</c> (không có object lồng nên dừng ở <c>}</c> đầu tiên).</summary>
        private static readonly Regex PluginEntryPattern = new Regex(
            "\"id\"\\s*:\\s*\"" + Regex.Escape(PluginId) + "\"[^}]*", RegexOptions.Compiled);

        private static readonly Regex PluginDisabledPattern = new Regex("\"enabled\"\\s*:\\s*false", RegexOptions.Compiled);

        private static readonly GUClaudeUnityMcpStep[] StepList =
        {
            new GUClaudeUnityMcpStep(StepKind.ClaudeCode, $"Claude Code ≥ {MinClaudeCodeVersion}", true),
            new GUClaudeUnityMcpStep(StepKind.Marketplace, $"Marketplace {MarketplaceSource}", true),
            new GUClaudeUnityMcpStep(StepKind.Plugin, "Plugin unity (skills chính thức của Unity)", true),
            new GUClaudeUnityMcpStep(StepKind.UnityCli, "Unity CLI (lệnh unity mcp)", true),
            new GUClaudeUnityMcpStep(StepKind.McpServer, $"MCP server {McpServerName} (scope user)", true),
            new GUClaudeUnityMcpStep(StepKind.Pipeline, $"Package {PipelinePackageName}", false),
            new GUClaudeUnityMcpStep(StepKind.EditorReady, "Editor ready + MCP Connected", false)
        };

        private static IEnumerator _routine;
        private static GUExternalCommand _command;
        private static bool _commandLogged;
        private static bool _logCommandOutput;
        private static double _waitUntil;
        private static bool _cancelRequested;
        private static string _claudePath;
        private static string _unityCliPath;
        private static bool _pluginDisabled;

        /// <summary>Bắn khi trạng thái bước, hoạt động hiện tại hoặc log đổi — cửa sổ dùng để Repaint.</summary>
        public static event Action Changed;

        public static IReadOnlyList<GUClaudeUnityMcpStep> Steps => StepList;

        public static bool IsBusy => _routine != null;

        public static string CurrentActivity { get; private set; }

        public static string Log => SessionState.GetString(LogKey, string.Empty);

        public static bool HasChecked => StepList[0].State != StepStatus.Unknown;

        /// <summary>
        /// Unity 6 đánh số <c>6000.x</c>, các bản trước theo năm (<c>2022.3</c>, <c>2023.2</c>) — so số đầu với 6 sẽ coi 2022 là "mới hơn".
        /// </summary>
        public static bool IsUnityVersionSupported => ParseLeadingInt(Application.unityVersion) >= MinUnityMajorVersion * 1000;

        static GUClaudeUnityMcpInstaller()
        {
            if (Application.isBatchMode)
                return;

            if (SessionState.GetBool(InstallingKey, false))
                EditorApplication.delayCall += ResumeAfterReload;
        }

        // ─── API ─────────────────────────────────────────────────────────────

        /// <summary>Chỉ kiểm tra trạng thái (không cài gì), dùng khi mở cửa sổ hoặc bấm "Kiểm tra lại".</summary>
        public static void CheckStatus()
        {
            if (IsBusy)
                return;

            StartRoutine(RunChecks());
        }

        public static void Install()
        {
            if (IsBusy)
                return;

            SessionState.SetString(LogKey, string.Empty);
            SessionState.EraseString(ReadyDeadlineKey);
            SessionState.SetBool(InstallingKey, true);
            AppendLog($"Bắt đầu cài Claude Code × Unity — Unity {Application.unityVersion}, {SystemInfo.operatingSystem}.");
            StartRoutine(RunInstall());
        }

        public static void Cancel()
        {
            if (!IsBusy)
                return;

            _cancelRequested = true;
            _command?.Kill();
            AppendLog("Đang dừng theo yêu cầu…");
        }

        public static void ClearLog()
        {
            SessionState.SetString(LogKey, string.Empty);
            NotifyChanged();
        }

        /// <summary>Số bước đã xong trên số bước áp dụng được (bước project không tính khi Unity &lt; 6).</summary>
        public static int CountDone(out int applicable)
        {
            var done = 0;
            applicable = 0;
            foreach (var step in StepList)
            {
                if (!step.PerMachine && !IsUnityVersionSupported)
                    continue;

                applicable++;
                if (step.State == StepStatus.Done)
                    done++;
            }

            return done;
        }

        // ─── Vòng chạy ───────────────────────────────────────────────────────

        private static void ResumeAfterReload()
        {
            if (IsBusy || !SessionState.GetBool(InstallingKey, false))
                return;

            AppendLog("Editor vừa reload — tiếp tục cài.");
            StartRoutine(RunInstall());
        }

        private static void StartRoutine(IEnumerator routine)
        {
            _routine = routine;
            _command = null;
            _cancelRequested = false;
            _waitUntil = 0d;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            NotifyChanged();
        }

        private static void Tick()
        {
            if (_routine == null)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (_command != null)
            {
                if (!_command.Poll())
                    return;

                LogFinishedCommand();
            }

            if (EditorApplication.timeSinceStartup < _waitUntil)
                return;

            bool hasMore;
            try
            {
                hasMore = !_cancelRequested && _routine.MoveNext();
            }
            catch (Exception e)
            {
                AppendLog($"Lỗi không mong đợi: {e.Message}");
                GULogger.Exception(e, LogTag);
                hasMore = false;
            }

            if (hasMore)
                NotifyChanged();
            else
                FinishRoutine();
        }

        private static void FinishRoutine()
        {
            var wasInstalling = SessionState.GetBool(InstallingKey, false);

            _routine = null;
            _command = null;
            CurrentActivity = null;
            EditorApplication.update -= Tick;
            SessionState.SetBool(InstallingKey, false);
            SessionState.EraseString(ReadyDeadlineKey);

            foreach (var step in StepList)
            {
                if (step.State == StepStatus.Running)
                    SetStep(step, StepStatus.Unknown, "đã dừng giữa chừng");
            }

            if (wasInstalling)
            {
                var done = CountDone(out var applicable);
                var summary = _cancelRequested
                    ? $"Đã dừng — {done}/{applicable} bước xong."
                    : $"Kết thúc — {done}/{applicable} bước xong.";
                AppendLog(summary);
                GULogger.Log(LogTag, summary);
            }

            NotifyChanged();
        }

        /// <summary>Bắt đầu một lệnh; routine <c>yield return</c> kết quả này và đọc <see cref="_command"/> khi được chạy tiếp.</summary>
        private static object RunCommand(string executable, string arguments, TimeSpan timeout, bool logOutput)
        {
            AppendLog($"$ {Path.GetFileNameWithoutExtension(executable)} {arguments}");
            _command = GUExternalCommand.Start(executable, arguments, GUClaudeToolkitInstaller.ProjectRoot, timeout);
            _commandLogged = false;
            _logCommandOutput = logOutput;
            return null;
        }

        private static object WaitSeconds(double seconds)
        {
            _waitUntil = EditorApplication.timeSinceStartup + seconds;
            return null;
        }

        private static bool LastSucceeded => _command != null && _command.Succeeded;

        private static void LogFinishedCommand()
        {
            if (_commandLogged)
                return;

            _commandLogged = true;

            if (_command.StartError != null)
            {
                AppendLog($"  → không chạy được: {_command.StartError}");
                return;
            }

            if (_command.TimedOut)
            {
                AppendLog("  → quá thời gian, đã huỷ lệnh");
                return;
            }

            if (_logCommandOutput || _command.ExitCode != 0)
            {
                var tail = TailLines(_command.Output);
                if (tail.Length > 0)
                    AppendLog(tail);
            }

            AppendLog(_command.ExitCode == 0 ? "  → ok" : $"  → lỗi (exit {_command.ExitCode})");
        }

        // ─── Luồng tổng ──────────────────────────────────────────────────────

        private static IEnumerator RunChecks()
        {
            foreach (var step in StepList)
            {
                var check = CheckStep(step, waitUntilReady: false);
                while (check.MoveNext())
                    yield return check.Current;
            }
        }

        private static IEnumerator RunInstall()
        {
            foreach (var step in StepList)
            {
                CurrentActivity = $"Kiểm tra: {step.Title}";
                var check = CheckStep(step, waitUntilReady: true);
                while (check.MoveNext())
                    yield return check.Current;

                if (step.State == StepStatus.Done)
                    continue;

                if (step.State == StepStatus.Blocked)
                {
                    AppendLog($"Bỏ qua \"{step.Title}\": {step.Detail}");
                    // Bước của máy bị chặn (chưa có Claude Code) thì mọi bước sau đều không chạy được.
                    if (step.PerMachine)
                        yield break;

                    continue;
                }

                CurrentActivity = $"Đang cài: {step.Title}";
                AppendLog($"==> {step.Title}");
                var act = ActStep(step);
                while (act.MoveNext())
                    yield return act.Current;

                check = CheckStep(step, waitUntilReady: true);
                while (check.MoveNext())
                    yield return check.Current;

                if (step.State != StepStatus.Done)
                {
                    AppendLog($"Dừng ở \"{step.Title}\": {step.Detail}");
                    yield break;
                }
            }
        }

        // ─── Kiểm tra ────────────────────────────────────────────────────────

        private static IEnumerator CheckStep(GUClaudeUnityMcpStep step, bool waitUntilReady)
        {
            SetStep(step, StepStatus.Running, "đang kiểm tra…");
            switch (step.Id)
            {
                case StepKind.ClaudeCode: return CheckClaudeCode(step);
                case StepKind.Marketplace: return CheckMarketplace(step);
                case StepKind.Plugin: return CheckPlugin(step);
                case StepKind.UnityCli: return CheckUnityCli(step);
                case StepKind.McpServer: return CheckMcpServer(step);
                case StepKind.Pipeline: return CheckPipeline(step);
                default: return CheckEditorReady(step, waitUntilReady);
            }
        }

        private static IEnumerator CheckClaudeCode(GUClaudeUnityMcpStep step)
        {
            _claudePath = GUExternalCommand.ResolveExecutable("claude");
            if (_claudePath == null)
            {
                SetStep(step, StepStatus.Blocked, $"chưa cài Claude Code — xem {ClaudeCodeSetupUrl}");
                yield break;
            }

            yield return RunCommand(_claudePath, "--version", QuickTimeout, logOutput: false);
            var version = LastSucceeded ? MatchVersion(_command.Output) : null;
            if (version == null)
            {
                SetStep(step, StepStatus.Failed, "không đọc được version — xem log");
                yield break;
            }

            var upToDate = CompareVersions(version, MinClaudeCodeVersion) >= 0;
            SetStep(step, upToDate ? StepStatus.Done : StepStatus.Missing,
                upToDate ? version : $"{version} < {MinClaudeCodeVersion}, cần update");
        }

        private static IEnumerator CheckMarketplace(GUClaudeUnityMcpStep step)
        {
            if (!RequireClaude(step))
                yield break;

            yield return RunCommand(_claudePath, "plugin marketplace list", QuickTimeout, logOutput: false);
            if (!LastSucceeded)
            {
                SetStep(step, StepStatus.Failed, "không liệt kê được marketplace — xem log");
                yield break;
            }

            var added = _command.Output.IndexOf(MarketplaceName, StringComparison.Ordinal) >= 0;
            SetStep(step, added ? StepStatus.Done : StepStatus.Missing, added ? "đã thêm" : "chưa thêm");
        }

        private static IEnumerator CheckPlugin(GUClaudeUnityMcpStep step)
        {
            if (!RequireClaude(step))
                yield break;

            yield return RunCommand(_claudePath, "plugin list --json", QuickTimeout, logOutput: false);
            if (!LastSucceeded)
            {
                SetStep(step, StepStatus.Failed, "không liệt kê được plugin — xem log");
                yield break;
            }

            var entry = PluginEntryPattern.Match(_command.Output);
            if (!entry.Success)
            {
                _pluginDisabled = false;
                SetStep(step, StepStatus.Missing, "chưa cài");
                yield break;
            }

            _pluginDisabled = PluginDisabledPattern.IsMatch(entry.Value);
            var version = MatchVersion(entry.Value);
            SetStep(step, _pluginDisabled ? StepStatus.Missing : StepStatus.Done,
                _pluginDisabled ? "đã cài nhưng đang tắt" : $"v{version} · enabled");
        }

        private static IEnumerator CheckUnityCli(GUClaudeUnityMcpStep step)
        {
            _unityCliPath = GUExternalCommand.ResolveExecutable("unity");
            if (_unityCliPath == null)
            {
                SetStep(step, StepStatus.Missing, "chưa cài");
                yield break;
            }

            yield return RunCommand(_unityCliPath, "--version", QuickTimeout, logOutput: false);
            var version = LastSucceeded ? MatchVersion(_command.Output) : null;
            SetStep(step, version != null ? StepStatus.Done : StepStatus.Failed,
                version ?? $"{_unityCliPath} không chạy được --version");
        }

        private static IEnumerator CheckMcpServer(GUClaudeUnityMcpStep step)
        {
            if (!RequireClaude(step))
                yield break;

            yield return RunCommand(_claudePath, $"mcp get {McpServerName}", QuickTimeout, logOutput: false);
            if (_command.StartError != null || _command.TimedOut)
            {
                SetStep(step, StepStatus.Failed, "không chạy được claude mcp get — xem log");
                yield break;
            }

            if (_command.ExitCode != 0)
            {
                SetStep(step, StepStatus.Missing, "chưa đăng ký");
                yield break;
            }

            var connected = _command.Output.IndexOf("Connected", StringComparison.Ordinal) >= 0;
            SetStep(step, StepStatus.Done, connected ? "đã đăng ký · Connected" : "đã đăng ký · chưa có Editor kết nối");
        }

        private static IEnumerator CheckPipeline(GUClaudeUnityMcpStep step)
        {
            if (!IsUnityVersionSupported)
            {
                SetStep(step, StepStatus.Blocked, $"Unity {Application.unityVersion} — cần Unity {MinUnityMajorVersion}.0+");
                yield break;
            }

            if (!IsPipelineInManifest())
            {
                SetStep(step, StepStatus.Missing, "chưa có trong manifest.json");
                yield break;
            }

            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{PipelinePackageName}");
            SetStep(step, StepStatus.Done, info != null ? $"v{info.version}" : "có trong manifest.json, đang import");
        }

        private static IEnumerator CheckEditorReady(GUClaudeUnityMcpStep step, bool waitUntilReady)
        {
            if (!IsUnityVersionSupported)
            {
                SetStep(step, StepStatus.Blocked, $"cần Unity {MinUnityMajorVersion}.0+");
                yield break;
            }

            if (!IsPipelineInManifest())
            {
                SetStep(step, StepStatus.Blocked, $"cần {PipelinePackageName} trước");
                yield break;
            }

            if (!RequireUnityCli(step) || !RequireClaude(step))
                yield break;

            var deadline = waitUntilReady ? GetOrCreateReadyDeadline() : DateTime.UtcNow;
            while (true)
            {
                yield return RunCommand(_unityCliPath, "status --json", QuickTimeout, logOutput: false);
                var editorReady = IsThisEditorReady(_command.Output);

                var connected = false;
                if (editorReady)
                {
                    yield return RunCommand(_claudePath, $"mcp get {McpServerName}", QuickTimeout, logOutput: false);
                    connected = _command.Output.IndexOf("Connected", StringComparison.Ordinal) >= 0;
                }

                if (editorReady && connected)
                {
                    SetStep(step, StepStatus.Done, "Editor ready · MCP Connected");
                    yield break;
                }

                var reason = editorReady
                    ? "Editor ready nhưng MCP chưa Connected"
                    : "Pipeline trong Editor chưa sẵn sàng (đang import/compile, hoặc Safe Mode)";

                if (DateTime.UtcNow >= deadline)
                {
                    SetStep(step, StepStatus.Missing, reason);
                    yield break;
                }

                SetStep(step, StepStatus.Running, $"{reason} — thử lại sau {ReadyPollIntervalSeconds:0}s");
                yield return WaitSeconds(ReadyPollIntervalSeconds);
            }
        }

        // ─── Cài ─────────────────────────────────────────────────────────────

        private static IEnumerator ActStep(GUClaudeUnityMcpStep step)
        {
            SetStep(step, StepStatus.Running, "đang cài…");
            switch (step.Id)
            {
                case StepKind.ClaudeCode: return ActClaudeCode();
                case StepKind.Marketplace: return ActMarketplace();
                case StepKind.Plugin: return ActPlugin();
                case StepKind.UnityCli: return ActUnityCli();
                case StepKind.McpServer: return ActMcpServer(step);
                case StepKind.Pipeline: return ActPipeline(step);
                default: return ActEditorReady();
            }
        }

        private static IEnumerator ActClaudeCode()
        {
            yield return RunCommand(_claudePath, "update", LongTimeout, logOutput: true);
        }

        private static IEnumerator ActMarketplace()
        {
            yield return RunCommand(_claudePath, $"plugin marketplace add {MarketplaceSource}", LongTimeout, logOutput: true);
        }

        private static IEnumerator ActPlugin()
        {
            // -y: bắt buộc khi stdin/stdout không phải TTY (tiến trình con của Editor).
            var arguments = _pluginDisabled
                ? $"plugin enable {PluginId}"
                : $"plugin install {PluginId} --scope user -y";
            yield return RunCommand(_claudePath, arguments, LongTimeout, logOutput: true);
        }

        private static IEnumerator ActUnityCli()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                yield return RunCommand(
                    "powershell",
                    $"-NoProfile -ExecutionPolicy Bypass -Command \"$env:UNITY_CLI_CHANNEL='beta'; irm {UnityCliInstallPs1Url} | iex\"",
                    CliInstallTimeout,
                    logOutput: true);
            }
            else
            {
                yield return RunCommand(
                    "/bin/bash",
                    $"-c \"curl -fsSL {UnityCliInstallShUrl} | UNITY_CLI_CHANNEL=beta bash\"",
                    CliInstallTimeout,
                    logOutput: true);
            }
        }

        private static IEnumerator ActMcpServer(GUClaudeUnityMcpStep step)
        {
            if (!RequireUnityCli(step))
                yield break;

            // Đường dẫn tuyệt đối: Claude Code mở từ IDE/launcher có thể không có ~/.local/bin trong PATH.
            yield return RunCommand(
                _claudePath,
                $"mcp add --scope user --transport stdio {McpServerName} \"{_unityCliPath}\" mcp",
                QuickTimeout,
                logOutput: true);
        }

        private static IEnumerator ActPipeline(GUClaudeUnityMcpStep step)
        {
            if (!RequireUnityCli(step))
                yield break;

            yield return RunCommand(
                _unityCliPath,
                $"pipeline install --project-path \"{GUClaudeToolkitInstaller.ProjectRoot}\" --non-interactive",
                LongTimeout,
                logOutput: true);

            if (!LastSucceeded)
                yield break;

            AppendLog($"Unity sẽ import {PipelinePackageName} và compile lại — trình cài tự chạy tiếp sau khi Editor reload.");
            UnityEditor.PackageManager.Client.Resolve();
            AssetDatabase.Refresh();
        }

        private static IEnumerator ActEditorReady()
        {
            AppendLog("Editor chưa sẵn sàng: xem Console có lỗi compile không (Pipeline không load trong Safe Mode), sửa xong bấm Cài đặt lại.");
            yield break;
        }

        // ─── Tiện ích ────────────────────────────────────────────────────────

        private static bool RequireClaude(GUClaudeUnityMcpStep step)
        {
            _claudePath = _claudePath ?? GUExternalCommand.ResolveExecutable("claude");
            if (_claudePath != null)
                return true;

            SetStep(step, StepStatus.Blocked, "cần Claude Code");
            return false;
        }

        private static bool RequireUnityCli(GUClaudeUnityMcpStep step)
        {
            _unityCliPath = _unityCliPath ?? GUExternalCommand.ResolveExecutable("unity");
            if (_unityCliPath != null)
                return true;

            SetStep(step, StepStatus.Blocked, "cần Unity CLI");
            return false;
        }

        private static bool IsPipelineInManifest()
        {
            var manifestPath = Path.Combine(GUClaudeToolkitInstaller.ProjectRoot, "Packages", "manifest.json");
            return File.Exists(manifestPath)
                   && File.ReadAllText(manifestPath).IndexOf($"\"{PipelinePackageName}\"", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// <c>unity status --json</c> liệt kê các Editor có Pipeline; coi là sẵn sàng khi có instance của project này ở trạng thái ready.
        /// So khớp chuỗi thay vì parse schema vì CLI đang beta và tên field có thể đổi.
        /// </summary>
        private static bool IsThisEditorReady(string statusJson)
        {
            var normalized = statusJson.Replace("\\\\", "/").Replace('\\', '/');
            var project = GUClaudeToolkitInstaller.ProjectRoot.Replace('\\', '/');
            return normalized.IndexOf(project, StringComparison.OrdinalIgnoreCase) >= 0
                   && normalized.IndexOf("ready", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Hạn chờ Editor ready lưu qua domain reload, để reload không kéo dài thời gian chờ mãi.</summary>
        private static DateTime GetOrCreateReadyDeadline()
        {
            if (long.TryParse(SessionState.GetString(ReadyDeadlineKey, string.Empty), out var ticks))
                return new DateTime(ticks, DateTimeKind.Utc);

            var deadline = DateTime.UtcNow + ReadyTimeout;
            SessionState.SetString(ReadyDeadlineKey, deadline.Ticks.ToString());
            return deadline;
        }

        private static void SetStep(GUClaudeUnityMcpStep step, StepStatus state, string detail)
        {
            step.State = state;
            step.Detail = detail;
            NotifyChanged();
        }

        private static void AppendLog(string text)
        {
            var log = $"{SessionState.GetString(LogKey, string.Empty)}[{DateTime.Now:HH:mm:ss}] {text}\n";
            if (log.Length > MaxLogChars)
                log = log.Substring(log.Length - MaxLogChars);

            SessionState.SetString(LogKey, log);
            NotifyChanged();
        }

        private static string TailLines(string output)
        {
            var lines = output.Replace("\r", string.Empty).TrimEnd('\n').Split('\n');
            var start = Math.Max(0, lines.Length - MaxLoggedOutputLines);
            var sb = new StringBuilder();
            for (var i = start; i < lines.Length; i++)
            {
                if (lines[i].Trim().Length > 0)
                    sb.Append("    ").AppendLine(lines[i]);
            }

            return sb.ToString().TrimEnd();
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private static string MatchVersion(string text)
        {
            var match = VersionPattern.Match(text);
            return match.Success ? match.Value : null;
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

        private static int ParseLeadingInt(string text)
        {
            var match = Regex.Match(text, @"^\d+");
            return match.Success && int.TryParse(match.Value, out var value) ? value : 0;
        }
    }
}
#endif
