#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>Trạng thái hiển thị của một bước / một mục trong các cửa sổ setup.</summary>
    public enum GUSetupState
    {
        Done,
        Missing,
        Busy,
        Blocked,
        Optional
    }

    /// <summary>
    /// Bộ widget dùng chung cho các cửa sổ cài đặt của GameUpCore: card, badge trạng thái,
    /// dòng trạng thái có nút hành động, progress theo bước. Giữ cùng ngôn ngữ thiết kế với
    /// cửa sổ Setup Dependencies của GameUpSDK.
    /// </summary>
    public static class GUInstallerUI
    {
        public static Color OkColor => EditorGUIUtility.isProSkin ? new Color(0.40f, 0.78f, 0.45f) : new Color(0.16f, 0.55f, 0.24f);
        public static Color MissingColor => EditorGUIUtility.isProSkin ? new Color(0.90f, 0.45f, 0.40f) : new Color(0.72f, 0.22f, 0.18f);
        public static Color BusyColor => EditorGUIUtility.isProSkin ? new Color(0.45f, 0.66f, 0.95f) : new Color(0.18f, 0.42f, 0.78f);
        public static Color BlockedColor => EditorGUIUtility.isProSkin ? new Color(0.85f, 0.70f, 0.35f) : new Color(0.65f, 0.48f, 0.10f);
        public static Color MutedColor => EditorGUIUtility.isProSkin ? new Color(0.65f, 0.65f, 0.65f) : new Color(0.42f, 0.42f, 0.42f);
        public static Color SeparatorColor => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.09f) : new Color(0f, 0f, 0f, 0.12f);

        public static Color Tint(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        public static Color ColorOf(GUSetupState state)
        {
            switch (state)
            {
                case GUSetupState.Done: return OkColor;
                case GUSetupState.Busy: return BusyColor;
                case GUSetupState.Blocked: return BlockedColor;
                case GUSetupState.Optional: return MutedColor;
                default: return MissingColor;
            }
        }

        public static string LabelOf(GUSetupState state)
        {
            switch (state)
            {
                case GUSetupState.Done: return "XONG";
                case GUSetupState.Busy: return "ĐANG CHẠY";
                case GUSetupState.Blocked: return "CHỜ BƯỚC TRƯỚC";
                case GUSetupState.Optional: return "TÙY CHỌN";
                default: return "CHƯA XONG";
            }
        }

        private static GUIStyle _card;
        private static GUIStyle _cardTitle;
        private static GUIStyle _stepTag;
        private static GUIStyle _desc;
        private static GUIStyle _muted;
        private static GUIStyle _badge;
        private static GUIStyle _pathLabel;

        public static GUIStyle Card { get { EnsureStyles(); return _card; } }
        public static GUIStyle CardTitle { get { EnsureStyles(); return _cardTitle; } }
        public static GUIStyle StepTag { get { EnsureStyles(); return _stepTag; } }
        public static GUIStyle Desc { get { EnsureStyles(); return _desc; } }
        public static GUIStyle Muted { get { EnsureStyles(); return _muted; } }
        public static GUIStyle PathLabel { get { EnsureStyles(); return _pathLabel; } }

        public static void EnsureStyles()
        {
            if (_card != null) return;

            _card = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 8, 8) };
            _cardTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            _stepTag = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            _desc = new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 11 };
            _muted = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            _badge = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _pathLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
        }

        // ─── Layout ──────────────────────────────────────────────────────────

        public readonly struct CardScope : IDisposable
        {
            public CardScope(float spaceBefore)
            {
                if (spaceBefore > 0f) EditorGUILayout.Space(spaceBefore);
                EditorGUILayout.BeginVertical(Card);
            }

            public void Dispose() => EditorGUILayout.EndVertical();
        }

        public static CardScope BeginCard(float spaceBefore = 6f) => new CardScope(spaceBefore);

        public static void Separator(float thickness = 1f)
        {
            var rect = EditorGUILayout.GetControlRect(false, thickness + 2f);
            rect.y += 1f;
            rect.height = thickness;
            EditorGUI.DrawRect(rect, SeparatorColor);
        }

        public static void SectionHeader(string title, string hint = null)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(title, StepTag);
            if (!string.IsNullOrEmpty(hint))
            {
                GUILayout.Space(6);
                GUILayout.Label(hint, Muted);
            }
            EditorGUILayout.EndHorizontal();
            Separator();
            EditorGUILayout.Space(2);
        }

        /// <summary>Tiêu đề card: "BƯỚC 1  Cài DOTween Pro          [XONG]".</summary>
        public static void CardHeader(string tag, string title, GUSetupState state)
        {
            EditorGUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(tag)) GUILayout.Label(tag, StepTag, GUILayout.Width(58f));
            GUILayout.Label(title, CardTitle);
            GUILayout.FlexibleSpace();
            DrawBadge(LabelOf(state), ColorOf(state));
            EditorGUILayout.EndHorizontal();
            Separator();
            EditorGUILayout.Space(3);
        }

        public static void DrawBadge(string text, Color color, float width = 108f)
        {
            EnsureStyles();
            var rect = GUILayoutUtility.GetRect(width, 17f, GUILayout.Width(width));
            EditorGUI.DrawRect(rect, Tint(color, 0.16f));

            var old = GUI.color;
            GUI.color = color;
            GUI.Label(rect, text, _badge);
            GUI.color = old;
        }

        /// <summary>
        /// Một dòng trạng thái: [badge] nhãn ........ chi tiết  [nút].
        /// Trả về true khi người dùng bấm nút hành động.
        /// </summary>
        public static bool StatusRow(
            string label,
            GUSetupState state,
            string detail = null,
            string actionLabel = null,
            bool actionEnabled = true,
            float actionWidth = 110f)
        {
            EnsureStyles();
            bool clicked = false;

            EditorGUILayout.BeginHorizontal();
            DrawBadge(LabelOf(state), ColorOf(state), 96f);
            GUILayout.Space(6);
            GUILayout.Label(label, Desc);

            if (!string.IsNullOrEmpty(detail))
            {
                GUILayout.FlexibleSpace();
                var old = GUI.color;
                GUI.color = MutedColor;
                GUILayout.Label(detail, PathLabel);
                GUI.color = old;
            }
            else
            {
                GUILayout.FlexibleSpace();
            }

            if (!string.IsNullOrEmpty(actionLabel))
            {
                using (new EditorGUI.DisabledScope(!actionEnabled))
                {
                    clicked = GUILayout.Button(actionLabel, EditorStyles.miniButton, GUILayout.Width(actionWidth));
                }
            }

            EditorGUILayout.EndHorizontal();
            return clicked;
        }

        /// <summary>Thanh tiến độ tổng: "Hoàn tất 3/5 bước".</summary>
        public static void ProgressBar(string prefix, int done, int total, float height = 20f)
        {
            total = Mathf.Max(1, total);
            var rect = GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(rect, Mathf.Clamp01(done / (float)total), $"{prefix} {done}/{total}");
        }

        public static bool PrimaryButton(string label, bool enabled = true, float height = 30f)
        {
            using (new EditorGUI.DisabledScope(!enabled))
            {
                return GUILayout.Button(label, GUILayout.Height(height));
            }
        }

        public static bool MiniButton(string label, bool enabled = true, float width = 0f)
        {
            using (new EditorGUI.DisabledScope(!enabled))
            {
                return width > 0f
                    ? GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(width))
                    : GUILayout.Button(label, EditorStyles.miniButton);
            }
        }

        public static void Hint(string text)
        {
            EnsureStyles();
            var old = GUI.color;
            GUI.color = MutedColor;
            GUILayout.Label(text, Muted);
            GUI.color = old;
        }

        /// <summary>Ping asset/folder theo path (bỏ qua nếu path không tồn tại).</summary>
        public static void PingPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (obj == null) return;
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }
    }
}
#endif
