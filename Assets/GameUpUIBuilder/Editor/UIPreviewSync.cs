using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Đọc ngược cây xem trước trong scene thành <see cref="UISpec"/>: người dùng nhóm lại, đổi tên, đổi thứ tự, kéo rect,
    /// tắt/xoá object bằng thao tác Unity bình thường, rồi bước này ghi lại thành spec.
    /// <para>
    /// Node bị xoá không mất: chuyển sang <see cref="UISpec.excluded"/> để khôi phục lại được và để lần sinh lại spec từ
    /// locate không dựng lại thứ đã bỏ. Object người dùng tự tạo (nhóm) thành node <c>empty</c>.
    /// </para>
    /// </summary>
    public static class UIPreviewSync
    {
        /// <summary>
        /// Spec mới theo cây dưới <paramref name="root"/>; <paramref name="source"/> giữ phần không nằm trong cây
        /// (template, skipRegions, notes…) và dữ liệu gốc của từng node.
        /// </summary>
        public static UISpec Read(RectTransform root, UISpec source)
        {
            var map = UIPreviewMap.instance;
            var spec = UISpecClone.Of(source);
            spec.nodes = new List<UISpecNode>();
            var context = new ReadContext(spec, root, map);
            Walk(root, string.Empty, string.Empty, context);

            CollectExcluded(source, context, spec);
            RemapStateGroups(source, context, spec);
            return spec;
        }

        private static void Walk(Transform parent, string parentId, string parentPath, ReadContext context)
        {
            foreach (Transform child in parent)
            {
                var path = string.IsNullOrEmpty(parentPath) ? child.name : $"{parentPath}/{child.name}";
                var entry = context.Map.Find(child.gameObject, path);
                if (entry != null && entry.role == UIPreviewRole.TemplateChild) continue;

                var node = entry?.source != null ? UISpecClone.Of(entry.source) : NewGroupNode();
                var stored = node.anchor;
                node.id = context.UniqueId(child.name);
                node.parent = parentId ?? string.Empty;
                node.kind = ResolveKind(child.gameObject, entry);
                node.active = child.gameObject.activeSelf;
                if (!string.IsNullOrEmpty(entry?.originalId)) context.Seen[entry.originalId] = node.id;

                var index = context.Spec.nodes.Count;
                context.Spec.nodes.Add(node);
                ApplyRect(node, child, entry, context);
                node.anchor = ResolveAnchor(child, node, stored, !string.IsNullOrEmpty(parentId), context);

                if (node.kind == UISpecNode.KindInstance)
                {
                    node.overrides = ReadOverrides(child, node, context.Spec);
                    continue;
                }

                var container = node.kind == UISpecNode.KindScroll ? child.Find("Viewport/Content") : child;
                var containerPath = container == child ? path : $"{path}/Viewport/Content";
                if (container != null) Walk(container, node.id, containerPath, context);
                // Nhóm người dùng tự tạo bằng menu Unity không có RectTransform → lấy khung bao các node con.
                if (!(child is RectTransform)) WrapChildren(context.Spec, index);
            }
        }

        private static UISpecNode NewGroupNode()
        {
            return new UISpecNode { kind = UISpecNode.KindEmpty, anchor = "auto" };
        }

        /// <summary>
        /// Giữ <c>auto</c> khi anchor hiện tại đúng bằng thứ builder tự chọn — người dùng không đụng anchor thì spec không
        /// bị chốt cứng; anchor sửa tay trong Inspector mới ghi thành tên cụ thể.
        /// </summary>
        private static string ResolveAnchor(Transform child, UISpecNode node, string stored, bool hasParent, ReadContext context)
        {
            if (!(child is RectTransform rt)) return stored;
            var current = UIAnchorNames.Of(rt);
            if (current == null) return stored;
            if (stored == "auto" && current == UISpecBuilder.AutoAnchorName(node, hasParent, context.RootRect)) return "auto";
            return current;
        }

        /// <summary>Component quyết định kiểu node — người dùng thêm Button/Image trong Inspector thì spec đổi theo.</summary>
        private static string ResolveKind(GameObject go, UIPreviewEntry entry)
        {
            if (entry?.source != null && entry.source.kind == UISpecNode.KindInstance) return UISpecNode.KindInstance;
            if (go.GetComponent<ScrollRect>() != null) return UISpecNode.KindScroll;
            if (go.GetComponent<Button>() != null) return UISpecNode.KindButton;
            if (go.GetComponent<TMP_Text>() != null) return UISpecNode.KindText;
            if (go.GetComponent<Image>() != null) return UISpecNode.KindImage;
            return UISpecNode.KindEmpty;
        }

        /// <summary>
        /// Rect của object trên hệ pixel demo (gốc trên-trái) — đọc từ 4 góc nên đúng với mọi anchor và mọi cấp lồng.
        /// Node do LayoutGroup xếp (item trong ScrollRect) giữ rect của spec: vị trí thật là do layout tính, không phải
        /// vị trí đo trên demo.
        /// </summary>
        private static void ApplyRect(UISpecNode node, Transform child, UIPreviewEntry entry, ReadContext context)
        {
            if (!(child is RectTransform rt)) return;
            if (entry?.source != null && child.parent != null && child.parent.GetComponent<LayoutGroup>() != null) return;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var min = context.Root.InverseTransformPoint(corners[0]);
            var max = context.Root.InverseTransformPoint(corners[2]);
            var area = context.Root.rect;

            node.x = Mathf.RoundToInt(min.x - area.xMin);
            node.y = Mathf.RoundToInt(area.yMax - max.y);
            node.w = Mathf.RoundToInt(max.x - min.x);
            node.h = Mathf.RoundToInt(max.y - min.y);
        }

        /// <summary>Nhóm không có RectTransform: rect = khung bao các node con vừa đọc.</summary>
        private static void WrapChildren(UISpec spec, int index)
        {
            var group = spec.nodes[index];
            var children = spec.nodes.Skip(index + 1).Where(n => n.parent == group.id).ToList();
            if (children.Count == 0) return;

            var left = children.Min(n => n.x);
            var top = children.Min(n => n.y);
            group.x = left;
            group.y = top;
            group.w = children.Max(n => n.x + n.w) - left;
            group.h = children.Max(n => n.y + n.h) - top;
        }

        /// <summary>
        /// Instance: phần tử của item prefab bị xoá hoặc tắt trong preview thành <c>hide</c>; override sprite/chữ/màu đã có
        /// của hàng được giữ nguyên.
        /// </summary>
        private static List<UISpecOverride> ReadOverrides(Transform instance, UISpecNode node, UISpec spec)
        {
            var previous = node.overrides ?? new List<UISpecOverride>();
            var template = spec.templates.FirstOrDefault(t => t.output == node.prefab || t.name == node.prefab);
            if (template == null) return previous;

            var present = instance.GetComponentsInChildren<Transform>(true)
                .Where(t => t != instance)
                .GroupBy(t => t.name)
                .ToDictionary(g => g.Key, g => g.First());
            var result = previous.Where(o => string.IsNullOrEmpty(o.id) || o.id == node.id).ToList();

            foreach (var child in template.nodes)
            {
                var hide = !present.TryGetValue(child.id, out var found) || !found.gameObject.activeSelf;
                var existing = previous.FirstOrDefault(o => o.id == child.id);
                if (existing == null && !hide) continue;
                var over = existing ?? new UISpecOverride { id = child.id };
                over.hide = hide;
                result.Add(over);
            }

            return result;
        }

        /// <summary>Node của spec cũ không còn trong cây = người dùng đã xoá, đưa vào danh sách bỏ (không trùng lặp).</summary>
        private static void CollectExcluded(UISpec source, ReadContext context, UISpec spec)
        {
            spec.excluded = new List<UISpecNode>(source.excluded ?? new List<UISpecNode>());
            var known = new HashSet<string>(spec.excluded.Select(n => n.id));
            foreach (var node in source.nodes.Where(n => !context.Seen.ContainsKey(n.id) && known.Add(n.id)))
                spec.excluded.Add(UISpecClone.Of(node));
        }

        /// <summary>Nhóm của từng tab có thể đã bị đổi tên hoặc xoá — cập nhật lại <see cref="UISpec.stateGroups"/>.</summary>
        private static void RemapStateGroups(UISpec source, ReadContext context, UISpec spec)
        {
            if (source.stateGroups == null) return;
            spec.stateGroups = source.stateGroups
                .Select(id => string.IsNullOrEmpty(id) ? id
                    : context.Seen.TryGetValue(id, out var renamed) ? renamed : string.Empty)
                .ToList();
        }

        private sealed class ReadContext
        {
            public readonly UISpec Spec;
            public readonly RectTransform Root;
            public readonly UIPreviewMap Map;

            /// <summary>Khung màn hình tham chiếu — để biết builder sẽ chọn anchor nào cho <c>auto</c>.</summary>
            public readonly RectInt RootRect;

            /// <summary>id node trong spec cũ → id trong spec mới (đổi tên object).</summary>
            public readonly Dictionary<string, string> Seen = new Dictionary<string, string>();

            private readonly HashSet<string> _used = new HashSet<string>();

            public ReadContext(UISpec spec, RectTransform root, UIPreviewMap map)
            {
                Spec = spec;
                Root = root;
                Map = map;
                RootRect = new RectInt(0, 0, spec.referenceWidth, spec.referenceHeight);
            }

            /// <summary>id phải duy nhất trong spec, còn tên object chỉ duy nhất trong cùng cha — thêm hậu tố khi trùng.</summary>
            public string UniqueId(string name)
            {
                var id = string.IsNullOrEmpty(name) ? "node" : name;
                if (_used.Add(id)) return id;
                for (var i = 2; ; i++)
                {
                    var candidate = $"{id}_{i}";
                    if (_used.Add(candidate)) return candidate;
                }
            }
        }
    }
}
