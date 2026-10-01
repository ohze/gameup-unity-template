using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor
{
    /// <summary>
    /// Giữ <c>.gitignore</c> của project khớp với bản mẫu đi kèm GameUp SDK (<c>GameUpGitignoreTemplate.txt</c>).
    /// Chạy một lần mỗi phiên Editor: chưa có <c>.gitignore</c> thì tạo đầy đủ từ bản mẫu; đã có thì chỉ bổ sung các luật
    /// còn thiếu vào một khối đánh dấu ở cuối file — không sửa/xoá dòng nào của dev. SDK cập nhật bản mẫu thì khối tự cập nhật.
    /// </summary>
    [InitializeOnLoad]
    internal static class GameUpGitignoreSync
    {
        private const string TemplateAssetName = "GameUpGitignoreTemplate";
        private const string BlockBegin = "# >>> GameUp SDK — tự thêm, sửa trong khối này sẽ bị ghi đè (tắt: GameUp → SDK → .gitignore)";
        private const string BlockEnd = "# <<< GameUp SDK";
        private const string SessionKey = "GameUpSDK_GitignoreSynced";
        private const string AutoSyncPrefKey = "GameUpSDK_GitignoreAutoSync";
        private const string MenuAutoSync = "GameUp/SDK/.gitignore/Tự đồng bộ khi mở project";

        static GameUpGitignoreSync()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += RunAutoSync;
        }

        private static bool IsAutoSyncEnabled => EditorPrefs.GetBool(AutoSyncPrefKey, true);

        private static void RunAutoSync()
        {
            if (!IsAutoSyncEnabled) return;
            TrySync(out _);
        }

        [MenuItem("GameUp/SDK/.gitignore/Đồng bộ ngay", priority = 40)]
        private static void MenuSyncNow()
        {
            if (TrySync(out string message)) Debug.Log($"[GameUpSDK] {message}");
            else Debug.LogWarning($"[GameUpSDK] {message}");
        }

        [MenuItem(MenuAutoSync, priority = 41)]
        private static void MenuToggleAutoSync()
        {
            EditorPrefs.SetBool(AutoSyncPrefKey, !IsAutoSyncEnabled);
        }

        [MenuItem(MenuAutoSync, true)]
        private static bool MenuToggleAutoSyncValidate()
        {
            Menu.SetChecked(MenuAutoSync, IsAutoSyncEnabled);
            return true;
        }

        /// <summary>Đồng bộ <c>.gitignore</c> ở thư mục gốc project. Trả false (kèm lý do) nếu không làm được.</summary>
        internal static bool TrySync(out string message)
        {
            string template = LoadTemplate();
            if (template == null)
            {
                message = $"Không tìm thấy {TemplateAssetName}.txt trong GameUp SDK — bỏ qua đồng bộ .gitignore.";
                return false;
            }

            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", ".gitignore");
            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, template);
                    message = $"Đã tạo .gitignore từ bản mẫu GameUp SDK: {path}";
                    Debug.Log($"[GameUpSDK] {message}");
                    return true;
                }

                string original = File.ReadAllText(path);
                string merged = Merge(original, template, out int addedRules);
                if (merged == original)
                {
                    message = ".gitignore đã đủ luật của GameUp SDK.";
                    return true;
                }

                File.WriteAllText(path, merged);
                message = addedRules > 0
                    ? $"Đã bổ sung {addedRules} luật GameUp SDK vào .gitignore (khối \"GameUp SDK\" cuối file)."
                    : "Đã dọn khối GameUp SDK trong .gitignore (các luật đã có sẵn ở phần của project).";
                Debug.Log($"[GameUpSDK] {message}");
                return true;
            }
            catch (Exception e)
            {
                message = $"Không ghi được .gitignore: {e.Message}";
                return false;
            }
        }

        private static string LoadTemplate()
        {
            foreach (string guid in AssetDatabase.FindAssets($"{TemplateAssetName} t:TextAsset"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(assetPath) != TemplateAssetName) continue;
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (asset != null) return asset.text;
            }
            return null;
        }

        /// <summary>
        /// Bỏ khối GameUp cũ, rồi dựng lại khối gồm các luật trong bản mẫu mà phần của dev chưa có (giữ thứ tự bản mẫu).
        /// So khớp theo dòng đã trim; comment và dòng trống của bản mẫu không được chép.
        /// </summary>
        private static string Merge(string original, string template, out int addedRules)
        {
            string newline = original.Contains("\r\n") ? "\r\n" : "\n";
            var allLines = SplitLines(original);
            var userLines = StripManagedBlock(allLines);
            bool hadBlock = userLines.Count != allLines.Count;

            var existing = new HashSet<string>(userLines.Select(l => l.Trim()).Where(IsRule));
            var missing = SplitLines(template)
                .Select(l => l.Trim())
                .Where(l => IsRule(l) && existing.Add(l))
                .ToList();
            addedRules = missing.Count;

            // Không thiếu gì và chưa từng có khối: giữ nguyên file từng byte (không chuẩn hoá dòng trống/xuống dòng).
            if (missing.Count == 0 && !hadBlock) return original;

            while (userLines.Count > 0 && userLines[userLines.Count - 1].Trim().Length == 0)
                userLines.RemoveAt(userLines.Count - 1);

            var result = new List<string>(userLines);
            if (missing.Count > 0)
            {
                if (result.Count > 0) result.Add("");
                result.Add(BlockBegin);
                result.AddRange(missing);
                result.Add(BlockEnd);
            }

            return string.Join(newline, result) + newline;
        }

        private static List<string> StripManagedBlock(List<string> lines)
        {
            var kept = new List<string>(lines.Count);
            bool inBlock = false;
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (!inBlock && trimmed.StartsWith("# >>> GameUp SDK", StringComparison.Ordinal))
                {
                    inBlock = true;
                    continue;
                }
                if (inBlock)
                {
                    if (trimmed == BlockEnd) inBlock = false;
                    continue;
                }
                kept.Add(line);
            }
            return kept;
        }

        private static List<string> SplitLines(string text)
        {
            return text.Replace("\r\n", "\n").Split('\n').ToList();
        }

        private static bool IsRule(string trimmedLine)
        {
            return trimmedLine.Length > 0 && !trimmedLine.StartsWith("#", StringComparison.Ordinal);
        }
    }
}
