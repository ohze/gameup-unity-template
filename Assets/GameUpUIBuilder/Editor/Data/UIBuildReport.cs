using System.Collections.Generic;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Kết quả một lần dựng prefab: đã tạo/cập nhật gì và cảnh báo cần người xử lý.</summary>
    public class UIBuildReport
    {
        public string PrefabPath;
        public bool Success;
        public int Created;
        public int Updated;
        public int BoundFields;
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public string Summary => Success
            ? $"{PrefabPath}: tạo {Created}, cập nhật {Updated} node, gán {BoundFields} field, {Warnings.Count} cảnh báo."
            : $"Dựng thất bại: {string.Join("; ", Errors)}";
    }
}
