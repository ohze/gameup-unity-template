using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Nhận ra danh sách trong demo (≥ 3 khung cùng sprite, cùng cỡ, cùng cột, cách đều) và chuyển thành:
    /// node <c>scroll</c> (ScrollRect + LayoutGroup) chứa các <c>instance</c> của một item prefab (<see cref="UITemplateSpec"/>).
    /// <para>
    /// Template = hợp nội dung mọi hàng theo vị trí tương đối (hàng 1 có vương miện, hàng 4 có số hạng → template có cả hai);
    /// mỗi instance ghi đè phần khác: ẩn phần tử hàng đó không có, đổi sprite (vương miện top 2/3), đổi chữ.
    /// Hàng lẻ có bố cục giống item (hàng hạng của người chơi, nền khác) cũng thành instance, ghi đè nền.
    /// </para>
    /// </summary>
    public static class UIListExtractor
    {
        public const string BackgroundId = "imgBg";

        private const int MinRows = 3;
        private const int ColumnTolerance = 4;
        private const int StepTolerance = 8;
        private const float SlotIou = 0.5f;
        private const float SimilarSize = 0.1f;
        private const float SimilarChildren = 0.6f;

        /// <summary>Tách danh sách trong <paramref name="nodes"/> (tọa độ tuyệt đối), thêm template vào <paramref name="templates"/>.</summary>
        public static void ExtractLists(List<UISpecNode> nodes, IList<string> templateNames, string folder,
            List<UITemplateSpec> templates, HashSet<string> usedIds, List<string> notes)
        {
            var groups = nodes
                .Where(n => (n.kind == UISpecNode.KindImage || n.kind == UISpecNode.KindButton) && !string.IsNullOrEmpty(n.sprite))
                .GroupBy(n => (n.sprite, n.w, n.h, column: Mathf.RoundToInt((float)n.x / ColumnTolerance)))
                .Where(g => g.Count() >= MinRows)
                .Select(g => g.OrderBy(n => n.y).ToList())
                .Where(IsEvenlySpaced)
                .OrderByDescending(rows => (long)rows[0].w * rows[0].h) // hàng lớn trước: avatar xếp cột trong hàng không thành danh sách riêng
                .ToList();

            var index = 0;
            foreach (var rows in groups)
            {
                if (rows.Any(r => !nodes.Contains(r))) continue; // đã thuộc một danh sách lớn hơn
                var name = index < templateNames.Count ? templateNames[index] : $"{templateNames.LastOrDefault() ?? "List"}{index + 1}";
                index++;
                var template = new UITemplateSpec
                {
                    name = name,
                    output = $"{folder}/{name}.prefab",
                    width = rows[0].w,
                    height = rows[0].h
                };
                template.nodes.Add(new UISpecNode
                {
                    id = BackgroundId, kind = UISpecNode.KindImage, w = rows[0].w, h = rows[0].h,
                    sprite = rows[0].sprite, sliced = rows[0].sliced, color = rows[0].color
                });

                // 2 lượt: gom đủ slot từ mọi hàng trước, rồi mới tạo instance — để hàng nào cũng ẩn đúng slot nó không có.
                var children = rows.Select(row => ChildrenOf(row, nodes, rows)).ToList();
                for (var r = 0; r < rows.Count; r++) AddSlots(template, rows[r], children[r]);
                FinishTemplate(template);
                var instances = rows.Select((row, index) => ToInstance(row, $"{name}_{index + 1}", children[index], template)).ToList();
                var step = rows.Count > 1 ? rows[1].y - rows[0].y : rows[0].h;
                var scroll = new UISpecNode
                {
                    id = UISpecGenerator.UniqueId($"scroll{name}", usedIds),
                    kind = UISpecNode.KindScroll,
                    x = rows.Min(r => r.x),
                    y = rows[0].y,
                    w = rows.Max(r => r.x + r.w) - rows.Min(r => r.x),
                    h = rows[rows.Count - 1].y + rows[rows.Count - 1].h - rows[0].y,
                    spacing = step - rows[0].h
                };
                foreach (var instance in instances)
                {
                    instance.id = UISpecGenerator.UniqueId(instance.id, usedIds);
                    instance.parent = scroll.id;
                }

                RemoveWithChildren(nodes, rows);
                nodes.Add(scroll);
                nodes.AddRange(instances);
                templates.Add(template);
                notes.Add($"Danh sách {rows.Count} hàng → {scroll.id} (ScrollRect, spacing {scroll.spacing}px) + item prefab {name} "
                          + $"({template.nodes.Count} node). Avatar/ô vật phẩm không có art → thêm vào {name} rồi build lại.");
            }
        }

        /// <summary>
        /// Hàng lẻ có bố cục giống một item (≥ 60% phần tử khớp vị trí tương đối, cỡ lệch ≤ 10%) → instance của item đó,
        /// ghi đè nền. Chọn template khớp nhất trong <paramref name="templates"/> (có thể của trạng thái/tab khác).
        /// </summary>
        public static void ExtractSimilarRows(List<UISpecNode> nodes, List<UITemplateSpec> templates, HashSet<string> usedIds,
            List<string> notes)
        {
            var candidates = nodes
                .Where(n => (n.kind == UISpecNode.KindImage || n.kind == UISpecNode.KindButton) && !string.IsNullOrEmpty(n.sprite)
                            && string.IsNullOrEmpty(n.parent))
                .ToList();
            foreach (var row in candidates)
            {
                var children = ChildrenOf(row, nodes, new List<UISpecNode> { row });
                if (children.Count == 0) continue;

                UITemplateSpec best = null;
                var bestScore = 0f;
                foreach (var template in templates.Where(t => IsSimilarSize(row, t)))
                {
                    var score = children.Count(c => FindSlot(template, Relative(c, row), c.kind) != null) / (float)children.Count;
                    if (score > bestScore) (best, bestScore) = (template, score);
                }

                if (best == null || bestScore < SimilarChildren) continue;
                var instance = ToInstance(row, UISpecGenerator.UniqueId($"{best.name}_{row.id}", usedIds), children, best);
                RemoveWithChildren(nodes, new List<UISpecNode> { row });
                nodes.Add(instance);
                notes.Add($"{row.id}: bố cục giống {best.name} → instance của {best.name}, ghi đè nền.");
            }
        }

        private static bool IsEvenlySpaced(List<UISpecNode> rows)
        {
            var steps = rows.Zip(rows.Skip(1), (a, b) => b.y - a.y).ToList();
            var step = steps.OrderBy(s => s).ElementAt(steps.Count / 2);
            return step >= rows[0].h * 0.8f && steps.All(s => Mathf.Abs(s - step) <= StepTolerance);
        }

        private static List<UISpecNode> ChildrenOf(UISpecNode row, List<UISpecNode> nodes, List<UISpecNode> rows)
        {
            return nodes.Where(n => !rows.Contains(n) && n.kind != UISpecNode.KindScroll && n.kind != UISpecNode.KindInstance
                                    && UISpecGenerator.Contains(row, n)).ToList();
        }

        /// <summary>Phần tử của hàng chưa có chỗ trong template (theo loại + vị trí tương đối) → thêm slot mới.</summary>
        private static void AddSlots(UITemplateSpec template, UISpecNode row, List<UISpecNode> children)
        {
            foreach (var child in children)
            {
                var relative = Relative(child, row);
                if (FindSlot(template, relative, child.kind) != null) continue;
                relative.id = UniqueSlotId(template, child);
                template.nodes.Add(relative);
            }
        }

        /// <summary>Hàng → instance: ghi đè nền nếu khác, sprite/chữ của từng slot, ẩn slot hàng này không có.</summary>
        private static UISpecNode ToInstance(UISpecNode row, string id, List<UISpecNode> children, UITemplateSpec template)
        {
            var instance = new UISpecNode
            {
                id = id, kind = UISpecNode.KindInstance, prefab = template.output,
                x = row.x, y = row.y, w = row.w, h = row.h, anchor = row.anchor
            };
            var background = template.nodes[0];
            if (row.sprite != background.sprite)
                instance.overrides.Add(new UISpecOverride { id = BackgroundId, sprite = row.sprite });

            var filled = new HashSet<string>();
            foreach (var child in children)
            {
                var slot = FindSlot(template, Relative(child, row), child.kind);
                if (slot == null || !filled.Add(slot.id)) continue;
                if (!string.IsNullOrEmpty(child.sprite) && child.sprite != slot.sprite)
                    instance.overrides.Add(new UISpecOverride { id = slot.id, sprite = child.sprite });
                if (child.kind == UISpecNode.KindText && child.text != slot.text)
                    instance.overrides.Add(new UISpecOverride { id = slot.id, setText = true, text = child.text });
            }

            instance.overrides.AddRange(template.nodes.Skip(1).Where(n => !filled.Contains(n.id))
                .Select(n => new UISpecOverride { id = n.id, hide = true }));
            return instance;
        }

        /// <summary>Nền đứng đầu, slot lớn vẽ trước; căn lề chữ theo cột như màn chính.</summary>
        private static void FinishTemplate(UITemplateSpec template)
        {
            template.nodes = template.nodes.Take(1).Concat(template.nodes.Skip(1).OrderByDescending(n => (long)n.w * n.h)).ToList();
            foreach (var node in template.nodes.Skip(1)) node.parent = string.Empty;
            UISpecGenerator.AssignTextAlignment(template.nodes, template.width);
        }

        /// <summary>Slot cùng loại ở cùng chỗ: ảnh theo độ chồng khung; chữ theo tâm ("4-10" và "11-20" dài ngắn khác nhau).</summary>
        private static UISpecNode FindSlot(UITemplateSpec template, UISpecNode relative, string kind)
        {
            return template.nodes.Skip(1)
                .Where(n => n.kind == kind && (kind == UISpecNode.KindText ? SameTextSpot(n, relative) : Iou(n, relative) >= SlotIou))
                .OrderByDescending(n => Iou(n, relative))
                .FirstOrDefault();
        }

        private static bool SameTextSpot(UISpecNode a, UISpecNode b)
        {
            var dx = Mathf.Abs(a.x + a.w / 2f - (b.x + b.w / 2f));
            var dy = Mathf.Abs(a.y + a.h / 2f - (b.y + b.h / 2f));
            return dx <= Mathf.Max(a.w, b.w) / 2f && dy <= Mathf.Max(a.h, b.h) / 2f;
        }

        private static UISpecNode Relative(UISpecNode node, UISpecNode row)
        {
            return new UISpecNode
            {
                id = node.id, kind = node.kind, x = node.x - row.x, y = node.y - row.y, w = node.w, h = node.h,
                sprite = node.sprite, sliced = node.sliced, color = node.color, raycastTarget = node.raycastTarget,
                text = node.text, font = node.font, fontSize = node.fontSize, align = node.align, bold = node.bold,
                material = node.material, outlineWidth = node.outlineWidth, outlineColor = node.outlineColor
            };
        }

        private static string UniqueSlotId(UITemplateSpec template, UISpecNode child)
        {
            var baseId = child.kind == UISpecNode.KindText
                ? UISpecGenerator.TextId(child.text ?? string.Empty)
                : System.IO.Path.GetFileNameWithoutExtension(child.sprite ?? child.id);
            var used = new HashSet<string>(template.nodes.Select(n => n.id));
            return UISpecGenerator.UniqueId(baseId, used);
        }

        private static bool IsSimilarSize(UISpecNode row, UITemplateSpec template)
        {
            return Mathf.Abs(row.w - template.width) <= template.width * SimilarSize
                   && Mathf.Abs(row.h - template.height) <= template.height * SimilarSize;
        }

        private static void RemoveWithChildren(List<UISpecNode> nodes, List<UISpecNode> rows)
        {
            var removed = new HashSet<UISpecNode>(rows);
            foreach (var row in rows)
                foreach (var child in ChildrenOf(row, nodes, rows)) removed.Add(child);
            nodes.RemoveAll(removed.Contains);
        }

        private static float Iou(UISpecNode a, UISpecNode b)
        {
            var ix = Mathf.Max(0, Mathf.Min(a.x + a.w, b.x + b.w) - Mathf.Max(a.x, b.x));
            var iy = Mathf.Max(0, Mathf.Min(a.y + a.h, b.y + b.h) - Mathf.Max(a.y, b.y));
            var inter = (float)ix * iy;
            return inter <= 0f ? 0f : inter / ((float)a.w * a.h + (float)b.w * b.h - inter);
        }
    }
}
