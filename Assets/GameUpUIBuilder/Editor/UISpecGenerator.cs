using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Sinh spec nháp từ kết quả định vị: mỗi vị trí khớp → một node image/button đúng tọa độ, lồng theo quan hệ chứa nhau
    /// (icon nằm trong nút → con của nút). Phần không dò được (text, glow, art thiếu) ghi vào <see cref="UISpec.notes"/>
    /// để người/AI bổ sung.
    /// </summary>
    public static class UISpecGenerator
    {
        private const int ContainTolerance = 2;
        private const int AlignTolerance = 3;
        private const float LowOcrConfidence = 0.9f;
        private const int MaxIdWords = 4;

        public const string PlaceholderText = "Text";

        /// <param name="fontPath">TMP_FontAsset cho các node text tìm được; rỗng = font mặc định TMP.</param>
        public static UISpec Generate(LocateResult locate, string name, string demoAssetPath, string outputPrefabPath, string fontPath)
        {
            var spec = new UISpec
            {
                name = name,
                demo = demoAssetPath,
                referenceWidth = locate.demoWidth,
                referenceHeight = locate.demoHeight,
                output = outputPrefabPath
            };

            var nodes = CreateNodes(locate, spec.notes);
            nodes.AddRange(CreateTextNodes(locate, fontPath, spec.notes, new HashSet<string>(nodes.Select(n => n.id))));
            AssignParents(nodes);
            AssignTextAlignment(nodes, locate.demoWidth);
            spec.nodes = OrderForDrawing(nodes);
            AddUnmatchedNotes(locate, spec.notes);
            return spec;
        }

        private static List<UISpecNode> CreateNodes(LocateResult locate, List<string> notes)
        {
            var nodes = new List<UISpecNode>();
            var usedIds = new HashSet<string>();
            foreach (var sprite in locate.sprites.Where(s => s.IsMatched))
            {
                var assetPath = UIBuilderPaths.ToAssetPath(sprite.sprite);
                if (assetPath == null)
                {
                    notes.Add($"{sprite.name}: file nằm ngoài Assets, không gán sprite được ({sprite.sprite}).");
                    continue;
                }

                var isButton = sprite.name.StartsWith("btn");
                for (var i = 0; i < sprite.matches.Count; i++)
                {
                    var match = sprite.matches[i];
                    var id = UniqueId(sprite.matches.Count > 1 ? $"{sprite.name}_{i + 1}" : sprite.name, usedIds);
                    nodes.Add(new UISpecNode
                    {
                        id = id,
                        kind = isButton ? UISpecNode.KindButton : UISpecNode.KindImage,
                        x = match.x,
                        y = match.y,
                        w = match.w,
                        h = match.h,
                        sprite = assetPath,
                        sliced = match.sliced,
                        raycastTarget = isButton
                    });
                }

                if (sprite.matches.Any(m => m.sliced))
                    AddBorderNote(sprite, assetPath, notes);
            }

            return nodes;
        }

        /// <summary>
        /// Mỗi dòng chữ tìm được → node text đúng khung + màu, cỡ chữ để builder tự tính (0), nội dung từ OCR và id theo
        /// nội dung (<c>txtRemoveAds</c>). OCR không có/không đọc được → chữ giữ chỗ để Claude (/gu-ui) hoặc người điền.
        /// </summary>
        private static List<UISpecNode> CreateTextNodes(LocateResult locate, string fontPath, List<string> notes, HashSet<string> usedIds)
        {
            var nodes = new List<UISpecNode>();
            if (locate.texts == null || locate.texts.Count == 0) return nodes;

            var unsure = new List<string>();
            foreach (var t in locate.texts)
            {
                var hasText = !string.IsNullOrWhiteSpace(t.text);
                var node = new UISpecNode
                {
                    id = UniqueId(hasText ? TextId(t.text) : $"txt_{nodes.Count + 1}", usedIds),
                    kind = UISpecNode.KindText,
                    x = t.x,
                    y = t.y,
                    w = t.w,
                    h = t.h,
                    text = hasText ? t.text : PlaceholderText,
                    color = t.color,
                    font = fontPath
                };
                nodes.Add(node);
                if (!hasText || t.confidence < LowOcrConfidence) unsure.Add($"{node.id} (\"{node.text}\")");
            }

            notes.Add(locate.ocr == "ok"
                ? $"{nodes.Count} dòng chữ: vị trí, màu, cỡ theo demo, nội dung đọc bằng OCR — soát lại ký hiệu đặc biệt (₫, ×, icon trong chữ)."
                : $"{nodes.Count} dòng chữ: vị trí, màu, cỡ theo demo; nội dung đang là \"{PlaceholderText}\" vì venv chưa có OCR "
                  + "(Bước 1 → Cập nhật) — hoặc Copy prompt cho Claude để điền.");
            if (unsure.Count > 0) notes.Add($"OCR không chắc, cần kiểm tra: {string.Join(", ", unsure)}.");
            return nodes;
        }

        /// <summary>"Remove Ads" → txtRemoveAds; "2,000 coins" → txt2000Coins (tối đa 4 từ, bỏ dấu tiếng Việt).</summary>
        public static string TextId(string text)
        {
            var normalized = text.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
            var words = new List<string>();
            var current = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (c < 128 && char.IsLetterOrDigit(c)) current.Append(c);
                else if (c == ' ' || c == '-' || c == '_') FlushWord(current, words);
            }

            FlushWord(current, words);
            var id = string.Concat(words.Take(MaxIdWords).Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));
            return id.Length > 0 ? $"txt{id}" : "txt";
        }

        private static void FlushWord(StringBuilder current, List<string> words)
        {
            if (current.Length > 0) words.Add(current.ToString());
            current.Clear();
        }

        /// <summary>Cha = node nhỏ nhất chứa trọn node này (không tính chính nó).</summary>
        private static void AssignParents(List<UISpecNode> nodes)
        {
            foreach (var node in nodes)
            {
                UISpecNode best = null;
                foreach (var other in nodes)
                {
                    if (other == node || Area(other) <= Area(node) || !Contains(other, node)) continue;
                    if (best == null || Area(other) < Area(best)) best = other;
                }

                node.parent = best != null ? best.id : string.Empty;
            }
        }

        /// <summary>
        /// Căn lề chữ — quan trọng khi nội dung đổi dài/ngắn so với demo: các dòng cùng cha thẳng mép trái (danh sách
        /// cạnh icon) → trái, thẳng mép phải → phải; hai lề cân nhau → giữa; còn lại theo phía gần hơn.
        /// </summary>
        private static void AssignTextAlignment(List<UISpecNode> nodes, int rootWidth)
        {
            var texts = nodes.Where(n => n.kind == UISpecNode.KindText).ToList();
            foreach (var node in texts)
            {
                var siblings = texts.Where(t => t != node && t.parent == node.parent).ToList();
                if (siblings.Any(t => Mathf.Abs(t.x - node.x) <= AlignTolerance))
                {
                    node.align = "left";
                    continue;
                }

                if (siblings.Any(t => Mathf.Abs(t.x + t.w - (node.x + node.w)) <= AlignTolerance))
                {
                    node.align = "right";
                    continue;
                }

                var parent = nodes.FirstOrDefault(n => n.id == node.parent);
                float left = parent?.x ?? 0, width = parent?.w ?? rootWidth;
                var leftMargin = node.x - left;
                var rightMargin = left + width - (node.x + node.w);
                node.align = Mathf.Abs(leftMargin - rightMargin) < width * 0.04f ? "center"
                    : leftMargin < rightMargin ? "left" : "right";
            }
        }

        /// <summary>Cha đứng trước con; cùng cha thì node lớn vẽ trước (nằm dưới).</summary>
        private static List<UISpecNode> OrderForDrawing(List<UISpecNode> nodes)
        {
            var result = new List<UISpecNode>(nodes.Count);
            AppendChildren(string.Empty, nodes, result);
            return result;
        }

        private static void AppendChildren(string parentId, List<UISpecNode> nodes, List<UISpecNode> result)
        {
            foreach (var child in nodes.Where(n => n.parent == parentId).OrderByDescending(Area).ThenBy(n => n.y))
            {
                result.Add(child);
                AppendChildren(child.id, nodes, result);
            }
        }

        private static void AddBorderNote(LocateSprite sprite, string assetPath, List<string> notes)
        {
            var loaded = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (loaded != null && loaded.border != Vector4.zero) return;
            var hint = sprite.suggestedBorder != null ? $" — gợi ý {sprite.suggestedBorder}" : string.Empty;
            notes.Add($"{sprite.name}: dùng dạng 9-slice nhưng sprite chưa có border, cần set trong Sprite Editor{hint}.");
        }

        private static void AddUnmatchedNotes(LocateResult locate, List<string> notes)
        {
            var soft = locate.sprites.Where(s => s.reason == "soft-alpha").Select(s => s.name).ToList();
            if (soft.Count > 0)
                notes.Add($"Glow/bán trong suốt, không dò được bằng hình — ước lượng vị trí từ demo: {string.Join(", ", soft)}.");
            notes.Add("Chưa có: vùng art thiếu, chữ nằm ngoài sprite đã khớp, phần nền gameplay phía sau (không thuộc prefab) — bổ sung khi review spec.");
        }

        private static string UniqueId(string baseId, HashSet<string> used)
        {
            var id = baseId;
            for (var i = 2; !used.Add(id); i++) id = $"{baseId}_{i}";
            return id;
        }

        private static long Area(UISpecNode n) => (long)n.w * n.h;

        private static bool Contains(UISpecNode outer, UISpecNode inner)
        {
            return inner.x >= outer.x - ContainTolerance
                   && inner.y >= outer.y - ContainTolerance
                   && inner.x + inner.w <= outer.x + outer.w + ContainTolerance
                   && inner.y + inner.h <= outer.y + outer.h + ContainTolerance;
        }
    }
}
