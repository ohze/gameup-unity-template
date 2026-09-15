#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Chạy một lệnh ngoài (claude, Unity CLI, bash/PowerShell) bất đồng bộ từ Editor: không chặn UI, gom stdout + stderr,
    /// có timeout. Gọi <see cref="Poll"/> mỗi frame cho tới khi trả về true.
    /// <para>
    /// Editor mở từ Unity Hub/launcher thường không có PATH của shell đăng nhập (thiếu <c>~/.local/bin</c> — nơi cài
    /// <c>claude</c> và <c>unity</c> CLI), nên <see cref="ResolveExecutable"/> dò thêm các thư mục cài đặt quen thuộc và
    /// tiến trình con cũng nhận PATH đã bổ sung đó.
    /// </para>
    /// </summary>
    public sealed class GUExternalCommand
    {
        private static readonly Regex AnsiEscape = new Regex(@"\x1B\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);

        private readonly StringBuilder _output = new StringBuilder();
        private readonly object _outputLock = new object();
        private readonly Process _process;
        private readonly DateTime _deadline;

        private GUExternalCommand(Process process, DateTime deadline, string startError)
        {
            _process = process;
            _deadline = deadline;
            StartError = startError;
            IsDone = process == null;
        }

        public bool IsDone { get; private set; }

        public bool TimedOut { get; private set; }

        public int ExitCode { get; private set; } = -1;

        /// <summary>Lý do không khởi động được tiến trình (không tìm thấy file…); null nếu đã chạy.</summary>
        public string StartError { get; }

        public bool Succeeded => IsDone && !TimedOut && StartError == null && ExitCode == 0;

        /// <summary>stdout + stderr theo thứ tự nhận được, đã bỏ mã màu ANSI.</summary>
        public string Output
        {
            get
            {
                lock (_outputLock)
                    return _output.ToString();
            }
        }

        public static GUExternalCommand Start(string executable, string arguments, string workingDirectory, TimeSpan timeout)
        {
            var startInfo = new ProcessStartInfo(executable, arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            startInfo.EnvironmentVariables["PATH"] = string.Join(Path.PathSeparator.ToString(), GetSearchDirectories());
            startInfo.EnvironmentVariables["NO_COLOR"] = "1";
            startInfo.EnvironmentVariables["UNITY_NO_BANNER"] = "1";
            startInfo.EnvironmentVariables["UNITY_NO_PAGER"] = "1";
            startInfo.EnvironmentVariables["UNITY_NON_INTERACTIVE"] = "1";

            var process = new Process { StartInfo = startInfo };
            var command = new GUExternalCommand(process, DateTime.UtcNow + timeout, null);
            process.OutputDataReceived += command.AppendOutput;
            process.ErrorDataReceived += command.AppendOutput;

            try
            {
                process.Start();
                // Lệnh nào lỡ hỏi tương tác sẽ nhận EOF thay vì treo tới hết timeout.
                process.StandardInput.Close();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                return command;
            }
            catch (Exception e) when (e is Win32Exception || e is InvalidOperationException)
            {
                process.Dispose();
                return new GUExternalCommand(null, DateTime.UtcNow, e.Message);
            }
        }

        /// <summary>Trả về true khi lệnh đã kết thúc: thoát bình thường, không khởi động được, hoặc quá timeout (đã bị kill).</summary>
        public bool Poll()
        {
            if (IsDone)
                return true;

            if (_process.HasExited)
            {
                // Không tham số: đợi luồng đọc output bất đồng bộ xả hết trước khi đọc Output.
                _process.WaitForExit();
                ExitCode = _process.ExitCode;
                Complete();
                return true;
            }

            if (DateTime.UtcNow < _deadline)
                return false;

            TimedOut = true;
            Kill();
            Complete();
            return true;
        }

        public void Kill()
        {
            if (IsDone)
                return;

            try
            {
                if (!_process.HasExited)
                    _process.Kill();
            }
            catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
            {
                // Tiến trình vừa tự thoát giữa hai lệnh — không còn gì để kill.
            }
        }

        /// <summary>
        /// Tìm file thực thi theo tên (không đuôi) trong PATH của Editor và các thư mục cài đặt quen thuộc.
        /// Bỏ qua binary của chính Unity Editor (thư mục có <c>Data/Managed</c>) để <c>unity</c> không trỏ nhầm <c>Unity.exe</c>.
        /// </summary>
        public static string ResolveExecutable(string name)
        {
            var extensions = Application.platform == RuntimePlatform.WindowsEditor
                ? new[] { ".exe", ".cmd", ".bat" }
                : new[] { string.Empty };

            foreach (var directory in GetSearchDirectories())
            {
                foreach (var extension in extensions)
                {
                    var candidate = Path.Combine(directory, name + extension);
                    if (File.Exists(candidate) && !IsUnityEditorBinary(candidate))
                        return candidate;
                }
            }

            return null;
        }

        private void AppendOutput(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;

            lock (_outputLock)
                _output.AppendLine(AnsiEscape.Replace(e.Data, string.Empty));
        }

        private void Complete()
        {
            IsDone = true;
            _process.Dispose();
        }

        private static bool IsUnityEditorBinary(string path)
        {
            var directory = Path.GetDirectoryName(path);
            return !string.IsNullOrEmpty(directory) && Directory.Exists(Path.Combine(directory, "Data", "Managed"));
        }

        private static List<string> GetSearchDirectories()
        {
            var candidates = new List<string>((Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator));

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home))
            {
                candidates.Add(Path.Combine(home, ".local", "bin"));
                candidates.Add(Path.Combine(home, ".claude", "local"));
                candidates.Add(Path.Combine(home, ".npm-global", "bin"));
                candidates.Add(Path.Combine(home, "bin"));
            }

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"));
            }
            else
            {
                candidates.Add("/usr/local/bin");
                candidates.Add("/opt/homebrew/bin");
                candidates.Add("/usr/bin");
                candidates.Add("/bin");
            }

            var result = new List<string>();
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate) || candidate.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                    continue;

                var trimmed = candidate.Trim();
                if (!result.Contains(trimmed))
                    result.Add(trimmed);
            }

            return result;
        }
    }
}
#endif
