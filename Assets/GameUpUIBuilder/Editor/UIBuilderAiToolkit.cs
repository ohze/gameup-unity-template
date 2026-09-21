using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Cài skill Claude Code của UI Builder vào project và soạn prompt giao việc cho AI.</summary>
    public static class UIBuilderAiToolkit
    {
        private const string SkillTarget = ".claude/skills/gameup-ui-builder/SKILL.md";
        private const string CommandTarget = ".claude/commands/gu-ui.md";

        public static bool IsInstalled => File.Exists(Path.Combine(UIBuilderPaths.ProjectRoot, SkillTarget));

        /// <summary>Bản trong project khác bản trong package (package vừa cập nhật).</summary>
        public static bool IsOutdated => IsInstalled && !SameContent(UIBuilderPaths.SkillSource, Path.Combine(UIBuilderPaths.ProjectRoot, SkillTarget));

        public static void Install()
        {
            Copy(UIBuilderPaths.SkillSource, SkillTarget);
            Copy(UIBuilderPaths.CommandSource, CommandTarget);
        }

        public static string BuildPrompt(string jobName, string demoPath, IReadOnlyList<string> artFolders, bool recursive, string outputPrefab)
        {
            var specPath = UIBuilderPaths.SpecPath(jobName);
            var locatePath = UIBuilderPaths.LocatePath(jobName);
            var sb = new StringBuilder();
            sb.AppendLine($"/gu-ui Dựng UI \"{jobName}\" từ ảnh demo bằng GameUp UI Builder.");
            sb.AppendLine($"- Demo: {demoPath}");
            sb.AppendLine($"- Art: {string.Join(", ", artFolders)}");
            sb.AppendLine($"- Kết quả định vị sprite: {locatePath}" + (File.Exists(UIBuilderPaths.ToAbsolute(locatePath)) ? string.Empty : " (chưa chạy)"));
            sb.AppendLine($"  Chạy lại: {UIBuilderLocator.BuildCommandLine(demoPath, artFolders, recursive, locatePath)}");
            sb.AppendLine($"- Spec: {specPath}" + (File.Exists(UIBuilderPaths.ToAbsolute(specPath)) ? " (đã có bản nháp — hoàn thiện tiếp)" : " (chưa có — tạo mới)"));
            sb.AppendLine($"- Prefab đầu ra: {outputPrefab}");
            return sb.ToString();
        }

        private static void Copy(string source, string relativeTarget)
        {
            var target = Path.Combine(UIBuilderPaths.ProjectRoot, relativeTarget);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target, true);
        }

        private static bool SameContent(string a, string b)
        {
            return File.Exists(a) && File.Exists(b) && File.ReadAllText(a) == File.ReadAllText(b);
        }
    }
}
