#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Cursor chỉ đọc rule từ thư mục project <c>.cursor/rules/</c>, không đọc trong <c>Packages/</c>.
    /// Bản mẫu nằm trong <c>Documentation~/cursor-rules/</c> (Unity không import thư mục <c>~</c>).
    /// </summary>
    public static class GUCursorRulesInstaller
    {
        private const string MenuPath = "GameUp/Project/Install Cursor IDE rules (from GameUp Core)";
        private const string MarkerFile = "gameup-core-usage.mdc";

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

            if (!TryGetTemplatesDir(out var src))
            {
                return;
            }

            CopyAllMdc(src, DestRulesDir, overwrite: false);
            GULogger.Log("CursorRules", "Đã copy Cursor rules (.mdc) vào .cursor/rules — mở lại Cursor nếu cần.");
        }

        [MenuItem(MenuPath)]
        private static void InstallFromMenu()
        {
            if (!TryGetTemplatesDir(out var src))
            {
                EditorUtility.DisplayDialog(
                    "GameUp Core",
                    "Không tìm thấy thư mục mẫu Documentation~/cursor-rules trong package com.ohze.gameup.core.",
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "GameUp Core",
                    "Ghi đè / thêm các file .mdc vào .cursor/rules trong project (không xóa file rule khác)?",
                    "OK",
                    "Cancel"))
            {
                return;
            }

            CopyAllMdc(src, DestRulesDir, overwrite: true);
            GULogger.Log("CursorRules", "Đã cập nhật Cursor rules từ GameUp Core.");
        }

        internal static bool TryGetTemplatesDir(out string path)
        {
            path = null;
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(GUCursorRulesInstaller).Assembly);
            if (info != null)
            {
                var p = Path.Combine(info.resolvedPath, "Documentation~", "cursor-rules");
                if (Directory.Exists(p))
                {
                    path = p;
                    return true;
                }
            }

            var fallback = Path.Combine(Application.dataPath, "GameUpCore", "Documentation~", "cursor-rules");
            if (Directory.Exists(fallback))
            {
                path = fallback;
                return true;
            }

            return false;
        }

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
