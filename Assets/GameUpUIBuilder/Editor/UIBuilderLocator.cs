using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GameUp.Core.Editor;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Chạy <c>Tools~/ui_locate.py</c> (dò sprite trên một demo) hoặc <c>Tools~/ui_psd.py</c> (đọc PSD, mọi tab một lượt)
    /// trong venv, không chặn Editor. Gọi <see cref="Poll"/> mỗi frame; trong lúc chạy <see cref="Progress"/> cập nhật theo
    /// từng dòng sự kiện script in ra.
    /// </summary>
    public sealed class UIBuilderLocator
    {
        // Thư mục art rộng (cả UI_v2, ~850 sprite) mất vài phút lần đầu; người dùng luôn huỷ được bằng nút Huỷ.
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

        private readonly GUExternalCommand _command;
        private readonly string _outPath;
        private int _parsedLength;

        public LocateProgress Progress { get; } = new LocateProgress();

        public LocateResult Result { get; private set; }

        public string Error { get; private set; }

        public string Output => _command?.Output ?? string.Empty;

        private UIBuilderLocator(GUExternalCommand command, string outPath)
        {
            _command = command;
            _outPath = outPath;
        }

        /// <param name="demoPath">Ảnh demo (asset path hoặc tuyệt đối).</param>
        /// <param name="artPaths">Thư mục art hoặc file PNG (asset path hoặc tuyệt đối).</param>
        /// <param name="outPath">File locate.json đầu ra (tương đối gốc project hoặc tuyệt đối).</param>
        /// <param name="hints">locate.json của demo các tab khác cùng màn — đối chiếu phần chung (file chưa có thì bỏ qua).</param>
        public static UIBuilderLocator Start(string demoPath, IEnumerable<string> artPaths, bool recursive, string outPath,
            IEnumerable<string> hints)
        {
            var args = BuildArguments(demoPath, artPaths, recursive, outPath, hints);
            var command = GUExternalCommand.Start(UIBuilderPaths.VenvPython, args, UIBuilderPaths.ProjectRoot, Timeout);
            return new UIBuilderLocator(command, outPath);
        }

        /// <summary>
        /// Đọc PSD: ghi <c>locate.json</c>, <c>locate_2.json</c>… (mỗi trạng thái/tab một file) vào thư mục job;
        /// <see cref="Result"/> là trạng thái đầu.
        /// </summary>
        /// <param name="demos">Ảnh demo theo thứ tự tab — chỉ để so sánh; tab chưa có demo dùng ảnh ghép từ PSD.</param>
        /// <param name="exportFolder">Thư mục (trong Assets) ghi PNG cho layer không có art; rỗng = không xuất.</param>
        public static UIBuilderLocator StartPsd(string psdPath, IEnumerable<string> artPaths, bool recursive, string jobName,
            IEnumerable<string> demos, string exportFolder)
        {
            var args = BuildPsdArguments(psdPath, artPaths, recursive, jobName, demos, exportFolder);
            var command = GUExternalCommand.Start(UIBuilderPaths.VenvPython, args, UIBuilderPaths.ProjectRoot, Timeout);
            return new UIBuilderLocator(command, UIBuilderPaths.LocatePath(jobName));
        }

        public static string BuildPsdCommandLine(string psdPath, IEnumerable<string> artPaths, bool recursive, string jobName,
            IEnumerable<string> demos, string exportFolder)
        {
            return $"{UIBuilderPython.Quote(UIBuilderPaths.VenvPython)} {BuildPsdArguments(psdPath, artPaths, recursive, jobName, demos, exportFolder)}";
        }

        /// <summary>Dòng lệnh đầy đủ — để hiện cho người dùng / đưa vào prompt cho AI chạy lại.</summary>
        public static string BuildCommandLine(string demoPath, IEnumerable<string> artPaths, bool recursive, string outPath,
            IEnumerable<string> hints)
        {
            return $"{UIBuilderPython.Quote(UIBuilderPaths.VenvPython)} {BuildArguments(demoPath, artPaths, recursive, outPath, hints)}";
        }

        /// <summary>Trả về true khi đã xong; khi đó <see cref="Result"/> hoặc <see cref="Error"/> có giá trị.</summary>
        public bool Poll()
        {
            if (Result != null || Error != null) return true;
            var finished = _command.Poll();
            ReadProgress();
            if (!finished) return false;

            if (!_command.Succeeded)
            {
                Error = _command.StartError
                        ?? (_command.TimedOut ? "Quá thời gian chờ." : LastLine(_command.Output) ?? $"exit {_command.ExitCode}");
                return true;
            }

            Result = Load(_outPath);
            if (Result == null) Error = $"Không đọc được {_outPath}.";
            return true;
        }

        public void Cancel() => _command?.Kill();

        public static LocateResult Load(string path)
        {
            var absolute = UIBuilderPaths.ToAbsolute(path);
            if (!File.Exists(absolute)) return null;
            try
            {
                return JsonUtility.FromJson<LocateResult>(File.ReadAllText(absolute));
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (IOException) // script đang ghi dở file → lần tải lại sau sẽ đọc được
            {
                return null;
            }
        }

        /// <summary>Áp các dòng stdout mới hoàn chỉnh (kết thúc bằng xuống dòng) vào <see cref="Progress"/>.</summary>
        private void ReadProgress()
        {
            var output = Output;
            var end = output.LastIndexOf('\n');
            if (end < _parsedLength) return;
            foreach (var line in output.Substring(_parsedLength, end - _parsedLength).Split('\n'))
                Progress.ApplyLine(line.TrimEnd('\r'));
            _parsedLength = end + 1;
        }

        private static string BuildArguments(string demoPath, IEnumerable<string> artPaths, bool recursive, string outPath,
            IEnumerable<string> hints)
        {
            var sb = new StringBuilder();
            sb.Append(UIBuilderPython.Quote(UIBuilderPaths.LocateScript));
            sb.Append(" --demo ").Append(UIBuilderPython.Quote(UIBuilderPaths.ToAbsolute(demoPath)));
            foreach (var art in artPaths)
                sb.Append(" --art ").Append(UIBuilderPython.Quote(UIBuilderPaths.ToAbsolute(art)));
            if (recursive) sb.Append(" --recursive");
            sb.Append(" --out ").Append(UIBuilderPython.Quote(UIBuilderPaths.ToAbsolute(outPath)));
            sb.Append(" --cache ").Append(UIBuilderPython.Quote(UIBuilderPaths.CacheFolder));
            foreach (var hint in hints)
                sb.Append(" --hint ").Append(UIBuilderPython.Quote(UIBuilderPaths.ToAbsolute(hint)));
            var workers = UIBuilderSettings.instance.locateWorkers;
            if (workers > 0) sb.Append(" --workers ").Append(workers);
            return sb.ToString();
        }

        private static string BuildPsdArguments(string psdPath, IEnumerable<string> artPaths, bool recursive, string jobName,
            IEnumerable<string> demos, string exportFolder)
        {
            var sb = new StringBuilder();
            sb.Append(UIBuilderPython.Quote(UIBuilderPaths.PsdScript));
            sb.Append(" --psd ").Append(UIBuilderPython.Quote(UIBuilderPaths.ToAbsolute(psdPath)));
            foreach (var art in artPaths)
                sb.Append(" --art ").Append(UIBuilderPython.Quote(UIBuilderPaths.ToAbsolute(art)));
            if (recursive) sb.Append(" --recursive");
            sb.Append(" --out-dir ").Append(UIBuilderPython.Quote(UIBuilderPaths.ToAbsolute(UIBuilderPaths.JobFolder(jobName))));
            // giữ đúng vị trí tab: tab chưa có demo truyền chuỗi rỗng
            foreach (var demo in demos)
                sb.Append(" --demo ").Append(UIBuilderPython.Quote(string.IsNullOrEmpty(demo) ? string.Empty : UIBuilderPaths.ToAbsolute(demo)));
            if (!string.IsNullOrEmpty(exportFolder))
                sb.Append(" --export ").Append(UIBuilderPython.Quote(UIBuilderPaths.ToAbsolute(exportFolder)));
            return sb.ToString();
        }

        private static string LastLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var last = text.TrimEnd().Split('\n').LastOrDefault(l => !l.StartsWith(LocateProgress.LinePrefix, StringComparison.Ordinal));
            return last?.Trim();
        }
    }
}
