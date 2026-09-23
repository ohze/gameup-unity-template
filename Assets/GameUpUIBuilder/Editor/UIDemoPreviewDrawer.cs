using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Vẽ ảnh demo trong cửa sổ đúng tỉ lệ gốc (fit theo khung, căn giữa) kèm khung những gì máy dò được: sprite đã khớp,
    /// dòng chữ, vị trí bị bộ lọc loại. Rê chuột vào khung để xem máy khớp nó thế nào (tỉ lệ, 9-slice, tint, độ tin cậy);
    /// sprite đang chọn và sprite vừa dò xong được tô nổi.
    /// </summary>
    public static class UIDemoPreviewDrawer
    {
        private static readonly Color Backdrop = new Color(0.11f, 0.11f, 0.11f);
        private static readonly Color FrameColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color MatchColor = new Color(0.30f, 0.90f, 0.45f, 0.9f);
        private static readonly Color SlicedColor = new Color(1f, 0.65f, 0.20f, 0.9f);
        private static readonly Color TextColor = new Color(0.35f, 0.80f, 1f, 0.9f);
        private static readonly Color DroppedColor = new Color(1f, 0.35f, 0.35f, 0.75f);
        private static readonly Color HighlightColor = new Color(1f, 0.92f, 0.20f, 1f);
        private static readonly Color TooltipBack = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        private static readonly Color OverlayShade = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color RegionColor = new Color(1f, 1f, 1f, 0.8f);
        private static readonly Color SkipFill = new Color(1f, 0.25f, 0.25f, 0.22f);
        private static readonly Color SkipColor = new Color(1f, 0.4f, 0.4f, 0.95f);
        private static readonly Color NodeColor = new Color(0.65f, 0.55f, 1f, 0.85f);
        private static readonly Color NodeSelected = new Color(1f, 0.92f, 0.20f, 1f);
        private static readonly Color NodeExcluded = new Color(0.55f, 0.55f, 0.55f, 0.8f);

        private static GUIStyle _tag;
        private static GUIStyle _tooltip;

        /// <summary>Khung đang dưới con trỏ: nhỏ nhất thắng (icon nằm trong nút).</summary>
        private struct Hover
        {
            public Rect Rect;
            public string Sprite;
            public string Info;
        }

        /// <param name="spec">Cây sắp dựng — vẽ khung từng node (lớp <see cref="PreviewLayers.Nodes"/>); null = không vẽ.</param>
        /// <param name="selectedNodes">id node đang chọn trong cây bên trái — tô nổi.</param>
        /// <param name="latest">Sprite vừa dò xong (khi đang chạy) — tô nổi như đang chọn.</param>
        /// <param name="caption">Dòng trạng thái vẽ trên đầu ảnh (khi đang chạy); null = không vẽ.</param>
        /// <returns>Sprite và node đang được rê chuột.</returns>
        public static UIDemoHover Draw(Rect area, Texture2D texture, LocateResult locate, PreviewLayers layers, string highlight,
            UISpec spec = null, ICollection<string> selectedNodes = null, string latest = null, string caption = null)
        {
            EditorGUI.DrawRect(area, Backdrop);
            if (texture == null) return default;

            var image = FitRect(area, texture.width, texture.height);
            GUI.DrawTexture(image, texture, ScaleMode.StretchToFill, true);
            DrawOutline(image, FrameColor, 1f);

            var hover = new Hover();
            if (locate != null && locate.demoWidth > 0)
            {
                var scale = image.width / locate.demoWidth;
                if ((layers & PreviewLayers.UIRegion) != 0 && locate.uiRegions.Count > 0)
                    DrawUIRegions(image, scale, locate.uiRegions, locate.demoHeight);
                if ((layers & PreviewLayers.Dropped) != 0) DrawDropped(image, scale, locate.dropped, ref hover);
                if ((layers & PreviewLayers.Sprites) != 0) DrawMatches(image, scale, locate.sprites, highlight, latest, ref hover);
                if ((layers & PreviewLayers.Texts) != 0) DrawTexts(image, scale, locate.texts, ref hover);
            }

            var node = new Hover();
            if (spec != null && spec.referenceWidth > 0 && (layers & PreviewLayers.Nodes) != 0)
                DrawNodes(image, image.width / spec.referenceWidth, spec, selectedNodes, ref node);

            if (!string.IsNullOrEmpty(caption)) DrawCaption(image, caption);
            // Node vẽ sau và thắng khi trùng khung: đang duyệt cây thì thông tin node là thứ cần xem.
            var tooltip = node.Info != null ? node : hover;
            if (tooltip.Info != null) DrawTooltip(tooltip, image);
            return new UIDemoHover { Sprite = hover.Sprite, Node = node.Sprite };
        }

        /// <summary>Khung từng node của spec: tím = sẽ dựng, vàng = đang chọn trong cây, xám đứt = đã bỏ.</summary>
        private static void DrawNodes(Rect image, float scale, UISpec spec, ICollection<string> selected, ref Hover hover)
        {
            foreach (var node in spec.nodes) DrawNode(image, scale, node, selected, false, ref hover);
            foreach (var node in spec.excluded) DrawNode(image, scale, node, selected, true, ref hover);
        }

        private static void DrawNode(Rect image, float scale, UISpecNode node, ICollection<string> selected, bool excluded,
            ref Hover hover)
        {
            if (node.w <= 0 || node.h <= 0) return;
            var rect = ToScreen(image, scale, node.x, node.y, node.w, node.h);
            var isSelected = selected != null && selected.Contains(node.id);

            if (excluded && !isSelected) DrawDashedOutline(rect, NodeExcluded);
            else
            {
                var color = isSelected ? NodeSelected : NodeColor;
                if (isSelected) EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.16f));
                DrawOutline(rect, color, isSelected ? 2f : 1f);
            }

            if (IsHovered(rect, hover))
                hover = new Hover { Rect = rect, Sprite = node.id, Info = DescribeNode(node, excluded) };
        }

        private static string DescribeNode(UISpecNode node, bool excluded)
        {
            var parent = string.IsNullOrEmpty(node.parent) ? "gốc" : node.parent;
            var content = node.kind == UISpecNode.KindText && !string.IsNullOrEmpty(node.text) ? $"\n“{node.text}”" : string.Empty;
            return $"{(excluded ? "✕ " : string.Empty)}{node.id} · {node.kind}{(excluded ? " — đã bỏ, không dựng" : string.Empty)}"
                   + $"\ncha: {parent} · {node.w}×{node.h} @({node.x},{node.y}) · anchor {node.anchor}{content}";
        }

        /// <summary>Rect lớn nhất có tỉ lệ w:h nằm trọn trong <paramref name="area"/>, căn giữa, làm tròn pixel để ảnh nét.</summary>
        public static Rect FitRect(Rect area, int width, int height)
        {
            var scale = Mathf.Min(area.width / width, area.height / height);
            var w = Mathf.Round(width * scale);
            var h = Mathf.Round(height * scale);
            return new Rect(Mathf.Round(area.x + (area.width - w) * 0.5f), Mathf.Round(area.y + (area.height - h) * 0.5f), w, h);
        }

        private static Rect ToScreen(Rect image, float scale, int x, int y, int w, int h) =>
            new Rect(image.x + x * scale, image.y + y * scale, w * scale, h * scale);

        private static void DrawMatches(Rect image, float scale, List<LocateSprite> sprites, string highlight, string latest,
            ref Hover hover)
        {
            foreach (var sprite in sprites)
            {
                if (!sprite.IsMatched) continue;
                var isHighlight = sprite.name == highlight || sprite.name == latest;
                foreach (var match in sprite.matches)
                {
                    var rect = ToScreen(image, scale, match.x, match.y, match.w, match.h);
                    var color = isHighlight ? HighlightColor : match.sliced ? SlicedColor : MatchColor;
                    if (isHighlight) EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.18f));
                    DrawOutline(rect, color, isHighlight ? 2f : 1f);
                    if (IsHovered(rect, hover))
                        hover = new Hover { Rect = rect, Sprite = sprite.name, Info = DescribeMatch(sprite, match) };
                }
            }
        }

        private static void DrawTexts(Rect image, float scale, List<LocateText> texts, ref Hover hover)
        {
            foreach (var text in texts)
            {
                var rect = ToScreen(image, scale, text.x, text.y, text.w, text.h);
                DrawOutline(rect, TextColor, 1f);
                if (IsHovered(rect, hover)) hover = new Hover { Rect = rect, Info = DescribeText(text) };
            }
        }

        private static void DrawDropped(Rect image, float scale, List<LocateDrop> dropped, ref Hover hover)
        {
            foreach (var drop in dropped)
            {
                var rect = ToScreen(image, scale, drop.x, drop.y, drop.w, drop.h);
                DrawDashedOutline(rect, DroppedColor);
                if (IsHovered(rect, hover))
                    hover = new Hover { Rect = rect, Sprite = drop.name, Info = $"✕ {drop.name} — bị loại\n{drop.ReasonLabel}\n{drop.w}×{drop.h} @({drop.x},{drop.y})" };
            }
        }

        /// <summary>
        /// Làm tối phần nằm dưới lớp phủ (ngoài mọi vùng UI) và viền đứt quanh từng vùng UI. Phần bù tính theo dải ngang:
        /// ranh giới dải là cạnh trên/dưới của các vùng, trong mỗi dải tô các đoạn không thuộc vùng nào.
        /// </summary>
        private static void DrawUIRegions(Rect image, float scale, List<LocateRect> regions, int demoHeight)
        {
            var edges = new SortedSet<int> { 0, demoHeight };
            foreach (var r in regions)
            {
                edges.Add(r.y);
                edges.Add(r.y + r.h);
            }

            var bands = new List<int>(edges);
            for (var i = 0; i < bands.Count - 1; i++)
            {
                int y0 = bands[i], y1 = bands[i + 1];
                var spans = regions.Where(r => r.y <= y0 && r.y + r.h >= y1).Select(r => (r.x, r.x + r.w)).OrderBy(s => s.Item1);
                var x = image.x;
                foreach (var (x0, x1) in spans)
                {
                    var left = image.x + x0 * scale;
                    if (left > x) EditorGUI.DrawRect(new Rect(x, image.y + y0 * scale, left - x, (y1 - y0) * scale), OverlayShade);
                    x = Mathf.Max(x, image.x + x1 * scale);
                }

                if (x < image.xMax) EditorGUI.DrawRect(new Rect(x, image.y + y0 * scale, image.xMax - x, (y1 - y0) * scale), OverlayShade);
            }

            foreach (var r in regions) DrawDashedOutline(ToScreen(image, scale, r.x, r.y, r.w, r.h), RegionColor);
        }

        /// <summary>Vùng đã có sẵn (không dựng): tô đỏ nhạt, viền đứt, nhãn tên; <paramref name="dragging"/> = khung đang kéo.</summary>
        public static void DrawSkipRegions(Rect image, float scale, IReadOnlyList<UISkipRegion> regions, Rect? dragging)
        {
            foreach (var r in regions)
            {
                var rect = ToScreen(image, scale, r.x, r.y, r.w, r.h);
                EditorGUI.DrawRect(rect, SkipFill);
                DrawDashedOutline(rect, SkipColor);
                var label = string.IsNullOrEmpty(r.prefab) ? $"Bỏ qua: {r.name}" : $"Có sẵn: {r.name} (prefab)";
                DrawCaption(new Rect(rect.x, rect.y, rect.width, 0f), label);
            }

            if (dragging.HasValue)
            {
                EditorGUI.DrawRect(dragging.Value, SkipFill);
                DrawOutline(dragging.Value, SkipColor, 2f);
            }
        }

        private static bool IsHovered(Rect rect, Hover current) =>
            rect.Contains(Event.current.mousePosition)
            && (current.Info == null || rect.width * rect.height < current.Rect.width * current.Rect.height);

        private static string DescribeMatch(LocateSprite sprite, LocateMatch match)
        {
            var kind = sprite.lowTexture ? "\nSprite một màu — chốt vị trí theo hình dáng viền" : string.Empty;
            var repeat = sprite.matches.Count > 1 ? $" · 1/{sprite.matches.Count} chỗ" : string.Empty;
            return $"{sprite.name}{repeat}\n{match.MethodLabel} · {match.w}×{match.h} @({match.x},{match.y})"
                   + $" · gốc {sprite.spriteWidth}×{sprite.spriteHeight}\n{match.ScoreLabel}{kind}";
        }

        private static string DescribeText(LocateText text)
        {
            var content = string.IsNullOrEmpty(text.text) ? "(chưa đọc nội dung — thiếu OCR)" : $"“{text.text}” · OCR tin cậy {text.confidence:P0}";
            var outline = string.IsNullOrEmpty(text.outlineColor) ? string.Empty : $" · viền {text.outlineColor} {text.outlineWidth:0.#}px";
            return $"Chữ {content}\nmàu {text.color}{outline} · {text.w}×{text.h} @({text.x},{text.y})";
        }

        private static void DrawTooltip(Hover hover, Rect bounds)
        {
            _tooltip ??= new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.white },
                padding = new RectOffset(6, 6, 3, 3),
                wordWrap = false,
                richText = false
            };
            DrawOutline(hover.Rect, HighlightColor, 2f);
            var content = new GUIContent(hover.Info);
            var size = _tooltip.CalcSize(content);
            var mouse = Event.current.mousePosition;
            var x = Mathf.Clamp(mouse.x + 14f, bounds.x, Mathf.Max(bounds.x, bounds.xMax - size.x));
            var y = mouse.y + 18f + size.y <= bounds.yMax ? mouse.y + 18f : mouse.y - 8f - size.y;
            var rect = new Rect(x, Mathf.Max(bounds.y, y), size.x, size.y);
            EditorGUI.DrawRect(rect, TooltipBack);
            DrawOutline(rect, HighlightColor, 1f);
            GUI.Label(rect, content, _tooltip);
        }

        private static void DrawCaption(Rect image, string caption)
        {
            _tag ??= new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.white },
                padding = new RectOffset(6, 6, 2, 2),
                clipping = TextClipping.Clip
            };
            var height = _tag.CalcSize(new GUIContent(caption)).y;
            var rect = new Rect(image.x, image.y, image.width, height);
            EditorGUI.DrawRect(rect, TooltipBack);
            GUI.Label(rect, caption, _tag);
        }

        private static void DrawOutline(Rect r, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), color);
        }

        private static void DrawDashedOutline(Rect r, Color color)
        {
            const float dash = 4f;
            for (var x = r.x; x < r.xMax; x += dash * 2f)
            {
                var w = Mathf.Min(dash, r.xMax - x);
                EditorGUI.DrawRect(new Rect(x, r.y, w, 1f), color);
                EditorGUI.DrawRect(new Rect(x, r.yMax - 1f, w, 1f), color);
            }

            for (var y = r.y; y < r.yMax; y += dash * 2f)
            {
                var h = Mathf.Min(dash, r.yMax - y);
                EditorGUI.DrawRect(new Rect(r.x, y, 1f, h), color);
                EditorGUI.DrawRect(new Rect(r.xMax - 1f, y, 1f, h), color);
            }
        }
    }
}
