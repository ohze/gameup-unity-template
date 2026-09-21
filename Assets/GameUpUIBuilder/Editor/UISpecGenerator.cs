using System.Collections.Generic;
using System.Linq;
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

        public static UISpec Generate(LocateResult locate, string name, string demoAssetPath, string outputPrefabPath)
        {
            var spec = new UISpec
            {
                name = name,
                demo = demoAssetPath,
                referenceWidth = locate.demoWidth,
                referenceHeight = locate.demoHeight,
                output = outputPrefabPath
            };

            var nodes = CreateNodes(locate, spec.notes);
            AssignParents(nodes);
            spec.nodes = OrderForDrawing(nodes);
            AddUnmatchedNotes(locate, spec.notes);
            return spec;
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
                        kind = isButton ? UISpecNode.KindButton : UISpecNode.KindImage,
                        x = match.x,
                        y = match.y,
                        w = match.w,
                        h = match.h,
                        sprite = assetPath,
                        sliced = match.sliced,
                        raycastTarget = isButton
                    });
                }

                if (sprite.matches.Any(m => m.sliced))
                    AddBorderNote(sprite, assetPath, notes);
            }

            return nodes;
        }

        /// <summary>Cha = node nhỏ nhất chứa trọn node này (không tính chính nó).</summary>
        private static void AssignParents(List<UISpecNode> nodes)
        {
            foreach (var node in nodes)
            {
                UISpecNode best = null;
                foreach (var other in nodes)
                {
                    if (other == node || Area(other) <= Area(node) || !Contains(other, node)) continue;
                    if (best == null || Area(other) < Area(best)) best = other;
                }

                node.parent = best != null ? best.id : string.Empty;
            }
        }

        /// <summary>Cha đứng trước con; cùng cha thì node lớn vẽ trước (nằm dưới).</summary>
        private static List<UISpecNode> OrderForDrawing(List<UISpecNode> nodes)
        {
            var result = new List<UISpecNode>(nodes.Count);
            AppendChildren(string.Empty, nodes, result);
            return result;
        }

        private static void AppendChildren(string parentId, List<UISpecNode> nodes, List<UISpecNode> result)
        {
            foreach (var child in nodes.Where(n => n.parent == parentId).OrderByDescending(Area).ThenBy(n => n.y))
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
            notes.Add("Chưa có: text (TMP), vùng art thiếu, phần nền gameplay phía sau (không thuộc prefab) — bổ sung khi review spec.");
        }

        private static string UniqueId(string baseId, HashSet<string> used)
        {
            var id = baseId;
            for (var i = 2; !used.Add(id); i++) id = $"{baseId}_{i}";
            return id;
        }

        private static long Area(UISpecNode n) => (long)n.w * n.h;

        private static bool Contains(UISpecNode outer, UISpecNode inner)
        {
            return inner.x >= outer.x - ContainTolerance
                   && inner.y >= outer.y - ContainTolerance
                   && inner.x + inner.w <= outer.x + outer.w + ContainTolerance
                   && inner.y + inner.h <= outer.y + outer.h + ContainTolerance;
        }
    }
}
