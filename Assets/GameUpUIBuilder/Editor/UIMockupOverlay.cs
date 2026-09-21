using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Vẽ ảnh demo bán trong suốt đè lên Scene View, khớp đúng rect của root UI đang sửa (Prefab Mode, hoặc object đang
    /// chọn) để đối chiếu từng pixel. Chỉ vẽ trên màn hình Editor — không thêm object nào vào prefab/scene.
    /// </summary>
    public static class UIMockupOverlay
    {
        private static string _demoPath;
        private static float _opacity = 0.5f;

        public static bool Enabled { get; private set; }

        public static float Opacity
        {
            get => _opacity;
            set
            {
                _opacity = Mathf.Clamp01(value);
                SceneView.RepaintAll();
            }
        }

        public static void Show(string demoPath)
        {
            _demoPath = demoPath;
            if (!Enabled) SceneView.duringSceneGui += OnSceneGUI;
            Enabled = true;
            SceneView.RepaintAll();
        }

        public static void Hide()
        {
            if (Enabled) SceneView.duringSceneGui -= OnSceneGUI;
            Enabled = false;
            SceneView.RepaintAll();
        }

        /// <summary>Root UI để phủ: nội dung Prefab Mode, nếu không có thì RectTransform ngoài cùng của object đang chọn.</summary>
        public static RectTransform FindTarget()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot.transform is RectTransform stageRoot) return stageRoot;

            var selected = Selection.activeTransform as RectTransform;
            if (selected == null) return null;
            var top = selected;
            while (top.parent is RectTransform parent && parent.GetComponent<Canvas>() == null) top = parent;
            return top;
        }

        private static void OnSceneGUI(SceneView view)
        {
            var target = FindTarget();
            var texture = UIDemoTexture.Get(_demoPath);
            if (texture == null || target == null) return;

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var bottomLeft = HandleUtility.WorldToGUIPoint(corners[0]);
            var topRight = HandleUtility.WorldToGUIPoint(corners[2]);
            var rect = Rect.MinMaxRect(
                Mathf.Min(bottomLeft.x, topRight.x), Mathf.Min(bottomLeft.y, topRight.y),
                Mathf.Max(bottomLeft.x, topRight.x), Mathf.Max(bottomLeft.y, topRight.y));

            Handles.BeginGUI();
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _opacity);
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = old;
            Handles.EndGUI();
        }
    }
}
