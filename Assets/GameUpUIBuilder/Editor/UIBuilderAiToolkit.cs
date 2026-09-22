using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public static string BuildPrompt(string jobName, IReadOnlyList<string> demos, IReadOnlyList<string> artFolders, bool recursive,
            string outputPrefab, IReadOnlyList<UISkipRegion> skipRegions)
        {
            var specPath = UIBuilderPaths.SpecPath(jobName);
            var sb = new StringBuilder();
            sb.AppendLine($"/gu-ui Dựng UI \"{jobName}\" từ ảnh demo bằng GameUp UI Builder.");
            if (demos.Count > 1) sb.AppendLine($"- {demos.Count} demo = {demos.Count} trạng thái (tab) của cùng UI, theo thứ tự:");
            sb.AppendLine($"- Art: {string.Join(", ", artFolders)}");
            for (var i = 0; i < demos.Count; i++)
            {
                var locatePath = UIBuilderPaths.LocatePath(jobName, i);
                sb.AppendLine($"- Demo {i + 1}: {demos[i]} → định vị: {locatePath}"
                              + (File.Exists(UIBuilderPaths.ToAbsolute(locatePath)) ? string.Empty : " (chưa chạy)"));
                var hints = Enumerable.Range(0, demos.Count).Where(k => k != i).Select(k => UIBuilderPaths.LocatePath(jobName, k));
                sb.AppendLine($"  Chạy lại: {UIBuilderLocator.BuildCommandLine(demos[i], artFolders, recursive, locatePath, hints)}");
            }
            sb.AppendLine($"- Spec: {specPath}" + (File.Exists(UIBuilderPaths.ToAbsolute(specPath)) ? " (đã có bản nháp — hoàn thiện tiếp)" : " (chưa có — tạo mới)"));
            sb.AppendLine($"- Prefab đầu ra: {outputPrefab}");
            foreach (var r in skipRegions)
                sb.AppendLine($"- Đã có sẵn, KHÔNG dựng: '{r.name}' ({r.x},{r.y} {r.w}×{r.h} px trên demo)"
                              + (string.IsNullOrEmpty(r.prefab) ? " — bỏ trống." : $" — đặt instance {r.prefab} (spec nháp đã có node instance)."));
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
