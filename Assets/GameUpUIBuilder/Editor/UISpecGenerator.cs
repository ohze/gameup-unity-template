using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

        private const int SameTolerance = 4;

        /// <param name="fontPath">TMP_FontAsset cho các node text tìm được; rỗng = font mặc định TMP.</param>
        public static UISpec Generate(LocateResult locate, string name, string demoAssetPath, string outputPrefabPath, string fontPath)
        {
            return Generate(new[] { locate }, name, new[] { demoAssetPath }, outputPrefabPath, fontPath);
        }

        /// <summary>
        /// Nhiều demo = nhiều trạng thái của cùng một UI (vd 2 tab). Node giống hệt ở mọi demo dựng một lần; phần riêng của
        /// demo k nằm trong nhóm <c>grp{NhãnTab}</c> (chỉ nhóm đầu bật). Danh sách mỗi tab thành ScrollRect + item prefab
        /// riêng, đặt tên theo nhãn tab ("Leaderboard" → LeaderboardItem).
        /// </summary>
        public static UISpec Generate(IReadOnlyList<LocateResult> states, string name, IReadOnlyList<string> demoAssetPaths,
            string outputPrefabPath, string fontPath)
        {
            var spec = new UISpec
            {
                name = name,
                demo = demoAssetPaths[0],
                extraDemos = demoAssetPaths.Skip(1).ToList(),
                referenceWidth = states[0].demoWidth,
                referenceHeight = states[0].demoHeight,
                output = outputPrefabPath
            };

            var perState = new List<List<UISpecNode>>();
            for (var k = 0; k < states.Count; k++)
            {
                var notes = k == 0 ? spec.notes : new List<string>();
                var nodes = CreateNodes(states[k], notes);
                nodes.AddRange(CreateTextNodes(states[k], fontPath, notes, new HashSet<string>(nodes.Select(n => n.id))));
                perState.Add(nodes);
            }

            var labels = TabLabels(perState[0]);
            var folder = Path.GetDirectoryName(outputPrefabPath)?.Replace('\\', '/');
            var usedIds = new HashSet<string>(perState.SelectMany(n => n).Select(n => n.id));
            for (var k = 0; k < states.Count; k++)
                UIListExtractor.ExtractLists(perState[k], TemplateNames(labels, k, states.Count, name), folder, spec.templates, usedIds, spec.notes);
            foreach (var nodes in perState)
                UIListExtractor.ExtractSimilarRows(nodes, spec.templates, usedIds, spec.notes);

            var merged = states.Count == 1 ? perState[0] : MergeStates(perState, labels, spec.notes, spec.stateGroups);
            if (states.Count == 1) AssignParents(merged, merged, string.Empty);
            AddDim(merged, states[0], spec.notes); // sau khi gán cha — lớp phủ kín màn không được làm cha của mọi thứ
            AssignTextAlignment(merged, states[0].demoWidth);
            spec.nodes = OrderForDrawing(merged);
            AddUnmatchedNotes(states[0], spec.notes);
            spec.notes = spec.notes.Distinct().ToList();
            return spec;
        }

        /// <summary>Gameplay phía sau bị tối đều (đo từ icon HUD bị tint xám) → lớp dim đen phủ màn, vẽ dưới cùng.</summary>
        private static void AddDim(List<UISpecNode> nodes, LocateResult locate, List<string> notes)
        {
            if (locate.dimAlpha <= 0f) return;
            var alpha = Mathf.Clamp(Mathf.RoundToInt(locate.dimAlpha * 255f), 0, 255);
            nodes.Insert(0, new UISpecNode
            {
                id = UniqueId("imgDim", new HashSet<string>(nodes.Select(n => n.id))), kind = UISpecNode.KindImage,
                w = locate.demoWidth, h = locate.demoHeight, anchor = "stretch", color = $"#000000{alpha:X2}",
                raycastTarget = true, flat = true
            });
            notes.Add($"imgDim: lớp dim đen {locate.dimAlpha:P0} đo từ gameplay bị làm tối phía sau.");
        }

        // ─── Nhiều trạng thái (tab) ──────────────────────────────────────────

        /// <summary>Nhãn tab: chữ nằm trên sprite tên có "tab", trái → phải ("Leaderboard", "Ranking Rewards").</summary>
        private static List<string> TabLabels(List<UISpecNode> nodes)
        {
            var tabs = nodes.Where(n => !string.IsNullOrEmpty(n.sprite)
                                        && Path.GetFileNameWithoutExtension(n.sprite).ToLowerInvariant().Contains("tab")).ToList();
            return nodes.Where(n => n.kind == UISpecNode.KindText && !string.IsNullOrWhiteSpace(n.text) && tabs.Any(t => Contains(t, n)))
                .OrderBy(n => n.x).Select(n => n.text).Distinct().ToList();
        }

        private static List<string> TemplateNames(List<string> labels, int state, int stateCount, string uiName)
        {
            var baseName = state < labels.Count ? TextId(labels[state]).Substring(3)
                : stateCount > 1 ? $"{uiName}Tab{state + 1}" : uiName;
            if (string.IsNullOrEmpty(baseName)) baseName = $"{uiName}Tab{state + 1}";
            return new List<string> { $"{baseName}Item", $"{baseName}Item2", $"{baseName}Item3" };
        }

        private static List<UISpecNode> MergeStates(List<List<UISpecNode>> perState, List<string> labels, List<string> notes,
            List<string> stateGroups)
        {
            var shared = perState[0].Where(a => perState.Skip(1).All(other => other.Any(b => Same(a, b, perState[0], other)))).ToList();
            var result = new List<UISpecNode>(shared);
            AssignParents(shared, shared, string.Empty);
            var used = new HashSet<string>(shared.Select(n => n.id));

            for (var k = 0; k < perState.Count; k++)
            {
                var own = perState[k].Where(n => !shared.Any(s => Same(s, n, perState[0], perState[k]))).ToList();
                if (own.Count == 0)
                {
                    stateGroups.Add(string.Empty);
                    continue;
                }
                RenameCollisions(own, used, k);

                var groupId = UniqueId(k < labels.Count ? $"grp{TextId(labels[k]).Substring(3)}" : $"grpState{k + 1}", used);
                var tops = own.Where(n => string.IsNullOrEmpty(n.parent)).ToList();
                var group = new UISpecNode
                {
                    id = groupId, kind = UISpecNode.KindEmpty, active = k == 0,
                    x = tops.Min(n => n.x), y = tops.Min(n => n.y),
                    w = tops.Max(n => n.x + n.w) - tops.Min(n => n.x), h = tops.Max(n => n.y + n.h) - tops.Min(n => n.y)
                };
                group.parent = SmallestContainer(group, shared)?.id ?? string.Empty;
                stateGroups.Add(groupId);
                AssignParents(own, own, groupId);
                result.Add(group);
                result.AddRange(own);
                notes.Add($"Trạng thái {k + 1}{(k < labels.Count ? $" (tab \"{labels[k]}\")" : string.Empty)}: {own.Count} node riêng trong {groupId}"
                          + (k == 0 ? " (bật)." : " (ẩn — bật khi chọn tab)."));
            }

            return result;
        }

        /// <summary>id trùng với node đã có (vd "txtPlayerName" ở cả 2 tab) → thêm hậu tố, sửa luôn tham chiếu cha.</summary>
        private static void RenameCollisions(List<UISpecNode> own, HashSet<string> used, int state)
        {
            var renamed = new Dictionary<string, string>();
            foreach (var node in own)
            {
                var id = used.Contains(node.id) ? UniqueId($"{node.id}_tab{state + 1}", used) : node.id;
                used.Add(id);
                if (id != node.id) renamed[node.id] = id;
                node.id = id;
            }

            foreach (var node in own)
                if (!string.IsNullOrEmpty(node.parent) && renamed.TryGetValue(node.parent, out var parent)) node.parent = parent;
        }

        /// <summary>
        /// Cùng một phần tử ở 2 demo: cùng loại, sprite, chữ, prefab, ghi đè, lệch ≤ 4 px. Chữ so theo nội dung + tâm (khung
        /// nét chữ lệch vài px giữa 2 ảnh). Scroll so cả item bên trong — 2 tab cùng chỗ nhưng khác danh sách.
        /// </summary>
        private static bool Same(UISpecNode a, UISpecNode b, List<UISpecNode> nodesA, List<UISpecNode> nodesB)
        {
            if (a.kind != b.kind || a.sprite != b.sprite || a.text != b.text || a.prefab != b.prefab || OverridesKey(a) != OverridesKey(b))
                return false;
            if (a.kind == UISpecNode.KindText)
                return Mathf.Abs(a.x + a.w / 2f - (b.x + b.w / 2f)) <= SameTolerance * 2
                       && Mathf.Abs(a.y + a.h / 2f - (b.y + b.h / 2f)) <= SameTolerance * 2;
            if (a.kind == UISpecNode.KindScroll && ScrollItems(a, nodesA) != ScrollItems(b, nodesB))
                return false;
            return Mathf.Abs(a.x - b.x) <= SameTolerance && Mathf.Abs(a.y - b.y) <= SameTolerance
                   && Mathf.Abs(a.w - b.w) <= SameTolerance && Mathf.Abs(a.h - b.h) <= SameTolerance;
        }

        private static string ScrollItems(UISpecNode scroll, List<UISpecNode> nodes)
        {
            return string.Join(",", nodes.Where(n => n.parent == scroll.id).Select(n => n.prefab).Distinct());
        }

        private static string OverridesKey(UISpecNode node)
        {
            return string.Join("|", node.overrides.Select(o => $"{o.id}:{o.hide}:{o.sprite}:{o.setText}:{o.text}"));
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
                        flat = sprite.lowTexture,
                        kind = isButton ? UISpecNode.KindButton : UISpecNode.KindImage,
                        x = match.x,
                        y = match.y,
                        w = match.w,
                        h = match.h,
                        sprite = assetPath,
                        sliced = match.sliced,
                        color = match.tint,
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

        /// <summary>
        /// Cha = node nhỏ nhất chứa trọn node này trong <paramref name="candidates"/>, không có thì <paramref name="fallback"/>.
        /// Node đã có cha (instance trong scroll) giữ nguyên; instance/scroll/text không làm cha theo hình học.
        /// </summary>
        private static void AssignParents(List<UISpecNode> nodes, List<UISpecNode> candidates, string fallback)
        {
            foreach (var node in nodes.Where(n => string.IsNullOrEmpty(n.parent)))
                node.parent = SmallestContainer(node, candidates)?.id ?? fallback;
        }

        /// <summary>
        /// Node nhỏ nhất chứa trọn <paramref name="node"/>. Ưu tiên sprite có hoạ tiết hơn sprite phẳng: nhãn tab nằm trong
        /// cả nút tab lẫn nền thanh tab — phải là con của nút (nền vẽ bên dưới, con của nền sẽ bị nút che).
        /// </summary>
        private static UISpecNode SmallestContainer(UISpecNode node, IEnumerable<UISpecNode> candidates)
        {
            UISpecNode best = null;
            foreach (var other in candidates)
            {
                if (other == node || other.kind == UISpecNode.KindInstance || other.kind == UISpecNode.KindScroll
                    || other.kind == UISpecNode.KindText || Area(other) <= Area(node) || !Contains(other, node)) continue;
                if (best == null || (best.flat && !other.flat) || (best.flat == other.flat && Area(other) < Area(best))) best = other;
            }

            return best;
        }

        /// <summary>
        /// Căn lề chữ — quan trọng khi nội dung đổi dài/ngắn so với demo: các dòng cùng cha thẳng mép trái (danh sách
        /// cạnh icon) → trái, thẳng mép phải → phải; hai lề cân nhau → giữa; còn lại theo phía gần hơn.
        /// </summary>
        internal static void AssignTextAlignment(List<UISpecNode> nodes, int rootWidth)
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

        /// <summary>Cha đứng trước con; cùng cha thì sprite phẳng (nền, panel) rồi node lớn vẽ trước (nằm dưới).</summary>
        private static List<UISpecNode> OrderForDrawing(List<UISpecNode> nodes)
        {
            var result = new List<UISpecNode>(nodes.Count);
            AppendChildren(string.Empty, nodes, result);
            return result;
        }

        private static void AppendChildren(string parentId, List<UISpecNode> nodes, List<UISpecNode> result)
        {
            foreach (var child in nodes.Where(n => n.parent == parentId).OrderByDescending(n => n.flat).ThenByDescending(Area).ThenBy(n => n.y))
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

        internal static string UniqueId(string baseId, HashSet<string> used)
        {
            var id = baseId;
            for (var i = 2; !used.Add(id); i++) id = $"{baseId}_{i}";
            return id;
        }

        private static long Area(UISpecNode n) => (long)n.w * n.h;

        internal static bool Contains(UISpecNode outer, UISpecNode inner)
        {
            return inner.x >= outer.x - ContainTolerance
                   && inner.y >= outer.y - ContainTolerance
                   && inner.x + inner.w <= outer.x + outer.w + ContainTolerance
                   && inner.y + inner.h <= outer.y + outer.h + ContainTolerance;
        }
    }
}
