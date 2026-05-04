#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Cursor đọc từ gốc project: <c>.cursor/rules/</c>, <c>.cursorrules</c>, <c>.cursorignore</c>.
    /// Mẫu trong <c>Documentation~/cursor-rules</c> và <c>Documentation~/cursor-project-root</c>
    /// (file mẫu ignore tên <c>cursorignore</c>, khi cài được ghi thành <c>.cursorignore</c>).
    /// </summary>
    public static class GUCursorRulesInstaller
    {
        private const string MenuPath = "GameUp/Project/Install Cursor IDE rules (from GameUp Core)";
        private const string MarkerFile = "gameup-core-usage.mdc";
        private const string IdeCursorGitUrl = "https://github.com/boxqkrtm/com.unity.ide.cursor.git";
        private const string IdeCursorPackageDirName = "com.boxqkrtm.ide.cursor";

        private static AddRequest _ideCursorAddRequest;

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        private static string DestRulesDir => Path.Combine(ProjectRoot, ".cursor", "rules");

        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            EditorApplication.delayCall += TryAutoInstallIfMissing;
        }

        private static void TryAutoInstallIfMissing()
        {
            if (File.Exists(Path.Combine(DestRulesDir, MarkerFile)))
            {
                return;
            }

            if (!TryGetCursorRulesTemplatesDir(out var src))
            {
                return;
            }

            CopyAllMdc(src, DestRulesDir, overwrite: false);
            GULogger.Log("CursorRules", "Đã copy Cursor rules (.mdc) vào .cursor/rules — mở lại Cursor nếu cần.");
        }

        [MenuItem(MenuPath)]
        private static void InstallFromMenu()
        {
            if (!TryGetGameUpCorePackageRoot(out var packageRoot))
            {
                EditorUtility.DisplayDialog(
                    "GameUp Core",
                    "Không tìm thấy package com.ohze.gameup.core (resolved path).",
                    "OK");
                return;
            }

            var rulesSrc = Path.Combine(packageRoot, "Documentation~", "cursor-rules");
            var rootTemplates = Path.Combine(packageRoot, "Documentation~", "cursor-project-root");
            if (!Directory.Exists(rulesSrc))
            {
                EditorUtility.DisplayDialog(
                    "GameUp Core",
                    "Thiếu thư mục Documentation~/cursor-rules trong GameUp Core.",
                    "OK");
                return;
            }

            if (!Directory.Exists(rootTemplates))
            {
                EditorUtility.DisplayDialog(
                    "GameUp Core",
                    "Thiếu thư mục Documentation~/cursor-project-root (.cursorrules / cursorignore mẫu).",
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "GameUp Core",
                    "Thực hiện:\n" +
                    "• Thêm package IDE Cursor qua Git (nếu chưa có): com.boxqkrtm.ide.cursor\n" +
                    "• Ghi đè / cập nhật .cursor/rules/*.mdc, .cursorrules, .cursorignore (mẫu cursorignore) tại gốc project\n\n" +
                    "Tiếp tục?",
                    "OK",
                    "Cancel"))
            {
                return;
            }

            CopyAllMdc(rulesSrc, DestRulesDir, overwrite: true);
            CopyProjectRootTemplate(Path.Combine(rootTemplates, ".cursorrules"), Path.Combine(ProjectRoot, ".cursorrules"));
            // File mẫu tên `cursorignore` (không dấu chấm) để tránh hạn chế FS/tooling khi đóng gói.
            CopyProjectRootTemplate(Path.Combine(rootTemplates, "cursorignore"), Path.Combine(ProjectRoot, ".cursorignore"));

            RequestAddIdeCursorPackage();

            GULogger.Log(
                "CursorRules",
                "Đã cập nhật .cursor/rules, .cursorrules, .cursorignore. Nếu package IDE Cursor chưa có, Unity đang thêm qua Git URL (xem Package Manager / Console).");
        }

        private static void CopyProjectRootTemplate(string sourceFile, string destFile)
        {
            if (!File.Exists(sourceFile))
            {
                GULogger.Warning("CursorRules", $"Thiếu file mẫu: {sourceFile}");
                return;
            }

            File.Copy(sourceFile, destFile, overwrite: true);
        }

        private static void RequestAddIdeCursorPackage()
        {
            if (IsIdeCursorInstalled())
            {
                GULogger.Log("CursorRules", "com.boxqkrtm.ide.cursor đã có trong Packages — bỏ qua Client.Add.");
                return;
            }

            if (_ideCursorAddRequest != null && !_ideCursorAddRequest.IsCompleted)
            {
                GULogger.Log("CursorRules", "Đang chờ thêm package IDE Cursor từ lần trước…");
                return;
            }

            _ideCursorAddRequest = Client.Add(IdeCursorGitUrl);
            EditorApplication.update += OnIdeCursorAddProgress;
        }

        private static void OnIdeCursorAddProgress()
        {
            if (_ideCursorAddRequest == null || !_ideCursorAddRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= OnIdeCursorAddProgress;
            var req = _ideCursorAddRequest;
            _ideCursorAddRequest = null;

            if (req.Status == StatusCode.Success)
            {
                GULogger.Log("CursorRules", "Đã thêm package com.boxqkrtm.ide.cursor (Cursor IDE for Unity).");
                return;
            }

            var err = req.Error != null ? req.Error.message : "unknown";
            GULogger.Warning(
                "CursorRules",
                $"Không thêm được IDE Cursor qua UPM: {err}. Thêm thủ công Git URL: {IdeCursorGitUrl} (xem https://github.com/boxqkrtm/com.unity.ide.cursor )");
        }

        private static bool IsIdeCursorInstalled()
        {
            return Directory.Exists(Path.Combine(ProjectRoot, "Packages", IdeCursorPackageDirName));
        }

        internal static bool TryGetCursorRulesTemplatesDir(out string path)
        {
            path = null;
            if (!TryGetGameUpCorePackageRoot(out var root))
            {
                return false;
            }

            var p = Path.Combine(root, "Documentation~", "cursor-rules");
            if (Directory.Exists(p))
            {
                path = p;
                return true;
            }

            return false;
        }

        internal static bool TryGetGameUpCorePackageRoot(out string packageRoot)
        {
            packageRoot = null;
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(GUCursorRulesInstaller).Assembly);
            if (info != null && !string.IsNullOrEmpty(info.resolvedPath))
            {
                packageRoot = info.resolvedPath;
                return true;
            }

            var fallback = Path.Combine(Application.dataPath, "GameUpCore");
            if (Directory.Exists(fallback) && File.Exists(Path.Combine(fallback, "package.json")))
            {
                packageRoot = fallback;
                return true;
            }

            return false;
        }

        /// <summary>Giữ tên cũ cho mã gọi nội bộ (nếu có).</summary>
        internal static bool TryGetTemplatesDir(out string path) => TryGetCursorRulesTemplatesDir(out path);

        private static void CopyAllMdc(string sourceDir, string destDir, bool overwrite)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir, "*.mdc"))
            {
                var name = Path.GetFileName(file);
                var dest = Path.Combine(destDir, name);
                if (!overwrite && File.Exists(dest))
                {
                    continue;
                }

                File.Copy(file, dest, overwrite);
            }
        }
    }
}
#endif
