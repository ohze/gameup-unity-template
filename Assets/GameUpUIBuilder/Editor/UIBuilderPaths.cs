using System;
using System.IO;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Đường dẫn dùng chung: script Python trong package, venv theo user, cache, thư mục làm việc.</summary>
    public static class UIBuilderPaths
    {
        public const string EmbeddedFolder = "Assets/GameUpUIBuilder";

        /// <summary>Thư mục làm việc ở gốc project (ngoài Assets → Unity không import, không sinh .meta).</summary>
        public const string WorkspaceFolder = "UIBuilder";

        public const string SpecFileName = "spec.json";
        public const string LocateFileName = "locate.json";

        private static string _packageRoot;

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        /// <summary>Thư mục thật trên đĩa của package — cài embedded (Assets/) hay Git UPM (PackageCache) đều đúng.</summary>
        public static string PackageRoot
        {
            get
            {
                if (!string.IsNullOrEmpty(_packageRoot)) return _packageRoot;
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UIBuilderPaths).Assembly);
                _packageRoot = info != null
                    ? info.resolvedPath
                    : Path.Combine(ProjectRoot, EmbeddedFolder);
                return _packageRoot;
            }
        }

        public static string LocateScript => Path.Combine(PackageRoot, "Tools~", "ui_locate.py");

        public static string Requirements => Path.Combine(PackageRoot, "Tools~", "requirements.txt");

        /// <summary>Gói cài với --no-deps (rapidocr khai báo opencv-python bản GUI, trùng với bản headless).</summary>
        public static string RequirementsNoDeps => Path.Combine(PackageRoot, "Tools~", "requirements-nodeps.txt");

        public static string SkillSource => Path.Combine(PackageRoot, "AI~", "SKILL.md");

        public static string CommandSource => Path.Combine(PackageRoot, "AI~", "gu-ui.md");

        /// <summary>Venv dùng chung cho mọi project trên máy — chỉ cài OpenCV một lần.</summary>
        public static string VenvFolder
        {
            get
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, ".gameup", "ui-builder", "venv");
            }
        }

        public static string VenvPython => Application.platform == RuntimePlatform.WindowsEditor
            ? Path.Combine(VenvFolder, "Scripts", "python.exe")
            : Path.Combine(VenvFolder, "bin", "python");

        /// <summary>File đánh dấu venv đã cài xong và kiểm tra import OK.</summary>
        public static string VenvReadyMarker => Path.Combine(VenvFolder, ".gameup-ready");

        public static string CacheFolder => Path.Combine(ProjectRoot, "Library", "GameUpUIBuilder", "cache");

        public static string JobFolder(string jobName) => Path.Combine(WorkspaceFolder, jobName);

        public static string SpecPath(string jobName) => Path.Combine(JobFolder(jobName), SpecFileName).Replace('\\', '/');

        /// <summary>Kết quả định vị của demo thứ <paramref name="index"/>: locate.json, locate_2.json, …</summary>
        public static string LocatePath(string jobName, int index = 0)
        {
            var file = index == 0 ? LocateFileName : $"locate_{index + 1}.json";
            return Path.Combine(JobFolder(jobName), file).Replace('\\', '/');
        }

        /// <summary>Đường dẫn tuyệt đối → asset path "Assets/..." (null nếu nằm ngoài Assets).</summary>
        public static string ToAssetPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return null;
            var full = Path.GetFullPath(absolutePath).Replace('\\', '/');
            var assets = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            if (full == assets) return "Assets";
            return full.StartsWith(assets + "/", StringComparison.Ordinal)
                ? "Assets" + full.Substring(assets.Length)
                : null;
        }

        /// <summary>Asset path hoặc đường dẫn tương đối gốc project → tuyệt đối.</summary>
        public static string ToAbsolute(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(ProjectRoot, path));
        }
    }
}
