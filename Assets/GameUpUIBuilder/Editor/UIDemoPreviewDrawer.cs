using UnityEditor;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Vẽ ảnh demo trong cửa sổ đúng tỉ lệ gốc (fit theo khung, căn giữa) kèm khung các sprite đã định vị.
    /// Rê chuột vào khung để xem tên sprite; sprite đang chọn được tô nổi.
    /// </summary>
    public static class UIDemoPreviewDrawer
    {
        private static readonly Color Backdrop = new Color(0.11f, 0.11f, 0.11f);
        private static readonly Color FrameColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color MatchColor = new Color(0.30f, 0.90f, 0.45f, 0.9f);
        private static readonly Color SlicedColor = new Color(1f, 0.65f, 0.20f, 0.9f);
        private static readonly Color HighlightColor = new Color(1f, 0.92f, 0.20f, 1f);

        private static GUIStyle _tag;

        /// <returns>Tên sprite đang được rê chuột, null nếu không có.</returns>
        public static string Draw(Texture2D texture, float height, LocateResult locate, bool showMatches, string highlight)
        {
            var area = GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(area, Backdrop);
            if (texture == null) return null;

            var image = FitRect(area, texture.width, texture.height);
            GUI.DrawTexture(image, texture, ScaleMode.StretchToFill, true);
            DrawOutline(image, FrameColor, 1f);

            if (locate == null || !showMatches || locate.demoWidth <= 0) return null;
            return DrawMatches(image, locate, highlight);
        }

        /// <summary>Rect lớn nhất có tỉ lệ w:h nằm trọn trong <paramref name="area"/>, căn giữa, làm tròn pixel để ảnh nét.</summary>
        private static Rect FitRect(Rect area, int width, int height)
        {
            var scale = Mathf.Min(area.width / width, area.height / height);
            var w = Mathf.Round(width * scale);
            var h = Mathf.Round(height * scale);
            return new Rect(Mathf.Round(area.x + (area.width - w) * 0.5f), Mathf.Round(area.y + (area.height - h) * 0.5f), w, h);
        }

        private static string DrawMatches(Rect image, LocateResult locate, string highlight)
        {
            var scale = image.width / locate.demoWidth;
            var mouse = Event.current.mousePosition;
            string hovered = null;
            var hoveredRect = default(Rect);

            foreach (var sprite in locate.sprites)
            {
                if (!sprite.IsMatched) continue;
                var isHighlight = sprite.name == highlight;
                foreach (var match in sprite.matches)
                {
                    var rect = new Rect(image.x + match.x * scale, image.y + match.y * scale, match.w * scale, match.h * scale);
                    var color = isHighlight ? HighlightColor : match.sliced ? SlicedColor : MatchColor;
                    if (isHighlight) EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.18f));
                    DrawOutline(rect, color, isHighlight ? 2f : 1f);

                    // Ưu tiên khung nhỏ nhất dưới con trỏ (icon nằm trong nút).
                    if (rect.Contains(mouse) && (hovered == null || rect.width * rect.height < hoveredRect.width * hoveredRect.height))
                    {
                        hovered = sprite.name;
                        hoveredRect = rect;
                    }
                }
            }

            if (hovered != null) DrawTag(hoveredRect, hovered, image);
            return hovered;
        }

        private static void DrawTag(Rect target, string text, Rect bounds)
        {
            _tag ??= new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.black },
                padding = new RectOffset(4, 4, 1, 1)
            };
            var size = _tag.CalcSize(new GUIContent(text));
            var x = Mathf.Clamp(target.x, bounds.x, bounds.xMax - size.x);
            var y = target.y - size.y >= bounds.y ? target.y - size.y : target.yMax;
            var rect = new Rect(x, y, size.x, size.y);
            EditorGUI.DrawRect(rect, HighlightColor);
            GUI.Label(rect, text, _tag);
        }

        private static void DrawOutline(Rect r, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), color);
        }
    }
}
