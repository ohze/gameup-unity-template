using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        private const float PanelArea = 0.3f; // panel ≥ 30% màn → con của nó chia box theo dải dọc (PSD)
        private const string SharedShapePrefix = "shape_round_";
        private const int IconSize = 96; // PNG xuất từ layer vô danh ≤ 96 px → imgIcon, lớn hơn → imgPart
        private static readonly Regex GenericLayerName =
            new Regex(@"(^(group|layer|rounded rectangle|rectangle|ellipse|shape|mode)\b)|\bcopy\b|^\d", RegexOptions.IgnoreCase);
        private const float SkipInside = 0.6f;
        private const float FullScreen = 0.95f;

        /// <param name="fontPath">TMP_FontAsset cho các node text tìm được; rỗng = font mặc định TMP.</param>
        public static UISpec Generate(LocateResult locate, string name, string demoAssetPath, string outputPrefabPath, string fontPath)
        {
            return Generate(new[] { locate }, name, new[] { demoAssetPath }, outputPrefabPath, fontPath);
        }

        /// <param name="outlineMaterialPath">Material cho chữ có viền; rỗng = builder tự chọn preset outline của font.</param>
        /// <param name="skipRegions">Phần đã có sẵn (thanh điều hướng…): không dựng, có prefab thì đặt instance.</param>
        public static UISpec Generate(IReadOnlyList<LocateResult> states, string name, IReadOnlyList<string> demoAssetPaths,
            string outputPrefabPath, string fontPath, string outlineMaterialPath, IReadOnlyList<UISkipRegion> skipRegions = null)
        {
            var spec = GenerateCore(states, name, demoAssetPaths, outputPrefabPath, fontPath, skipRegions);
            if (string.IsNullOrEmpty(outlineMaterialPath)) return spec;
            foreach (var node in spec.nodes.Concat(spec.templates.SelectMany(t => t.nodes)))
                if (node.kind == UISpecNode.KindText && node.outlineWidth > 0f) node.material = outlineMaterialPath;
            return spec;
        }

        /// <summary>
        /// Nhiều demo = nhiều trạng thái của cùng một UI (vd 2 tab). Node giống hệt ở mọi demo dựng một lần; phần riêng của
        /// demo k nằm trong nhóm <c>grp{NhãnTab}</c> (chỉ nhóm đầu bật). Danh sách mỗi tab thành ScrollRect + item prefab
        /// riêng, đặt tên theo nhãn tab ("Leaderboard" → LeaderboardItem).
        /// </summary>
        public static UISpec Generate(IReadOnlyList<LocateResult> states, string name, IReadOnlyList<string> demoAssetPaths,
            string outputPrefabPath, string fontPath)
        {
            return GenerateCore(states, name, demoAssetPaths, outputPrefabPath, fontPath, null);
        }

        private static UISpec GenerateCore(IReadOnlyList<LocateResult> states, string name, IReadOnlyList<string> demoAssetPaths,
            string outputPrefabPath, string fontPath, IReadOnlyList<UISkipRegion> skipRegions)
        {
            var skips = skipRegions?.Where(r => r.w > 0 && r.h > 0).ToList() ?? new List<UISkipRegion>();
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
                spec.notes.AddRange(states[k].notes);
                RemoveSkipped(nodes, skips, k == 0 ? spec.notes : null);
                perState.Add(nodes);
            }

            var labels = TabLabels(perState[0]);
            var folder = Path.GetDirectoryName(outputPrefabPath)?.Replace('\\', '/');
            var usedIds = new HashSet<string>(perState.SelectMany(n => n).Select(n => n.id));
            for (var k = 0; k < states.Count; k++)
                UIListExtractor.ExtractLists(perState[k], TemplateNames(labels, k, states.Count, name), folder, spec.templates, usedIds,
                    spec.notes, states[k].IsPsd);
            foreach (var nodes in perState)
                UIListExtractor.ExtractSimilarRows(nodes, spec.templates, usedIds, spec.notes);

            var merged = states.Count == 1 ? perState[0] : MergeStates(perState, labels, spec.notes, spec.stateGroups);
            if (states.Count == 1) AssignParents(merged, merged, string.Empty);
            UnparentFromBackground(merged, spec.referenceWidth, spec.referenceHeight);
            if (states[0].IsPsd) GroupIntoBoxes(merged, spec.referenceWidth, spec.referenceHeight, usedIds, spec.notes);
            AddDim(merged, states[0], spec.notes); // sau khi gán cha — lớp phủ kín màn không được làm cha của mọi thứ
            AssignTextAlignment(merged, states[0].demoWidth);
            spec.nodes = OrderForDrawing(merged);
            AddSkipInstances(spec, skips, usedIds);
            AddUnmatchedNotes(states[0], spec.notes);
            spec.notes = spec.notes.Distinct().ToList();
            return spec;
        }

        /// <summary>
        /// Ảnh nền cả màn (≥ 95% khung tham chiếu) chứa mọi thứ nên thành cha của cả UI — đổi nền là kéo theo toàn bộ cây.
        /// Đưa con của nó lên cùng cấp; nền đặt anchor stretch → vẽ dưới cùng (kể cả dưới panel phẳng).
        /// </summary>
        private static void UnparentFromBackground(List<UISpecNode> nodes, int width, int height)
        {
            var backgrounds = nodes.Where(n => n.kind == UISpecNode.KindImage && n.w >= width * FullScreen && n.h >= height * FullScreen)
                .ToDictionary(n => n.id, n => n.parent);
            foreach (var node in nodes.Where(n => backgrounds.ContainsKey(n.id)))
                node.anchor = "stretch"; // phủ kín màn ở mọi tỉ lệ màn hình; vẽ trước mọi node cùng cấp
            foreach (var node in nodes)
                while (node.parent != null && backgrounds.TryGetValue(node.parent, out var grand))
                    node.parent = grand;
        }

        /// <summary>Bỏ node nằm phần lớn (≥ 60% diện tích) trong vùng đã có sẵn; ghi chú số node bỏ theo từng vùng.</summary>
        private static void RemoveSkipped(List<UISpecNode> nodes, List<UISkipRegion> skips, List<string> notes)
        {
            foreach (var region in skips)
            {
                var removed = nodes.RemoveAll(n => InsideRatio(n, region) >= SkipInside);
                if (notes != null && removed > 0)
                    notes.Add($"Vùng bỏ qua '{region.name}': bỏ {removed} node"
                              + (string.IsNullOrEmpty(region.prefab) ? " (không đặt gì)." : $", thay bằng prefab {region.prefab}."));
            }
        }

        /// <summary>Vùng có prefab → node instance đúng khung, vẽ trên cùng (thanh điều hướng nằm trên nội dung).</summary>
        private static void AddSkipInstances(UISpec spec, List<UISkipRegion> skips, HashSet<string> usedIds)
        {
            foreach (var region in skips.Where(r => !string.IsNullOrEmpty(r.prefab)))
            {
                var id = UniqueId(string.IsNullOrEmpty(region.name) ? Path.GetFileNameWithoutExtension(region.prefab) : region.name, usedIds);
                spec.nodes.Add(new UISpecNode
                {
                    id = id, kind = UISpecNode.KindInstance, parent = string.Empty, prefab = region.prefab,
                    x = region.x, y = region.y, w = region.w, h = region.h
                });
            }

            spec.skipRegions = skips;
        }

        private static float InsideRatio(UISpecNode node, UISkipRegion region)
        {
            var ix = Mathf.Max(0, Mathf.Min(node.x + node.w, region.x + region.w) - Mathf.Max(node.x, region.x));
            var iy = Mathf.Max(0, Mathf.Min(node.y + node.h, region.y + region.h) - Mathf.Max(node.y, region.y));
            return node.w * node.h > 0 ? ix * iy / (float)(node.w * node.h) : 0f;
        }

        /// <summary>Gameplay phía sau bị tối đều (đo từ icon HUD bị tint xám) → lớp dim đen phủ màn, vẽ dưới cùng.</summary>
        private static void AddDim(List<UISpecNode> nodes, LocateResult locate, List<string> notes)
        {
            if (locate.dimAlpha <= 0f) return;
            var alpha = Mathf.Clamp(Mathf.RoundToInt(locate.dimAlpha * 255f), 0, 255);
            nodes.Insert(0, new UISpecNode
            {
                id = UniqueId("imgDim", new HashSet<string>(nodes.Select(n => n.id))), kind = UISpecNode.KindImage,
                parent = string.Empty, // gốc — OrderForDrawing chỉ duyệt từ cha rỗng, null thì node bị rơi mất
                w = locate.demoWidth, h = locate.demoHeight, anchor = "stretch", color = $"#000000{alpha:X2}",
                raycastTarget = true, flat = true
            });
            notes.Add(locate.dimEstimated
                ? $"imgDim: lớp dim đen {locate.dimAlpha:P0} ước lượng từ điểm sáng nhất của phần bị phủ (cận trên) — so với demo và chỉnh alpha nếu cần."
                : $"imgDim: lớp dim đen {locate.dimAlpha:P0} đo từ gameplay bị làm tối phía sau.");
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
            var owns = perState.Select((nodes, k) => nodes.Where(n => !shared.Any(s => Same(s, n, perState[0], nodes))).ToList()).ToList();
            ShareSwappedSprites(shared, owns, notes);
            MoveCoveredTexts(shared, owns);
            var result = new List<UISpecNode>(shared);
            AssignParents(shared, shared, string.Empty);
            var used = new HashSet<string>(shared.Select(n => n.id));

            for (var k = 0; k < perState.Count; k++)
            {
                var own = owns[k];
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

        /// <summary>
        /// Nút cùng chỗ ở mọi tab nhưng khác sprite (nút tab chọn / chưa chọn đổi art cho nhau) → một node dùng chung
        /// (sprite của tab 1), ghi chú để script đổi sprite khi chọn tab — không nhân đôi nút vào từng nhóm tab. Chỉ nút:
        /// ảnh khác nhau giữa các tab (vương miện top 1 / top 2) là nội dung riêng của tab.
        /// </summary>
        internal static void ShareSwappedSprites(List<UISpecNode> shared, List<List<UISpecNode>> owns, List<string> notes)
        {
            foreach (var node in owns[0].Where(n => n.kind == UISpecNode.KindButton && !string.IsNullOrEmpty(n.sprite)).ToList())
            {
                var twins = owns.Skip(1).Select(own => own.FirstOrDefault(o => o.kind == node.kind && SameRect(o, node))).ToList();
                if (twins.Any(t => t == null)) continue;
                owns[0].Remove(node);
                for (var k = 0; k < twins.Count; k++) owns[k + 1].Remove(twins[k]);
                shared.Add(node);
                var sprites = new[] { node.sprite }.Concat(twins.Select(t => t.sprite)).Select(Path.GetFileNameWithoutExtension);
                notes.Add($"{node.id}: cùng chỗ ở mọi tab, đổi sprite theo tab ({string.Join(" / ", sprites)}) → một node, script đổi sprite khi chọn tab.");
            }
        }

        private static bool SameRect(UISpecNode a, UISpecNode b)
        {
            return Mathf.Abs(a.x - b.x) <= SameTolerance && Mathf.Abs(a.y - b.y) <= SameTolerance
                   && Mathf.Abs(a.w - b.w) <= SameTolerance && Mathf.Abs(a.h - b.h) <= SameTolerance;
        }

        /// <summary>
        /// Chữ giống nhau ở mọi tab nhưng ở tab nào cũng nằm trên một phần tử riêng của tab đó (nhãn "Leaderboard" trên nút
        /// tab đổi chỗ giữa 2 tab) → nhóm tab vẽ sau sẽ che chữ nếu để chung. Nhân chữ vào từng tab để làm con của nút.
        /// </summary>
        internal static void MoveCoveredTexts(List<UISpecNode> shared, List<List<UISpecNode>> owns)
        {
            var covered = shared.Where(t => t.kind == UISpecNode.KindText
                                            && owns.All(own => own.Any(o => o.kind != UISpecNode.KindText && Contains(o, t)))).ToList();
            foreach (var text in covered)
            {
                shared.Remove(text);
                foreach (var own in owns)
                {
                    var copy = JsonUtility.FromJson<UISpecNode>(JsonUtility.ToJson(text));
                    copy.parent = null;
                    copy.alignFixed = text.alignFixed;
                    own.Add(copy);
                }
            }
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
            if (a.kind != b.kind || a.sprite != b.sprite || a.text != b.text || a.prefab != b.prefab || OverridesKey(a) != OverridesKey(b)
                || (a.kind != UISpecNode.KindText && a.color != b.color))
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
            return string.Join("|", node.overrides.Select(o => $"{o.id}:{o.hide}:{o.sprite}:{o.setText}:{o.text}:{o.color}"));
        }

        private static List<UISpecNode> CreateNodes(LocateResult locate, List<string> notes)
        {
            var nodes = new List<UISpecNode>();
            var usedIds = new HashSet<string>();
            var exported = new HashSet<string>(locate.exported.Select(Path.GetFullPath));
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
                    var baseName = NodeName(sprite, match, exported.Contains(Path.GetFullPath(sprite.sprite)));
                    var id = UniqueId(sprite.matches.Count > 1 || baseName != sprite.name ? $"{baseName}_{i + 1}" : baseName, usedIds);
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
                        raycastTarget = isButton,
                        group = match.group
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
                    id = UniqueId(hasText ? TextId(StripRichText(t.text)) : $"txt_{nodes.Count + 1}", usedIds),
                    kind = UISpecNode.KindText,
                    x = t.x,
                    y = t.y,
                    w = t.w,
                    h = t.h,
                    text = hasText ? t.text : PlaceholderText,
                    color = t.color,
                    font = fontPath,
                    outlineWidth = t.outlineWidth,
                    outlineColor = t.outlineColor,
                    align = string.IsNullOrEmpty(t.align) ? "center" : t.align,
                    alignFixed = !string.IsNullOrEmpty(t.align),
                    group = t.group
                };
                nodes.Add(node);
                if (!hasText || t.confidence < LowOcrConfidence) unsure.Add($"{node.id} (\"{node.text}\")");
            }

            if (locate.IsPsd)
            {
                notes.Add($"{nodes.Count} dòng chữ lấy từ text layer của PSD: nội dung, màu (nhiều màu → rich text), căn lề, viền; cỡ chữ tự khớp theo khung.");
                return nodes;
            }

            notes.Add(locate.ocr == "ok"
                ? $"{nodes.Count} dòng chữ: vị trí, màu, cỡ theo demo, nội dung đọc bằng OCR — soát lại ký hiệu đặc biệt (₫, ×, icon trong chữ)."
                : $"{nodes.Count} dòng chữ: vị trí, màu, cỡ theo demo; nội dung đang là \"{PlaceholderText}\" vì venv chưa có OCR "
                  + "(Bước 1 → Cập nhật) — hoặc Copy prompt cho Claude để điền.");
            if (unsure.Count > 0) notes.Add($"OCR không chắc, cần kiểm tra: {string.Join(", ", unsure)}.");
            return nodes;
        }

        /// <summary>
        /// Tên node của một sprite: art thật → tên art; PNG xuất từ PSD → tên layer nếu có nghĩa ("boder_slot"), không thì
        /// theo vai trò: shape một màu <c>imgFill</c>, ô nhỏ <c>imgIcon</c>, còn lại <c>imgPart</c> (tên kiểu
        /// "border_popup copy 13", "Group 31" là rác của designer).
        /// </summary>
        private static string NodeName(LocateSprite sprite, LocateMatch match, bool exported)
        {
            if (!exported) return sprite.name;
            var layer = match.layer?.Trim();
            if (!string.IsNullOrEmpty(layer) && !GenericLayerName.IsMatch(layer))
                return sprite.name.StartsWith(SharedShapePrefix) ? Regex.Replace(layer, @"[^\w]+", "_").Trim('_') : sprite.name;
            if (sprite.name.StartsWith(SharedShapePrefix)) return "imgFill";
            return match.w <= IconSize && match.h <= IconSize ? "imgIcon" : "imgPart";
        }

        /// <summary>
        /// PSD: con của một panel lớn (≥ 30% màn) chia thành box theo dải dọc không chồng nhau — header, bục top 3, danh sách,
        /// hàng của mình… Mỗi dải ≥ 2 phần tử thành một node rỗng (anchor tự tính theo vị trí), tên theo nhóm layer PSD chung.
        /// </summary>
        internal static void GroupIntoBoxes(List<UISpecNode> nodes, int width, int height, HashSet<string> used, List<string> notes)
        {
            var panels = nodes.Where(n => (n.kind == UISpecNode.KindImage || n.kind == UISpecNode.KindButton) && n.anchor != "stretch"
                                          && (long)n.w * n.h >= width * height * PanelArea).ToList();
            foreach (var panel in panels)
            {
                var bands = new List<List<UISpecNode>>();
                var bottom = int.MinValue;
                foreach (var child in nodes.Where(n => n.parent == panel.id).OrderBy(n => n.y))
                {
                    if (bands.Count == 0 || child.y >= bottom) bands.Add(new List<UISpecNode>());
                    bands[bands.Count - 1].Add(child);
                    bottom = Mathf.Max(bottom, child.y + child.h);
                }

                if (bands.Count < 2) continue;
                // dải chỉ gồm các nhóm tab chồng nhau (grpLeaderboard + grpRankingRewards) không cần box bọc thêm
                foreach (var band in bands.Where(b => b.Count >= 2 && b.Any(n => n.kind != UISpecNode.KindEmpty)))
                {
                    var x0 = band.Min(n => n.x);
                    var y0 = band.Min(n => n.y);
                    var box = new UISpecNode
                    {
                        id = UniqueId(BoxName(band), used), kind = UISpecNode.KindEmpty, parent = panel.id,
                        x = x0, y = y0, w = band.Max(n => n.x + n.w) - x0, h = band.Max(n => n.y + n.h) - y0
                    };
                    foreach (var member in band) member.parent = box.id;
                    nodes.Add(box);
                    notes.Add($"{box.id}: box gom {band.Count} phần tử cùng dải trong {panel.id} ({string.Join(", ", band.Select(n => n.id))}).");
                }
            }
        }

        /// <summary>Tên box: nhóm layer PSD sâu nhất chung cho cả dải mà có nghĩa ("top1-3" → boxTop13), không thì theo phần tử lớn nhất.</summary>
        private static string BoxName(List<UISpecNode> band)
        {
            var paths = band.Where(n => !string.IsNullOrEmpty(n.group)).Select(n => n.group.Split('/')).ToList();
            if (paths.Count == band.Count(n => n.kind != UISpecNode.KindEmpty) && paths.Count > 0)
            {
                var common = paths[0].TakeWhile((segment, i) => paths.All(p => p.Length > i && p[i] == segment)).ToList();
                for (var i = common.Count - 1; i >= 0; i--)
                    if (!GenericLayerName.IsMatch(common[i])) return $"box{Pascal(common[i])}";
            }

            var largest = band.OrderByDescending(n => (long)n.w * n.h).First();
            return $"box{Pascal(Regex.Replace(largest.id, @"_\d+$", string.Empty))}";
        }

        private static string Pascal(string text)
        {
            return string.Concat(Regex.Split(text, @"[^A-Za-z0-9]+").Where(w => w.Length > 0)
                .Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));
        }

        /// <summary>Bỏ thẻ rich text TMP (<c>&lt;color=#..&gt;</c>…) — để đặt id theo chữ hiện ra.</summary>
        internal static string StripRichText(string text)
        {
            return Regex.Replace(text, "<[^<>]+>", string.Empty);
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
            foreach (var node in texts.Where(n => !n.alignFixed))
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
            // item trong ScrollRect: thứ tự con = thứ tự hàng trên màn (LayoutGroup xếp theo thứ tự sibling)
            if (nodes.Any(n => n.id == parentId && n.kind == UISpecNode.KindScroll))
            {
                foreach (var item in nodes.Where(n => n.parent == parentId).OrderBy(n => n.y).ThenBy(n => n.x))
                {
                    result.Add(item);
                    AppendChildren(item.id, nodes, result);
                }

                return;
            }

            // Nền/lớp phủ kín màn (stretch) vẽ dưới cùng, rồi sprite phẳng, rồi lớn trước nhỏ.
            foreach (var child in nodes.Where(n => n.parent == parentId).OrderByDescending(n => n.anchor == "stretch")
                         .ThenByDescending(n => n.flat).ThenByDescending(Area).ThenBy(n => n.y))
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
            if (locate.IsPsd) return; // ghi chú của bước đọc PSD đã chép từ locate.notes
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
