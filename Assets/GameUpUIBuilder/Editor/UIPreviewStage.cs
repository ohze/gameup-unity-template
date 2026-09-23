using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Scene tạm chứa cây xem trước: mở thêm (additive) một scene rỗng có Canvas world-space đúng độ phân giải tham chiếu
    /// rồi dựng node của spec vào đó. Object là object Unity thật nên nhóm, đổi tên, xoá, kéo rect, xem Inspector đều như
    /// bình thường; scene này không bao giờ được lưu và không tạo asset nào.
    /// </summary>
    public static class UIPreviewStage
    {
        private const int UILayer = 5;

        /// <summary>1 px demo = 0.01 unit — cây 1080×2160 vừa tầm nhìn Scene View, không lẫn cỡ với scene đang mở.</summary>
        private const float PixelScale = 0.01f;

        /// <summary>
        /// Root của cây đang xem trước; null = chưa mở. Tìm lại qua instanceID lưu trong <see cref="UIPreviewMap"/> nên
        /// sống qua lần Unity recompile và qua việc đóng/mở lại cửa sổ.
        /// </summary>
        public static RectTransform Root
        {
            get
            {
                var id = UIPreviewMap.instance.rootId;
                if (id == 0) return null;
                var go = EditorUtility.InstanceIDToObject(id) as GameObject;
                return go != null ? go.transform as RectTransform : null;
            }
        }

        public static bool IsOpen => Root != null;

        public static string JobName => Root != null ? UIPreviewMap.instance.jobName : null;

        public static int State => Root != null ? UIPreviewMap.instance.state : 0;

        /// <summary>Dựng lại cây xem trước cho <paramref name="spec"/>, mở scene tạm nếu chưa có.</summary>
        public static UIBuildReport Open(UISpec spec, string jobName)
        {
            var map = UIPreviewMap.instance;
            var root = Root;
            if (root == null || root.name != spec.name)
            {
                if (root != null) Close();
                root = CreateStage(spec);
                map.state = 0;
            }

            map.Reset(root.gameObject.GetInstanceID(), jobName);
            var report = UIPreviewBuilder.Populate(root, spec);
            UIPreviewBuilder.ShowState(root, spec, map.state);
            Selection.activeGameObject = root.gameObject;
            Frame(root);
            return report;
        }

        public static void Close()
        {
            var root = Root;
            UIPreviewMap.instance.Reset(0, null);
            UIPreviewMap.instance.Persist();
            if (root == null) return;
            if (UIMockupOverlay.Enabled) UIMockupOverlay.Hide();
            EditorSceneManager.CloseScene(root.gameObject.scene, true);
        }

        /// <summary>Đọc cây hiện tại thành spec mới; null nếu chưa mở xem trước.</summary>
        public static UISpec Sync(UISpec source)
        {
            var root = Root;
            return root == null ? null : UIPreviewSync.Read(root, source);
        }

        public static void ShowState(UISpec spec, int state)
        {
            var root = Root;
            if (root == null) return;
            UIPreviewMap.instance.state = state;
            UIPreviewMap.instance.Persist();
            UIPreviewBuilder.ShowState(root, spec, state);
        }

        /// <summary>
        /// Gom các object đang chọn vào một nhóm mới cùng cha, khung bao vừa đủ — dùng thay "Create Empty Parent" của Unity
        /// để nhóm luôn có RectTransform đúng cỡ và giữ nguyên vị trí các object con.
        /// </summary>
        public static string GroupSelection(string groupName)
        {
            var root = Root;
            if (root == null) return "Chưa mở bản xem trước.";

            var selected = Selection.gameObjects
                .Select(go => go.transform as RectTransform)
                .Where(t => t != null && t != root && t.IsChildOf(root))
                .ToList();
            if (selected.Count == 0) return "Chọn ít nhất một object trong cây xem trước.";

            var parent = selected[0].parent as RectTransform;
            selected = selected.Where(t => t.parent == parent).OrderBy(t => t.GetSiblingIndex()).ToList();
            if (parent == null) return "Không nhóm được: object đang chọn không nằm trong cây.";

            var bounds = LocalBounds(selected, parent);
            var group = new GameObject(string.IsNullOrWhiteSpace(groupName) ? "grpNew" : groupName.Trim(), typeof(RectTransform))
                { layer = UILayer };
            Undo.RegisterCreatedObjectUndo(group, "Nhóm node UI Builder");
            var rt = (RectTransform)group.transform;
            rt.SetParent(parent, false);
            SetLocalRect(rt, parent, bounds);
            rt.SetSiblingIndex(selected[0].GetSiblingIndex());

            foreach (var child in selected)
            {
                Undo.SetTransformParent(child, rt, "Nhóm node UI Builder");
                AnchorToCenter(child);
            }

            Selection.activeGameObject = group;
            return null;
        }

        /// <summary>Chọn root và bật ảnh demo phủ lên Scene View để soi lệch vị trí.</summary>
        public static void FocusForOverlay()
        {
            var root = Root;
            if (root == null) return;
            Selection.activeGameObject = root.gameObject;
            Frame(root);
        }

        private static RectTransform CreateStage(UISpec spec)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            scene.name = $"UIBuilder Preview — {spec.name}";

            var canvasGo = new GameObject("PreviewCanvas", typeof(RectTransform)) { layer = UILayer };
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            var canvas = canvasGo.AddComponent<Canvas>();
            // World space: cây chỉ hiện trong Scene View, không đè lên Game View của scene đang làm.
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<GraphicRaycaster>();
            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(spec.referenceWidth, spec.referenceHeight);
            canvasRect.position = Vector3.zero;
            canvasRect.localScale = Vector3.one * PixelScale;

            var rootGo = new GameObject(spec.name, typeof(RectTransform)) { layer = UILayer };
            var root = (RectTransform)rootGo.transform;
            root.SetParent(canvasRect, false);
            UISpecBuilder.SetStretchFull(root);
            return root;
        }

        private static void Frame(RectTransform root)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            var corners = new Vector3[4];
            root.GetWorldCorners(corners);
            var bounds = new Bounds(corners[0], Vector3.zero);
            foreach (var corner in corners) bounds.Encapsulate(corner);
            view.in2DMode = true;
            view.Frame(bounds, false);
        }

        private static Rect LocalBounds(IEnumerable<RectTransform> items, RectTransform space)
        {
            var corners = new Vector3[4];
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var item in items)
            {
                item.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = space.InverseTransformPoint(corner);
                    minX = Mathf.Min(minX, local.x);
                    minY = Mathf.Min(minY, local.y);
                    maxX = Mathf.Max(maxX, local.x);
                    maxY = Mathf.Max(maxY, local.y);
                }
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        /// <summary>Đặt rect (toạ độ local của cha) bằng anchor giữa — nhóm mới không kéo giãn theo cha.</summary>
        private static void SetLocalRect(RectTransform rt, RectTransform parent, Rect local)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.sizeDelta = local.size;
            rt.anchoredPosition = local.center - parent.rect.center;
        }

        /// <summary>Đổi anchor về giữa cha hiện tại mà giữ nguyên vị trí/cỡ đang thấy.</summary>
        private static void AnchorToCenter(RectTransform rt)
        {
            var parent = rt.parent as RectTransform;
            if (parent == null) return;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var min = parent.InverseTransformPoint(corners[0]);
            var max = parent.InverseTransformPoint(corners[2]);
            SetLocalRect(rt, parent, Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }
    }
}
