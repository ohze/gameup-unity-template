using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Sửa cấu trúc cây của spec từ cửa sổ UI Builder: nhóm lại, đổi tên, đổi cha, đổi thứ tự vẽ, bỏ/dựng lại node.
    /// <para>
    /// <see cref="UISpec.nodes"/> là danh sách phẳng, thứ tự = thứ tự vẽ và cha luôn đứng trước con. Mọi thao tác ở đây
    /// sửa trên cây rồi trải lại danh sách theo đúng luật đó.
    /// </para>
    /// </summary>
    public static class UISpecEdit
    {
        /// <summary>Con trực tiếp của <paramref name="parentId"/> (rỗng = cấp gốc), theo thứ tự vẽ.</summary>
        public static List<UISpecNode> ChildrenOf(UISpec spec, string parentId)
        {
            return spec.nodes.Where(n => (n.parent ?? string.Empty) == (parentId ?? string.Empty)).ToList();
        }

        /// <summary>Trải lại danh sách phẳng: cha trước con, giữ nguyên thứ tự anh em.</summary>
        public static void SortByParent(UISpec spec)
        {
            Apply(spec, ChildMap(spec));
        }

        /// <summary>
        /// Gom các node đang chọn vào một node <c>empty</c> mới cùng cha, khung bao vừa đủ. Trả lỗi (tiếng Việt) nếu
        /// không gom được, null nếu xong.
        /// </summary>
        public static string Group(UISpec spec, IReadOnlyList<string> ids, string groupId)
        {
            var selected = spec.nodes.Where(n => ids.Contains(n.id)).ToList();
            if (selected.Count == 0) return "Chọn ít nhất một node trong cây.";

            var parent = selected[0].parent ?? string.Empty;
            if (selected.Any(n => (n.parent ?? string.Empty) != parent))
                return "Chỉ nhóm được các node cùng một cha.";

            var name = Sanitize(groupId);
            if (string.IsNullOrEmpty(name)) return "Đặt tên cho nhóm trước.";
            if (Exists(spec, name)) return $"Đã có node tên '{name}'.";

            var group = new UISpecNode
            {
                id = name,
                parent = parent,
                kind = UISpecNode.KindEmpty,
                anchor = "auto",
                x = selected.Min(n => n.x),
                y = selected.Min(n => n.y)
            };
            group.w = selected.Max(n => n.x + n.w) - group.x;
            group.h = selected.Max(n => n.y + n.h) - group.y;

            var map = ChildMap(spec);
            var siblings = map[parent];
            var index = siblings.IndexOf(selected[0]);
            foreach (var node in selected)
            {
                siblings.Remove(node);
                node.parent = name;
            }

            siblings.Insert(Mathf.Clamp(index, 0, siblings.Count), group);
            map[name] = selected;
            spec.nodes.Add(group);
            Apply(spec, map);
            return null;
        }

        /// <summary>Đổi <c>id</c> node và mọi chỗ trỏ tới nó (cha của con, nhóm tab). Trả lỗi hoặc null.</summary>
        public static string Rename(UISpec spec, string oldId, string newId)
        {
            var node = spec.nodes.FirstOrDefault(n => n.id == oldId) ?? spec.excluded.FirstOrDefault(n => n.id == oldId);
            if (node == null) return $"Không thấy node '{oldId}'.";

            var name = Sanitize(newId);
            if (string.IsNullOrEmpty(name)) return "Tên node không được rỗng.";
            if (name == oldId) return null;
            if (Exists(spec, name)) return $"Đã có node tên '{name}'.";

            node.id = name;
            foreach (var other in spec.nodes.Concat(spec.excluded))
                if (other.parent == oldId) other.parent = name;
            for (var i = 0; i < spec.stateGroups.Count; i++)
                if (spec.stateGroups[i] == oldId) spec.stateGroups[i] = name;
            return null;
        }

        /// <summary>
        /// Chuyển các node sang cha mới, chèn vào vị trí <paramref name="index"/> trong danh sách con (âm = cuối).
        /// Node không chuyển vào chính nó hoặc vào con cháu của nó.
        /// </summary>
        public static void Reparent(UISpec spec, IReadOnlyList<string> ids, string newParentId, int index)
        {
            var parent = newParentId ?? string.Empty;
            var moving = spec.nodes.Where(n => ids.Contains(n.id) && !IsAncestorOf(spec, n.id, parent) && n.id != parent).ToList();
            if (moving.Count == 0) return;

            var map = ChildMap(spec);
            foreach (var node in moving) map[node.parent ?? string.Empty].Remove(node);
            if (!map.TryGetValue(parent, out var target)) map[parent] = target = new List<UISpecNode>();

            var at = index < 0 ? target.Count : Mathf.Clamp(index, 0, target.Count);
            for (var i = 0; i < moving.Count; i++)
            {
                moving[i].parent = parent;
                target.Insert(at + i, moving[i]);
            }

            Apply(spec, map);
        }

        /// <summary>Đổi chỗ node với anh em liền trước/sau — đổi thứ tự vẽ (<paramref name="delta"/> −1 = lên trên).</summary>
        public static void Move(UISpec spec, string id, int delta)
        {
            var node = spec.nodes.FirstOrDefault(n => n.id == id);
            if (node == null) return;

            var map = ChildMap(spec);
            var siblings = map[node.parent ?? string.Empty];
            var from = siblings.IndexOf(node);
            var to = from + delta;
            if (to < 0 || to >= siblings.Count) return;

            siblings[from] = siblings[to];
            siblings[to] = node;
            Apply(spec, map);
        }

        /// <summary>Bỏ node (và cả nhánh con) khỏi phần sẽ dựng — chuyển sang <see cref="UISpec.excluded"/>.</summary>
        public static void Exclude(UISpec spec, string id)
        {
            var node = spec.nodes.FirstOrDefault(n => n.id == id);
            if (node == null) return;

            foreach (var child in ChildrenOf(spec, id).ToList()) Exclude(spec, child.id);
            spec.nodes.Remove(node);
            if (spec.excluded.All(n => n.id != node.id)) spec.excluded.Add(node);
        }

        /// <summary>id node đã có trong spec (kể cả phần đã bỏ).</summary>
        public static bool Exists(UISpec spec, string id)
        {
            return spec.nodes.Any(n => n.id == id) || spec.excluded.Any(n => n.id == id);
        }

        private static bool IsAncestorOf(UISpec spec, string ancestorId, string nodeId)
        {
            for (var id = nodeId; !string.IsNullOrEmpty(id);)
            {
                if (id == ancestorId) return true;
                id = spec.nodes.FirstOrDefault(n => n.id == id)?.parent;
            }

            return false;
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().Replace('/', '_').Replace(' ', '_');
        }

        private static Dictionary<string, List<UISpecNode>> ChildMap(UISpec spec)
        {
            var map = new Dictionary<string, List<UISpecNode>>();
            foreach (var node in spec.nodes)
            {
                var key = node.parent ?? string.Empty;
                if (!map.TryGetValue(key, out var list)) map[key] = list = new List<UISpecNode>();
                list.Add(node);
            }

            return map;
        }

        private static void Apply(UISpec spec, Dictionary<string, List<UISpecNode>> map)
        {
            var sorted = new List<UISpecNode>(spec.nodes.Count);
            AppendChildren(map, string.Empty, sorted);
            // Node có cha không tồn tại (spec sửa tay) vẫn giữ lại để không mất dữ liệu.
            sorted.AddRange(spec.nodes.Where(n => !sorted.Contains(n)));
            spec.nodes = sorted;
        }

        private static void AppendChildren(Dictionary<string, List<UISpecNode>> map, string parentId, List<UISpecNode> output)
        {
            if (!map.TryGetValue(parentId, out var children)) return;
            foreach (var node in children)
            {
                if (output.Contains(node)) continue; // spec sửa tay tạo vòng cha-con
                output.Add(node);
                AppendChildren(map, node.id, output);
            }
        }
    }
}
