using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GameUp.Core.Editor;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Chạy <c>Tools~/ui_locate.py</c> trong venv, không chặn Editor. Gọi <see cref="Poll"/> mỗi frame.</summary>
    public sealed class UIBuilderLocator
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

        private readonly GUExternalCommand _command;
        private readonly string _outPath;

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
        public static UIBuilderLocator Start(string demoPath, IEnumerable<string> artPaths, bool recursive, string outPath)
        {
            var args = BuildArguments(demoPath, artPaths, recursive, outPath);
            var command = GUExternalCommand.Start(UIBuilderPaths.VenvPython, args, UIBuilderPaths.ProjectRoot, Timeout);
            return new UIBuilderLocator(command, outPath);
        }

        /// <summary>Dòng lệnh đầy đủ — để hiện cho người dùng / đưa vào prompt cho AI chạy lại.</summary>
        public static string BuildCommandLine(string demoPath, IEnumerable<string> artPaths, bool recursive, string outPath)
        {
            return $"{UIBuilderPython.Quote(UIBuilderPaths.VenvPython)} {BuildArguments(demoPath, artPaths, recursive, outPath)}";
        }

        /// <summary>Trả về true khi đã xong; khi đó <see cref="Result"/> hoặc <see cref="Error"/> có giá trị.</summary>
        public bool Poll()
        {
            if (Result != null || Error != null) return true;
            if (!_command.Poll()) return false;

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
        }

        private static string BuildArguments(string demoPath, IEnumerable<string> artPaths, bool recursive, string outPath)
        {
            var sb = new StringBuilder();
            sb.Append(UIBuilderPython.Quote(UIBuilderPaths.LocateScript));
            sb.Append(" --demo ").Append(UIBuilderPython.Quote(UIBuilderPaths.ToAbsolute(demoPath)));
            foreach (var art in artPaths)
                sb.Append(" --art ").Append(UIBuilderPython.Quote(UIBuilderPaths.ToAbsolute(art)));
            if (recursive) sb.Append(" --recursive");
            sb.Append(" --out ").Append(UIBuilderPython.Quote(UIBuilderPaths.ToAbsolute(outPath)));
            sb.Append(" --cache ").Append(UIBuilderPython.Quote(UIBuilderPaths.CacheFolder));
            return sb.ToString();
        }

        private static string LastLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var lines = text.TrimEnd().Split('\n');
            return lines[lines.Length - 1].Trim();
        }
    }
}
