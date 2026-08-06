using System.Collections.Generic;
using System.IO;
using System.Text;
using GameUp.SDK;
using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    /// <summary>
    /// Sinh file constants chứa tên placement ("where") từ GameUpAdsConfig,
    /// để code gọi ads không phải viết chuỗi tay.
    /// </summary>
    public static class AdPlacementGenerator
    {
        public const string OutputDirectory = "Assets/_MainProject/Scripts/SDK";
        public const string OutputPath = OutputDirectory + "/AdPlacement.cs";

        public static void Generate()
        {
            var config = GameUpAdsConfigAsset.Find();
            if (config == null)
            {
                Debug.LogWarning("[GameUp.SDK] Chưa có GameUpAdsConfig, không sinh được AdPlacement.cs.");
                return;
            }

            var placements = CollectPlacements(config);

            var sb = new StringBuilder();
            sb.AppendLine("// ========================================================");
            sb.AppendLine("// AUTO-GENERATED FILE BY GAMEUP SDK. DO NOT MODIFY DIRECTLY.");
            sb.AppendLine("// ========================================================");
            sb.AppendLine("");
            sb.AppendLine("namespace GameUp.SDK");
            sb.AppendLine("{");
            sb.AppendLine("    public static class AdPlacement");
            sb.AppendLine("    {");

            var usedNames = new HashSet<string>();
            foreach (var placement in placements)
            {
                string varName = SanitizeToVariableName(placement);
                while (!usedNames.Add(varName)) varName += "_";
                sb.AppendLine($"        public const string {varName} = \"{placement}\";");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllText(OutputPath, sb.ToString());
            AssetDatabase.Refresh();

            Debug.Log($"[GameUp.SDK] Đã tạo file Constants tại: {OutputPath} ({placements.Count} placement)");

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(OutputPath);
            if (asset != null) EditorGUIUtility.PingObject(asset);
        }

        public static List<string> CollectPlacements(GameUpAdsConfig config)
        {
            var placements = new List<string> { "default", "main" };
            if (config == null) return placements;

            CollectFrom(config.admob.units, placements);
            CollectFrom(config.max.units, placements);
            CollectFrom(config.ironSource.units, placements);
            return placements;
        }

        private static void CollectFrom(AdUnitConfigSet set, List<string> placements)
        {
            if (set == null) return;
            foreach (var config in set.All())
            {
                if (config == null) continue;
                foreach (var list in new[] { config.placementsAndroid, config.placementsIOS })
                {
                    if (list == null) continue;
                    foreach (var placement in list)
                    {
                        if (placement == null || string.IsNullOrWhiteSpace(placement.where)) continue;
                        string clean = placement.where.Trim();
                        if (!placements.Contains(clean)) placements.Add(clean);
                    }
                }
            }
        }

        public static string SanitizeToVariableName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Unknown";

            // 1. Thay thế các ký tự không phải chữ/số thành dấu cách (để dễ viết hoa)
            char[] chars = raw.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = ' ';
            }

            // 2. Chuyển thành PascalCase (VD: "main menu" -> "MainMenu")
            string cleaned = new string(chars);
            var textInfo = new System.Globalization.CultureInfo("en-US", false).TextInfo;
            cleaned = textInfo.ToTitleCase(cleaned).Replace(" ", "");
            if (string.IsNullOrEmpty(cleaned)) return "Unknown";

            // 3. Đảm bảo biến hợp lệ trong C# (nếu lỡ bắt đầu bằng số thì thêm dấu _)
            if (!char.IsLetter(cleaned[0]) && cleaned[0] != '_') cleaned = "_" + cleaned;

            return cleaned;
        }
    }
}
