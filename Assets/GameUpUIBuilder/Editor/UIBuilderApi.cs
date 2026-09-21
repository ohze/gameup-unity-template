using System.Text;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Điểm vào một lệnh cho AI qua Unity MCP (<c>eval</c>, ghi tên đầy đủ vì eval không nhận <c>using</c>):
    /// <code>return GameUp.UIBuilder.Editor.UIBuilderApi.BuildAndCompare("PopupResult");</code>
    /// Dựng/cập nhật prefab từ <c>UIBuilder/&lt;job&gt;/spec.json</c>, render ảnh so sánh, trả về báo cáo dạng text.
    /// </summary>
    public static class UIBuilderApi
    {
        public static string BuildAndCompare(string jobName)
        {
            var spec = UISpecFile.Load(UIBuilderPaths.SpecPath(jobName), out var error);
            if (spec == null) return $"LỖI: {error}";

            var report = UISpecBuilder.Build(spec);
            var sb = new StringBuilder(report.Summary).AppendLine();
            foreach (var warning in report.Warnings) sb.AppendLine($"⚠ {warning}");
            if (!report.Success) return sb.ToString();

            var compares = UIPrefabRenderer.RenderCompare(spec, UIBuilderPaths.JobFolder(jobName));
            sb.AppendLine(compares.Count > 0
                ? $"Ảnh so sánh (demo | prefab | chồng 50%), mỗi trạng thái/tab một ảnh: {string.Join(", ", compares)}"
                : "Không render được ảnh so sánh (thiếu demo?).");
            return sb.ToString();
        }
    }
}
