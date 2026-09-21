using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Dựng hoặc cập nhật prefab uGUI từ <see cref="UISpec"/>.
    /// <para>
    /// Tọa độ spec (pixel demo, gốc trên-trái) được quy đổi sang offsetMin/offsetMax tương đối node cha theo anchor, nên ở
    /// độ phân giải tham chiếu mọi node nằm đúng pixel như demo; anchor chỉ quyết định cách co giãn trên màn khác tỉ lệ.
    /// </para>
    /// <para>
    /// Prefab đã tồn tại thì chỉ cập nhật: tìm node theo tên, sửa rect/nội dung theo spec, giữ nguyên object và component
    /// dev tự thêm — chạy lại sau khi sửa spec không làm mất phần làm tay.
    /// </para>
    /// </summary>
    public static class UISpecBuilder
    {
        private const int UILayer = 5;

        public static UIBuildReport Build(UISpec spec)
        {
            var report = new UIBuildReport { PrefabPath = spec.output };
            if (!Validate(spec, report)) return report;

            EnsureFolder(Path.GetDirectoryName(spec.output)?.Replace('\\', '/'));
            var existing = File.Exists(UIBuilderPaths.ToAbsolute(spec.output));
            var root = existing
                ? PrefabUtility.LoadPrefabContents(spec.output)
                : new GameObject(spec.name, typeof(RectTransform)) { layer = UILayer };

            try
            {
                var rootRect = (RectTransform)root.transform;
                SetStretchFull(rootRect);
                var built = BuildNodes(spec, rootRect, report);
                AttachRootComponent(spec, root, built, report);

                PrefabUtility.SaveAsPrefabAsset(root, spec.output, out var saved);
                report.Success = saved;
                if (!saved) report.Errors.Add($"Không lưu được prefab {spec.output}.");
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }

            return report;
        }

        // ─── Kiểm tra spec ───────────────────────────────────────────────────

        private static bool Validate(UISpec spec, UIBuildReport report)
        {
            if (string.IsNullOrEmpty(spec.name)) report.Errors.Add("Thiếu 'name'.");
            if (string.IsNullOrEmpty(spec.output) || !spec.output.StartsWith("Assets/") || !spec.output.EndsWith(".prefab"))
                report.Errors.Add("'output' phải là asset path dạng Assets/.../Tên.prefab.");
            if (spec.referenceWidth <= 0 || spec.referenceHeight <= 0)
                report.Errors.Add("'referenceWidth/referenceHeight' phải > 0.");

            var ids = new HashSet<string>();
            foreach (var node in spec.nodes)
            {
                if (string.IsNullOrEmpty(node.id)) report.Errors.Add("Có node thiếu 'id'.");
                else if (!ids.Add(node.id)) report.Errors.Add($"Trùng id '{node.id}'.");
                else if (!string.IsNullOrEmpty(node.parent) && !ids.Contains(node.parent))
                    report.Errors.Add($"Node '{node.id}': cha '{node.parent}' phải khai báo trước nó.");
            }

            return report.Errors.Count == 0;
        }

        // ─── Dựng node ───────────────────────────────────────────────────────

        private static Dictionary<string, RectTransform> BuildNodes(UISpec spec, RectTransform root, UIBuildReport report)
        {
            var built = new Dictionary<string, RectTransform>();
            var absolute = new Dictionary<string, RectInt>();
            var siblingIndex = new Dictionary<Transform, int>();
            var rootAbs = new RectInt(0, 0, spec.referenceWidth, spec.referenceHeight);

            foreach (var node in spec.nodes)
            {
                var hasParent = !string.IsNullOrEmpty(node.parent);
                var parent = hasParent ? built[node.parent] : root;
                var parentAbs = hasParent ? absolute[node.parent] : rootAbs;

                var rt = FindOrCreate(root, parent, node.id, report);
                var nodeAbs = new RectInt(node.x, node.y, node.w, node.h);
                ApplyRect(rt, nodeAbs, parentAbs, ResolveAnchor(node, hasParent, rootAbs, report));
                ApplyContent(rt.gameObject, node, report);

                siblingIndex.TryGetValue(parent, out var index);
                rt.SetSiblingIndex(index);
                siblingIndex[parent] = index + 1;
                if (rt.gameObject.activeSelf != node.active) rt.gameObject.SetActive(node.active);

                built[node.id] = rt;
                absolute[node.id] = nodeAbs;
            }

            return built;
        }

        private static RectTransform FindOrCreate(RectTransform root, RectTransform parent, string id, UIBuildReport report)
        {
            var direct = parent.Find(id);
            if (direct is RectTransform found)
            {
                report.Updated++;
                return found;
            }

            // Node đã có nhưng spec đổi cha → chuyển sang cha mới, giữ component làm tay.
            var moved = root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t != root && t.name == id);
            if (moved != null)
            {
                moved.SetParent(parent, false);
                report.Updated++;
                return moved;
            }

            var go = new GameObject(id, typeof(RectTransform)) { layer = UILayer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            report.Created++;
            return rt;
        }

        // ─── Rect & anchor ───────────────────────────────────────────────────

        private static void SetStretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        /// <summary>Đặt rect của node (pixel demo, y hướng xuống) vào cha theo anchor, chính xác tới pixel ở độ phân giải tham chiếu.</summary>
        private static void ApplyRect(RectTransform rt, RectInt node, RectInt parent, (Vector2 min, Vector2 max) anchor)
        {
            // Hệ tọa độ trong cha: gốc dưới-trái, y hướng lên (như Unity).
            float left = node.x - parent.x;
            float right = left + node.width;
            float bottom = parent.y + parent.height - (node.y + node.height);
            float top = bottom + node.height;

            rt.anchorMin = anchor.min;
            rt.anchorMax = anchor.max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.offsetMin = new Vector2(left - anchor.min.x * parent.width, bottom - anchor.min.y * parent.height);
            rt.offsetMax = new Vector2(right - anchor.max.x * parent.width, top - anchor.max.y * parent.height);
        }

        private static (Vector2 min, Vector2 max) ResolveAnchor(UISpecNode node, bool hasParent, RectInt root, UIBuildReport report)
        {
            var name = string.IsNullOrEmpty(node.anchor) ? "auto" : node.anchor.ToLowerInvariant();
            if (name == "auto") name = hasParent ? "center" : GuessAnchor(node, root);

            switch (name)
            {
                case "center": return (new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                case "top": return (new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                case "bottom": return (new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                case "left": return (new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                case "right": return (new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
                case "top-left": return (new Vector2(0f, 1f), new Vector2(0f, 1f));
                case "top-right": return (new Vector2(1f, 1f), new Vector2(1f, 1f));
                case "bottom-left": return (Vector2.zero, Vector2.zero);
                case "bottom-right": return (new Vector2(1f, 0f), new Vector2(1f, 0f));
                case "stretch": return (Vector2.zero, Vector2.one);
                case "stretch-top": return (new Vector2(0f, 1f), new Vector2(1f, 1f));
                case "stretch-middle": return (new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
                case "stretch-bottom": return (Vector2.zero, new Vector2(1f, 0f));
                default:
                    report.Warnings.Add($"{node.id}: anchor '{node.anchor}' không hợp lệ → dùng center.");
                    return (new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            }
        }

        /// <summary>Anchor cho node cấp 1: phủ gần kín → stretch; bám theo phần ba trên/giữa/dưới và mép trái/phải.</summary>
        private static string GuessAnchor(UISpecNode node, RectInt root)
        {
            var fullWidth = node.w >= root.width * 0.95f;
            var fullHeight = node.h >= root.height * 0.95f;
            if (fullWidth && fullHeight) return "stretch";

            var v = (node.y + node.h * 0.5f) / root.height;
            var vertical = v < 0.3f ? "top" : v > 0.7f ? "bottom" : "middle";
            if (fullWidth) return $"stretch-{vertical}";

            var u = (node.x + node.w * 0.5f) / root.width;
            var horizontal = u < 0.2f ? "left" : u > 0.8f ? "right" : null;
            if (vertical == "middle") return horizontal ?? "center";
            return horizontal == null ? vertical : $"{vertical}-{horizontal}";
        }

        // ─── Nội dung ────────────────────────────────────────────────────────

        private static void ApplyContent(GameObject go, UISpecNode node, UIBuildReport report)
        {
            switch (node.kind)
            {
                case UISpecNode.KindImage:
                    RemoveComponent<TextMeshProUGUI>(go);
                    RemoveComponent<Button>(go);
                    ApplyImage(go, node, report);
                    break;
                case UISpecNode.KindButton:
                    RemoveComponent<TextMeshProUGUI>(go);
                    var button = GetOrAdd<Button>(go);
                    button.targetGraphic = ApplyImage(go, node, report);
                    break;
                case UISpecNode.KindText:
                    RemoveComponent<Button>(go);
                    RemoveComponent<Image>(go);
                    ApplyText(go, node, report);
                    break;
                case UISpecNode.KindEmpty:
                    break;
                default:
                    report.Warnings.Add($"{node.id}: kind '{node.kind}' không hợp lệ (empty|image|button|text).");
                    break;
            }
        }

        private static Image ApplyImage(GameObject go, UISpecNode node, UIBuildReport report)
        {
            var image = GetOrAdd<Image>(go);
            image.sprite = LoadSprite(node, report);
            image.type = node.sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = node.preserveAspect;
            image.color = ParseColor(node.color, Color.white, node.id, report);
            image.raycastTarget = node.raycastTarget || node.kind == UISpecNode.KindButton;

            if (node.sliced && image.sprite != null && image.sprite.border == Vector4.zero)
                report.Warnings.Add($"{node.id}: 9-slice nhưng '{image.sprite.name}' chưa set border (Sprite Editor).");
            return image;
        }

        private static void ApplyText(GameObject go, UISpecNode node, UIBuildReport report)
        {
            var text = GetOrAdd<TextMeshProUGUI>(go);
            text.text = node.text ?? string.Empty;
            text.enableAutoSizing = false;
            text.color = ParseColor(node.color, Color.white, node.id, report);
            text.fontStyle = node.bold ? FontStyles.Bold : FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = node.raycastTarget;
            text.alignment = node.align switch
            {
                "left" => TextAlignmentOptions.MidlineLeft,
                "right" => TextAlignmentOptions.MidlineRight,
                _ => TextAlignmentOptions.Midline
            };

            if (!string.IsNullOrEmpty(node.font))
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(node.font);
                if (font != null) text.font = font;
                else report.Warnings.Add($"{node.id}: không thấy TMP_FontAsset '{node.font}'.");
            }

            if (text.font == null)
            {
                report.Warnings.Add($"{node.id}: chưa có font TMP (set 'font' trong spec hoặc Default Font trong TMP Settings).");
                return;
            }

            text.fontSize = node.fontSize > 0f ? node.fontSize : FitFontSize(text, node);
        }

        /// <summary>
        /// Cỡ chữ để chữ hoa cao bằng khung chữ đo trên demo (khung bao nét chữ, không gồm khoảng đệm dòng), rồi thu nhỏ
        /// nếu nội dung tràn chiều ngang. Tính một lần lúc dựng — không bật Auto Size nên không tốn chi phí lúc chạy.
        /// </summary>
        private static float FitFontSize(TextMeshProUGUI text, UISpecNode node)
        {
            var face = text.font.faceInfo;
            var capRatio = face.pointSize > 0 && face.capLine > 0 ? face.capLine / face.pointSize : 0.7f;
            var size = node.h / capRatio;

            text.fontSize = Mathf.Max(1f, Mathf.Round(size));
            var width = PreferredWidth(text);
            if (width <= node.w) return text.fontSize;

            // Độ rộng không tỉ lệ tuyến tính với cỡ chữ (kerning, làm tròn glyph) → co theo tỉ lệ rồi giảm dần tới khi vừa.
            text.fontSize = Mathf.Max(1f, Mathf.Floor(text.fontSize * node.w / width));
            for (var i = 0; i < 20 && text.fontSize > 1f && PreferredWidth(text) > node.w; i++) text.fontSize -= 1f;
            return text.fontSize;
        }

        private static float PreferredWidth(TMP_Text text)
        {
            return text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity).x;
        }

        private static Sprite LoadSprite(UISpecNode node, UIBuildReport report)
        {
            if (string.IsNullOrEmpty(node.sprite)) return null;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(node.sprite);
            if (sprite != null) return sprite;

            report.Warnings.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(node.sprite) != null
                ? $"{node.id}: '{node.sprite}' chưa import dạng Sprite (Texture Type = Sprite (2D and UI))."
                : $"{node.id}: không thấy sprite '{node.sprite}'.");
            return null;
        }

        // ─── Component gốc & gán field ───────────────────────────────────────

        private static void AttachRootComponent(UISpec spec, GameObject root, Dictionary<string, RectTransform> built, UIBuildReport report)
        {
            if (string.IsNullOrEmpty(spec.rootComponent)) return;

            var candidates = TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
                .Where(t => !t.IsAbstract && (t.FullName == spec.rootComponent || t.Name == spec.rootComponent))
                .ToList();
            if (candidates.Count == 0)
            {
                report.Warnings.Add($"Chưa có type '{spec.rootComponent}' (chưa viết script hoặc chưa compile) — bỏ qua gắn component.");
                return;
            }

            if (candidates.Count > 1)
                report.Warnings.Add($"Nhiều type tên '{spec.rootComponent}', dùng {candidates[0].FullName} — ghi FullName để chọn đúng.");

            var component = root.GetComponent(candidates[0]) ?? root.AddComponent(candidates[0]);
            report.BoundFields = BindFields(component, built, report);
        }

        /// <summary>Gán field tham chiếu (GameObject/Component) còn trống có tên trùng id node — vd field <c>btnReward</c> ↔ node <c>btnReward</c>.</summary>
        private static int BindFields(Component component, Dictionary<string, RectTransform> built, UIBuildReport report)
        {
            var nodesByKey = built.ToDictionary(kv => NormalizeKey(kv.Key), kv => kv.Value);
            var serialized = new SerializedObject(component);
            var bound = 0;

            foreach (var field in SerializableReferenceFields(component.GetType()))
            {
                if (!nodesByKey.TryGetValue(NormalizeKey(field.Name), out var node)) continue;
                var property = serialized.FindProperty(field.Name);
                if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (property.objectReferenceValue != null) continue;

                Object value = field.FieldType == typeof(GameObject) ? node.gameObject : node.GetComponent(field.FieldType);
                if (value == null)
                {
                    report.Warnings.Add($"Field '{field.Name}' ({field.FieldType.Name}): node '{node.name}' không có component phù hợp.");
                    continue;
                }

                property.objectReferenceValue = value;
                bound++;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return bound;
        }

        private static IEnumerable<FieldInfo> SerializableReferenceFields(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                foreach (var field in t.GetFields(flags))
                {
                    var serializable = field.IsPublic || field.IsDefined(typeof(SerializeField), false);
                    var isReference = field.FieldType == typeof(GameObject) || typeof(Component).IsAssignableFrom(field.FieldType);
                    if (serializable && isReference) yield return field;
                }
            }
        }

        private static string NormalizeKey(string name) => name.Replace("_", string.Empty).ToLowerInvariant();

        // ─── Tiện ích ────────────────────────────────────────────────────────

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent<T>(out var existing) ? existing : go.AddComponent<T>();
        }

        private static void RemoveComponent<T>(GameObject go) where T : Component
        {
            if (go.TryGetComponent<T>(out var existing)) Object.DestroyImmediate(existing, true);
        }

        private static Color ParseColor(string value, Color fallback, string nodeId, UIBuildReport report)
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            if (ColorUtility.TryParseHtmlString(value, out var color)) return color;
            report.Warnings.Add($"{nodeId}: màu '{value}' không hợp lệ (dùng #RRGGBB hoặc #RRGGBBAA).");
            return fallback;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder)) return;
            var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }
    }
}
