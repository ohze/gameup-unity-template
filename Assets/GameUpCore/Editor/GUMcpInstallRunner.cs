#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using StepStatus = GameUp.Core.Editor.GUMcpInstallStep.Status;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Máy chạy chung cho các trình cài "IDE × Unity MCP" (Claude Code, Cursor).
    /// <para>
    /// Mỗi bước: kiểm tra → cài nếu thiếu → kiểm tra lại; bước đã xong thì bỏ qua nên bấm lại bao nhiêu lần cũng an toàn.
    /// Lệnh ngoài chạy qua <see cref="GUExternalCommand"/> trên <see cref="EditorApplication.update"/>, không chặn Editor.
    /// Cờ "đang cài" và log nằm trong <see cref="SessionState"/> nên cài <c>com.unity.pipeline</c> (gây domain reload)
    /// xong vẫn tự chạy tiếp qua <see cref="ResumeIfInstalling"/>.
    /// </para>
    /// Kèm các bước dùng chung mọi IDE: Unity CLI, <c>com.unity.pipeline</c>, Editor ready.
    /// </summary>
    public sealed class GUMcpInstallRunner
    {
        public const int MinUnityMajorVersion = 6;

        public static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(90);
        public static readonly TimeSpan LongTimeout = TimeSpan.FromMinutes(5);

        private const string PipelinePackageName = "com.unity.pipeline";
        private const string UnityCliInstallShUrl = "https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh";
        private const string UnityCliInstallPs1Url = "https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1";
        private const int MaxLogChars = 20000;
        private const int MaxLoggedOutputLines = 40;
        private const double ReadyPollIntervalSeconds = 5d;

        private static readonly TimeSpan CliInstallTimeout = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan ReadyTimeout = TimeSpan.FromMinutes(5);
        private static readonly Regex VersionPattern = new Regex(@"\d+\.\d+\.\d+(?:-[0-9A-Za-z.]+)?", RegexOptions.Compiled);

        private readonly GUMcpInstallStep[] _steps;
        private readonly Func<GUMcpInstallStep, bool, IEnumerator> _checkStep;
        private readonly Func<GUMcpInstallStep, IEnumerator> _actStep;
        private readonly string _logTag;
        private readonly string _installingKey;
        private readonly string _logKey;
        private readonly string _readyDeadlineKey;

        private IEnumerator _routine;
        private GUExternalCommand _command;
        private bool _commandLogged;
        private bool _logCommandOutput;
        private double _waitUntil;
        private bool _cancelRequested;

        /// <param name="sessionKeyPrefix">Tiền tố khoá <see cref="SessionState"/>, riêng cho từng IDE để hai trình cài không đè log nhau.</param>
        /// <param name="logTag">Tag của <see cref="GULogger"/> khi báo kết quả.</param>
        /// <param name="steps">Các bước theo thứ tự chạy.</param>
        /// <param name="checkStep">Kiểm tra một bước; tham số bool = được phép chờ (chỉ khi đang cài).</param>
        /// <param name="actStep">Cài một bước đang thiếu.</param>
        public GUMcpInstallRunner(
            string sessionKeyPrefix,
            string logTag,
            GUMcpInstallStep[] steps,
            Func<GUMcpInstallStep, bool, IEnumerator> checkStep,
            Func<GUMcpInstallStep, IEnumerator> actStep)
        {
            _steps = steps;
            _checkStep = checkStep;
            _actStep = actStep;
            _logTag = logTag;
            _installingKey = $"{sessionKeyPrefix}.Installing";
            _logKey = $"{sessionKeyPrefix}.Log";
            _readyDeadlineKey = $"{sessionKeyPrefix}.ReadyDeadline";
        }

        /// <summary>Bắn khi trạng thái bước, hoạt động hiện tại hoặc log đổi — cửa sổ dùng để Repaint.</summary>
        public event Action Changed;

        public IReadOnlyList<GUMcpInstallStep> Steps => _steps;

        public bool IsBusy => _routine != null;

        public string CurrentActivity { get; private set; }

        public string Log => SessionState.GetString(_logKey, string.Empty);

        public bool HasChecked => _steps[0].State != StepStatus.Unknown;

        /// <summary>Đường dẫn Unity CLI tìm được ở bước Unity CLI; null nếu chưa kiểm tra hoặc chưa cài.</summary>
        public string UnityCliPath { get; private set; }

        /// <summary>Lệnh vừa chạy xong — step code đọc kết quả sau khi <c>yield return</c> <see cref="RunCommand"/>.</summary>
        public GUExternalCommand LastCommand => _command;

        public bool LastSucceeded => _command != null && _command.Succeeded;

        /// <summary>
        /// Unity 6 đánh số <c>6000.x</c>, các bản trước theo năm (<c>2022.3</c>, <c>2023.2</c>) — so số đầu với 6 sẽ coi 2022 là "mới hơn".
        /// </summary>
        public static bool IsUnityVersionSupported => ParseLeadingInt(Application.unityVersion) >= MinUnityMajorVersion * 1000;

        // ─── API ─────────────────────────────────────────────────────────────

        /// <summary>Chỉ kiểm tra trạng thái (không cài gì), dùng khi mở cửa sổ hoặc bấm "Kiểm tra lại".</summary>
        public void CheckStatus()
        {
            if (IsBusy)
                return;

            StartRoutine(RunChecks());
        }

        public void Install(string heading)
        {
            if (IsBusy)
                return;

            SessionState.SetString(_logKey, string.Empty);
            SessionState.EraseString(_readyDeadlineKey);
            SessionState.SetBool(_installingKey, true);
            AppendLog($"Bắt đầu cài {heading} — Unity {Application.unityVersion}, {SystemInfo.operatingSystem}.");
            StartRoutine(RunInstall());
        }

        public void Cancel()
        {
            if (!IsBusy)
                return;

            _cancelRequested = true;
            _command?.Kill();
            AppendLog("Đang dừng theo yêu cầu…");
        }

        public void ClearLog()
        {
            SessionState.SetString(_logKey, string.Empty);
            NotifyChanged();
        }

        /// <summary>Số bước đã xong trên số bước áp dụng được (bước cần Unity 6 không tính khi Unity cũ hơn).</summary>
        public int CountDone(out int applicable)
        {
            var done = 0;
            applicable = 0;
            foreach (var step in _steps)
            {
                if (step.RequiresUnity6 && !IsUnityVersionSupported)
                    continue;

                applicable++;
                if (step.State == StepStatus.Done)
                    done++;
            }

            return done;
        }

        /// <summary>Gọi từ <c>EditorApplication.delayCall</c> sau khi Editor nạp: tiếp tục nếu lần cài trước bị domain reload cắt ngang.</summary>
        public void ResumeIfInstalling()
        {
            if (IsBusy || !SessionState.GetBool(_installingKey, false))
                return;

            AppendLog("Editor vừa reload — tiếp tục cài.");
            StartRoutine(RunInstall());
        }

        // ─── Dành cho code của từng bước ─────────────────────────────────────

        /// <summary>Bắt đầu một lệnh; routine <c>yield return</c> kết quả này rồi đọc <see cref="LastCommand"/> khi được chạy tiếp.</summary>
        public object RunCommand(string executable, string arguments, TimeSpan timeout, bool logOutput)
        {
            AppendLog($"$ {Path.GetFileNameWithoutExtension(executable)} {arguments}");
            _command = GUExternalCommand.Start(executable, arguments, GUClaudeToolkitInstaller.ProjectRoot, timeout);
            _commandLogged = false;
            _logCommandOutput = logOutput;
            return null;
        }

        public object WaitSeconds(double seconds)
        {
            _waitUntil = EditorApplication.timeSinceStartup + seconds;
            return null;
        }

        public void SetStep(GUMcpInstallStep step, StepStatus state, string detail)
        {
            step.State = state;
            step.Detail = detail;
            NotifyChanged();
        }

        public void AppendLog(string text)
        {
            var log = $"{SessionState.GetString(_logKey, string.Empty)}[{DateTime.Now:HH:mm:ss}] {text}\n";
            if (log.Length > MaxLogChars)
                log = log.Substring(log.Length - MaxLogChars);

            SessionState.SetString(_logKey, log);
            NotifyChanged();
        }

        public bool RequireUnityCli(GUMcpInstallStep step)
        {
            UnityCliPath = UnityCliPath ?? GUExternalCommand.ResolveExecutable("unity");
            if (UnityCliPath != null)
                return true;

            SetStep(step, StepStatus.Blocked, "cần Unity CLI");
            return false;
        }

        public static string MatchVersion(string text)
        {
            var match = VersionPattern.Match(text);
            return match.Success ? match.Value : null;
        }

        public static bool IsPipelineInManifest()
        {
            var manifestPath = Path.Combine(GUClaudeToolkitInstaller.ProjectRoot, "Packages", "manifest.json");
            return File.Exists(manifestPath)
                   && File.ReadAllText(manifestPath).IndexOf($"\"{PipelinePackageName}\"", StringComparison.Ordinal) >= 0;
        }

        // ─── Bước dùng chung mọi IDE ─────────────────────────────────────────

        public IEnumerator CheckUnityCli(GUMcpInstallStep step)
        {
            UnityCliPath = GUExternalCommand.ResolveExecutable("unity");
            if (UnityCliPath == null)
            {
                SetStep(step, StepStatus.Missing, "chưa cài");
                yield break;
            }

            yield return RunCommand(UnityCliPath, "--version", QuickTimeout, logOutput: false);
            var version = LastSucceeded ? MatchVersion(_command.Output) : null;
            SetStep(step, version != null ? StepStatus.Done : StepStatus.Failed,
                version ?? $"{UnityCliPath} không chạy được --version");
        }

        public IEnumerator ActUnityCli()
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

        public IEnumerator CheckPipeline(GUMcpInstallStep step)
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

        public IEnumerator ActPipeline(GUMcpInstallStep step)
        {
            if (!RequireUnityCli(step))
                yield break;

            yield return RunCommand(
                UnityCliPath,
                $"pipeline install --project-path \"{GUClaudeToolkitInstaller.ProjectRoot}\" --non-interactive",
                LongTimeout,
                logOutput: true);

            if (!LastSucceeded)
                yield break;

            AppendLog($"Unity sẽ import {PipelinePackageName} và compile lại — trình cài tự chạy tiếp sau khi Editor reload.");
            UnityEditor.PackageManager.Client.Resolve();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Chờ Pipeline trong Editor này báo ready (tối đa 5 phút khi <paramref name="waitUntilReady"/>).
        /// Việc xác nhận IDE đã kết nối MCP là của từng IDE, chạy sau khi bước này Done.
        /// </summary>
        public IEnumerator CheckEditorReady(GUMcpInstallStep step, bool waitUntilReady, string readyDetail)
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

            if (!RequireUnityCli(step))
                yield break;

            var deadline = waitUntilReady ? GetOrCreateReadyDeadline() : DateTime.UtcNow;
            while (true)
            {
                yield return RunCommand(UnityCliPath, "status --json", QuickTimeout, logOutput: false);
                if (IsThisEditorReady(_command.Output))
                {
                    SetStep(step, StepStatus.Done, readyDetail);
                    yield break;
                }

                const string reason = "Pipeline trong Editor chưa sẵn sàng (đang import/compile, hoặc Safe Mode)";
                if (DateTime.UtcNow >= deadline)
                {
                    SetStep(step, StepStatus.Missing, reason);
                    yield break;
                }

                SetStep(step, StepStatus.Running, $"{reason} — thử lại sau {ReadyPollIntervalSeconds:0}s");
                yield return WaitSeconds(ReadyPollIntervalSeconds);
            }
        }

        public IEnumerator ActEditorReady()
        {
            AppendLog("Editor chưa sẵn sàng: xem Console có lỗi compile không (Pipeline không load trong Safe Mode), sửa xong bấm Cài đặt lại.");
            yield break;
        }

        // ─── Vòng chạy ───────────────────────────────────────────────────────

        private void StartRoutine(IEnumerator routine)
        {
            _routine = routine;
            _command = null;
            _cancelRequested = false;
            _waitUntil = 0d;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            NotifyChanged();
        }

        private void Tick()
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
                GULogger.Exception(e, _logTag);
                hasMore = false;
            }

            if (hasMore)
                NotifyChanged();
            else
                FinishRoutine();
        }

        private void FinishRoutine()
        {
            var wasInstalling = SessionState.GetBool(_installingKey, false);

            _routine = null;
            _command = null;
            CurrentActivity = null;
            EditorApplication.update -= Tick;
            SessionState.SetBool(_installingKey, false);
            SessionState.EraseString(_readyDeadlineKey);

            foreach (var step in _steps)
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
                GULogger.Log(_logTag, summary);
            }

            NotifyChanged();
        }

        private void LogFinishedCommand()
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

        private IEnumerator RunChecks()
        {
            foreach (var step in _steps)
            {
                SetStep(step, StepStatus.Running, "đang kiểm tra…");
                var check = _checkStep(step, false);
                while (check.MoveNext())
                    yield return check.Current;
            }
        }

        private IEnumerator RunInstall()
        {
            foreach (var step in _steps)
            {
                CurrentActivity = $"Kiểm tra: {step.Title}";
                SetStep(step, StepStatus.Running, "đang kiểm tra…");
                var check = _checkStep(step, true);
                while (check.MoveNext())
                    yield return check.Current;

                if (step.State == StepStatus.Done)
                    continue;

                if (step.State == StepStatus.Blocked)
                {
                    AppendLog($"Bỏ qua \"{step.Title}\": {step.Detail}");
                    // Bước của máy bị chặn (chưa có IDE) thì mọi bước sau đều không chạy được.
                    if (step.PerMachine)
                        yield break;

                    continue;
                }

                CurrentActivity = $"Đang cài: {step.Title}";
                AppendLog($"==> {step.Title}");
                SetStep(step, StepStatus.Running, "đang cài…");
                var act = _actStep(step);
                while (act.MoveNext())
                    yield return act.Current;

                SetStep(step, StepStatus.Running, "đang kiểm tra lại…");
                check = _checkStep(step, true);
                while (check.MoveNext())
                    yield return check.Current;

                if (step.State != StepStatus.Done)
                {
                    AppendLog($"Dừng ở \"{step.Title}\": {step.Detail}");
                    yield break;
                }
            }
        }

        // ─── Tiện ích ────────────────────────────────────────────────────────

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
        private DateTime GetOrCreateReadyDeadline()
        {
            if (long.TryParse(SessionState.GetString(_readyDeadlineKey, string.Empty), out var ticks))
                return new DateTime(ticks, DateTimeKind.Utc);

            var deadline = DateTime.UtcNow + ReadyTimeout;
            SessionState.SetString(_readyDeadlineKey, deadline.Ticks.ToString());
            return deadline;
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

        private void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private static int ParseLeadingInt(string text)
        {
            var match = Regex.Match(text, @"^\d+");
            return match.Success && int.TryParse(match.Value, out var value) ? value : 0;
        }
    }
}
#endif
