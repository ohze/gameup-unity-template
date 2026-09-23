using System.Collections.Generic;
using System.Linq;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Danh sách node đã loại ở bước xem trước (<see cref="UISpec.excluded"/>): giữ qua mỗi lần sinh lại spec từ kết quả
    /// định vị, và khôi phục lại được khi đổi ý.
    /// </summary>
    public static class UISpecExclusions
    {
        /// <summary>Áp quyết định đã loại của spec cũ lên spec vừa sinh — node cùng id (và con của nó) không dựng lại.</summary>
        public static void Carry(UISpec fresh, UISpec previous)
        {
            if (previous?.excluded == null || previous.excluded.Count == 0) return;
            fresh.excluded = previous.excluded.Select(UISpecClone.Of).ToList();

            var dropped = new HashSet<string>(fresh.excluded.Select(n => n.id));
            var kept = new List<UISpecNode>();
            foreach (var node in fresh.nodes)
            {
                var orphan = !string.IsNullOrEmpty(node.parent) && dropped.Contains(node.parent);
                if (!dropped.Contains(node.id) && !orphan)
                {
                    kept.Add(node);
                    continue;
                }

                // Cha đã loại thì cả nhánh con theo cùng — ghi lại để khôi phục được cả cụm.
                if (dropped.Add(node.id)) fresh.excluded.Add(UISpecClone.Of(node));
            }

            fresh.nodes = kept;
        }

        /// <summary>Đưa một node đã loại (kèm các cha còn đang bị loại) trở lại cây, đặt cuối danh sách con của cha.</summary>
        public static bool Restore(UISpec spec, string id)
        {
            var node = spec.excluded.FirstOrDefault(n => n.id == id);
            if (node == null) return false;

            spec.excluded.Remove(node);
            if (!string.IsNullOrEmpty(node.parent) && spec.nodes.All(n => n.id != node.parent)) Restore(spec, node.parent);
            spec.nodes.Add(node);
            UISpecEdit.SortByParent(spec);
            return true;
        }

        public static void RestoreAll(UISpec spec)
        {
            foreach (var id in spec.excluded.Select(n => n.id).ToList()) Restore(spec, id);
        }
    }
}
