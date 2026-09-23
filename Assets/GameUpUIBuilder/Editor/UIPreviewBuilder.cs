using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Dựng cây xem trước từ spec vào một root trong scene: object thật, đủ Image/Text/ScrollRect như prefab cuối, nhưng
    /// không tạo asset nào. Node <c>instance</c> được bung thành nhóm chứa node của template (item prefab chưa dựng), nên
    /// người dùng thấy đúng nội dung một hàng danh sách mà vẫn chưa ghi gì ra <c>Assets/</c>.
    /// </summary>
    public static class UIPreviewBuilder
    {
        /// <summary>Xoá cây cũ dưới <paramref name="root"/> rồi dựng lại toàn bộ node của spec, ghi lại bảng object ↔ node.</summary>
        public static UIBuildReport Populate(RectTransform root, UISpec spec)
        {
            for (var i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);

            UIPreviewMap.instance.ClearEntries();
            var report = new UIBuildReport { PrefabPath = spec.output };
            var hook = new PreviewHook(spec, root);
            var rootRect = new RectInt(0, 0, spec.referenceWidth, spec.referenceHeight);
            UISpecBuilder.BuildInto(root, rootRect, spec.nodes, report, hook);
            report.Success = report.Errors.Count == 0;
            UIPreviewMap.instance.Persist();
            return report;
        }

        /// <summary>Bật nhóm của trạng thái <paramref name="state"/>, tắt nhóm của các trạng thái khác.</summary>
        public static void ShowState(RectTransform root, UISpec spec, int state)
        {
            if (spec.stateGroups == null || spec.stateGroups.Count == 0) return;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < spec.stateGroups.Count; i++)
            {
                if (string.IsNullOrEmpty(spec.stateGroups[i])) continue;
                foreach (var t in all)
                    if (t.name == spec.stateGroups[i]) t.gameObject.SetActive(i == state);
            }
        }

        private sealed class PreviewHook : IUIBuildHook
        {
            private const int UILayer = 5;

            private readonly UISpec _spec;
            private readonly RectTransform _root;

            public PreviewHook(UISpec spec, RectTransform root)
            {
                _spec = spec;
                _root = root;
            }

            /// <summary>Instance ở preview = nhóm rỗng chứa node của template, để xoá/ẩn từng phần tử ngay trong cây.</summary>
            public RectTransform CreateInstance(RectTransform parent, UISpecNode node, UIBuildReport report)
            {
                var go = new GameObject(node.id, typeof(RectTransform)) { layer = UILayer };
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                report.Created++;

                var template = _spec.templates.FirstOrDefault(t => t.output == node.prefab || t.name == node.prefab);
                if (template == null)
                {
                    report.Warnings.Add($"{node.id}: không thấy template cho prefab '{node.prefab}' — xem trước nhóm rỗng.");
                    return rt;
                }

                // Node template có tọa độ tương đối góc trên-trái item → dịch sang tọa độ tuyệt đối của instance trên demo.
                var shifted = template.nodes.Select(n => Shift(n, node.x, node.y)).ToList();
                var inner = new UIBuildReport();
                var built = UISpecBuilder.BuildInto(rt, new RectInt(node.x, node.y, node.w, node.h), shifted, inner, null);
                foreach (var t in built.Values) Tag(t.gameObject, null, UIPreviewRole.TemplateChild);

                foreach (var warning in inner.Warnings) report.Warnings.Add($"{node.id}: {warning}");
                report.Created += inner.Created;
                return rt;
            }

            public void OnNodeBuilt(UISpecNode node, RectTransform rt) => Tag(rt.gameObject, node, UIPreviewRole.Node);

            /// <summary>Đường dẫn của object trong cây, tính từ root — khoá dự phòng của bảng thẻ.</summary>
            private string PathOf(Transform t)
            {
                var path = t.name;
                for (var p = t.parent; p != null && p != _root; p = p.parent) path = $"{p.name}/{path}";
                return path;
            }

            private static UISpecNode Shift(UISpecNode node, int dx, int dy)
            {
                var copy = UISpecClone.Of(node);
                copy.x += dx;
                copy.y += dy;
                return copy;
            }

            private void Tag(GameObject go, UISpecNode node, UIPreviewRole role)
            {
                UIPreviewMap.instance.Add(go, PathOf(go.transform), node, role);
            }
        }
    }
}
