using System.Collections.Generic;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Tên anchor trong spec ↔ cặp anchorMin/anchorMax của RectTransform.</summary>
    public static class UIAnchorNames
    {
        private const float Tolerance = 0.0005f;

        private static readonly Dictionary<string, (Vector2 min, Vector2 max)> Table = new Dictionary<string, (Vector2, Vector2)>
        {
            { "center", (new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)) },
            { "top", (new Vector2(0.5f, 1f), new Vector2(0.5f, 1f)) },
            { "bottom", (new Vector2(0.5f, 0f), new Vector2(0.5f, 0f)) },
            { "left", (new Vector2(0f, 0.5f), new Vector2(0f, 0.5f)) },
            { "right", (new Vector2(1f, 0.5f), new Vector2(1f, 0.5f)) },
            { "top-left", (new Vector2(0f, 1f), new Vector2(0f, 1f)) },
            { "top-right", (new Vector2(1f, 1f), new Vector2(1f, 1f)) },
            { "bottom-left", (Vector2.zero, Vector2.zero) },
            { "bottom-right", (new Vector2(1f, 0f), new Vector2(1f, 0f)) },
            { "stretch", (Vector2.zero, Vector2.one) },
            { "stretch-top", (new Vector2(0f, 1f), new Vector2(1f, 1f)) },
            { "stretch-middle", (new Vector2(0f, 0.5f), new Vector2(1f, 0.5f)) },
            { "stretch-bottom", (Vector2.zero, new Vector2(1f, 0f)) }
        };

        /// <summary>Tên anchor ứng với rect đang có; null nếu người dùng đặt anchor tự do (giữ nguyên anchor cũ trong spec).</summary>
        public static string Of(RectTransform rt)
        {
            foreach (var entry in Table)
                if (Close(entry.Value.min, rt.anchorMin) && Close(entry.Value.max, rt.anchorMax))
                    return entry.Key;
            return null;
        }

        private static bool Close(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < Tolerance && Mathf.Abs(a.y - b.y) < Tolerance;
        }
    }
}
