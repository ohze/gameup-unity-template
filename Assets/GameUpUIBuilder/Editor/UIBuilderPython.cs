using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GameUp.Core.Editor;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Môi trường Python cho bước định vị sprite: venv dùng chung theo user (<see cref="UIBuilderPaths.VenvFolder"/>)
    /// cài sẵn OpenCV + numpy. Cài bất đồng bộ theo từng bước, gọi <see cref="Poll"/> mỗi frame.
    /// </summary>
    public sealed class UIBuilderPython
    {
        private static readonly TimeSpan StepTimeout = TimeSpan.FromMinutes(10);

        private readonly Queue<(string label, string exe, string args)> _steps = new Queue<(string, string, string)>();
        private readonly StringBuilder _log = new StringBuilder();
        private GUExternalCommand _current;
        private string _currentLabel;

        public bool IsRunning => _current != null || _steps.Count > 0;

        public bool Failed { get; private set; }

        public string Log => _log.ToString();

        /// <summary>Venv đã cài xong và kiểm tra import thành công.</summary>
        public static bool IsReady => File.Exists(UIBuilderPaths.VenvPython) && File.Exists(UIBuilderPaths.VenvReadyMarker);

        /// <summary>Python hệ thống dùng để tạo venv; null nếu không tìm thấy.</summary>
        public static string FindSystemPython()
        {
            var names = Application.platform == RuntimePlatform.WindowsEditor
                ? new[] { "python", "py", "python3" }
                : new[] { "python3", "python" };
            foreach (var name in names)
            {
                var path = GUExternalCommand.ResolveExecutable(name);
                // Windows: "python.exe" trong WindowsApps là stub mở Microsoft Store, không chạy được.
                if (path != null && !path.Contains("WindowsApps"))
                    return path;
            }

            return null;
        }

        /// <summary>Tạo venv (nếu chưa có) → cài requirements → kiểm tra import → ghi marker.</summary>
        public void StartSetup(string systemPython)
        {
            Failed = false;
            _log.Clear();
            _steps.Clear();
            if (File.Exists(UIBuilderPaths.VenvReadyMarker)) File.Delete(UIBuilderPaths.VenvReadyMarker);

            var venvPython = UIBuilderPaths.VenvPython;
            if (!File.Exists(venvPython))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(UIBuilderPaths.VenvFolder));
                _steps.Enqueue(("Tạo venv", systemPython, $"-m venv {Quote(UIBuilderPaths.VenvFolder)}"));
            }

            _steps.Enqueue(("Cài OpenCV + numpy", venvPython,
                $"-m pip install --disable-pip-version-check -q -r {Quote(UIBuilderPaths.Requirements)}"));
            _steps.Enqueue(("Kiểm tra", venvPython,
                "-c \"import cv2, numpy; print('opencv', cv2.__version__, 'numpy', numpy.__version__)\""));
            StartNext();
        }

        /// <summary>Trả về true khi đã chạy xong tất cả bước (thành công hoặc thất bại).</summary>
        public bool Poll()
        {
            if (_current == null) return !IsRunning;
            if (!_current.Poll()) return false;

            _log.AppendLine(_current.Output.TrimEnd());
            if (!_current.Succeeded)
            {
                Failed = true;
                _log.AppendLine(_current.StartError ?? (_current.TimedOut ? "Quá thời gian chờ." : $"Lỗi (exit {_current.ExitCode})."));
                if (_currentLabel == "Tạo venv")
                    _log.AppendLine("Gợi ý Ubuntu/Debian: sudo apt install python3-venv");
                _steps.Clear();
                _current = null;
                return true;
            }

            _current = null;
            if (_steps.Count == 0)
            {
                File.WriteAllText(UIBuilderPaths.VenvReadyMarker, DateTime.UtcNow.ToString("O"));
                _log.AppendLine("✔ Môi trường sẵn sàng.");
                return true;
            }

            StartNext();
            return false;
        }

        public void Cancel()
        {
            _current?.Kill();
            _current = null;
            _steps.Clear();
        }

        public static string Quote(string path) => $"\"{path}\"";

        private void StartNext()
        {
            var step = _steps.Dequeue();
            _currentLabel = step.label;
            _log.AppendLine($"▶ {step.label}…");
            _current = GUExternalCommand.Start(step.exe, step.args, UIBuilderPaths.ProjectRoot, StepTimeout);
        }
    }
}
