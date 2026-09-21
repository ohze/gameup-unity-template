using System.Collections.Generic;
using System.Linq;
using GameUp.UIBuilder.Editor;
using NUnit.Framework;

namespace GameUp.UIBuilder.Tests
{
    public class UIListExtractorTests
    {
        private const string Row = "Assets/art/boder_rankslot.png";
        private const string CrownTop1 = "Assets/art/crown_top1.png";
        private const string CrownTop2 = "Assets/art/crown_top2.png";

        [Test]
        public void ExtractLists_EvenRows_BecomeScrollOfInstancesWithOverrides()
        {
            var nodes = Rows(5);
            nodes.Add(Image("crown1", CrownTop1, 72, 663, 132, 132));
            nodes.Add(Image("crown2", CrownTop2, 72, 839, 132, 132));
            nodes.Add(Text("txt4", "4", 117, 1228, 36, 54)); // chỉ hàng 4 có số hạng
            var templates = new List<UITemplateSpec>();

            UIListExtractor.ExtractLists(nodes, new[] { "RankItem" }, "Assets/Out", templates, new HashSet<string>(), new List<string>());

            var template = templates.Single();
            Assert.AreEqual("Assets/Out/RankItem.prefab", template.output);
            Assert.AreEqual(UIListExtractor.BackgroundId, template.nodes[0].id);
            Assert.AreEqual(3, template.nodes.Count, "nền + 1 slot vương miện (2 sprite khác nhau cùng chỗ) + 1 slot số hạng");

            var scroll = nodes.Single(n => n.kind == UISpecNode.KindScroll);
            Assert.AreEqual(8f, scroll.spacing);
            var instances = nodes.Where(n => n.parent == scroll.id).OrderBy(n => n.y).ToList();
            Assert.AreEqual(5, instances.Count);
            Assert.IsTrue(instances.All(i => i.prefab == template.output));
            Assert.IsFalse(nodes.Any(n => n.id.StartsWith("row") || n.id.StartsWith("crown")), "hàng gốc và phần tử con đã được thay");

            var crownSlot = template.nodes.Single(n => n.sprite == CrownTop1).id;
            var rankSlot = template.nodes.Single(n => n.kind == UISpecNode.KindText).id;
            Assert.IsTrue(instances[1].overrides.Any(o => o.id == crownSlot && o.sprite == CrownTop2));
            Assert.IsTrue(instances[2].overrides.Any(o => o.id == crownSlot && o.hide));
            Assert.IsTrue(instances[0].overrides.Any(o => o.id == rankSlot && o.hide), "hàng 1 phải ẩn slot số hạng thêm từ hàng 4");
            Assert.IsFalse(instances[3].overrides.Any(o => o.id == rankSlot && o.hide));
        }

        [Test]
        public void ExtractSimilarRows_RowWithSameLayout_BecomesInstanceWithBackgroundOverride()
        {
            var nodes = Rows(3);
            foreach (var row in nodes.ToList())
                nodes.Add(Text($"name_{row.id}", "Player Name", row.x + 307, row.y + 55, 257, 61));
            var player = Image("playerrank", "Assets/art/boder_playerrank.png", 40, 1533, 1000, 176);
            nodes.Add(player);
            nodes.Add(Text("namePlayer", "Player Name", 40 + 310, 1533 + 58, 257, 61));
            var templates = new List<UITemplateSpec>();
            var used = new HashSet<string>();
            UIListExtractor.ExtractLists(nodes, new[] { "RankItem" }, "Assets/Out", templates, used, new List<string>());

            UIListExtractor.ExtractSimilarRows(nodes, templates, used, new List<string>());

            var instance = nodes.Single(n => n.kind == UISpecNode.KindInstance && string.IsNullOrEmpty(n.parent));
            Assert.AreEqual((40, 1533, 1000, 176), (instance.x, instance.y, instance.w, instance.h));
            Assert.IsTrue(instance.overrides.Any(o => o.id == UIListExtractor.BackgroundId && o.sprite == player.sprite));
            Assert.IsFalse(nodes.Contains(player));
        }

        [Test]
        public void ExtractLists_ColumnOfIconsInsideRows_IsNotASeparateList()
        {
            var nodes = Rows(5);
            foreach (var row in nodes.ToList())
                nodes.Add(Image($"avatar_{row.id}", "Assets/art/avatar_001.png", row.x + 187, row.y + 27, 103, 103));
            var templates = new List<UITemplateSpec>();

            UIListExtractor.ExtractLists(nodes, new[] { "RankItem", "Other" }, "Assets/Out", templates, new HashSet<string>(), new List<string>());

            Assert.AreEqual(1, templates.Count, "cột avatar nằm trong hàng thuộc item, không thành danh sách thứ 2");
            Assert.IsTrue(templates[0].nodes.Any(n => n.sprite == "Assets/art/avatar_001.png"));
            Assert.AreEqual(1, nodes.Count(n => n.kind == UISpecNode.KindScroll));
        }

        [Test]
        public void ExtractLists_UnevenRows_AreNotAList()
        {
            var nodes = new List<UISpecNode>
            {
                Image("a", Row, 56, 100, 968, 168), Image("b", Row, 56, 300, 968, 168), Image("c", Row, 56, 900, 968, 168)
            };

            UIListExtractor.ExtractLists(nodes, new[] { "Item" }, "Assets/Out", new List<UITemplateSpec>(), new HashSet<string>(), new List<string>());

            Assert.IsFalse(nodes.Any(n => n.kind == UISpecNode.KindScroll));
        }

        private static List<UISpecNode> Rows(int count)
        {
            return Enumerable.Range(0, count).Select(i => Image($"row{i + 1}", Row, 56, 647 + i * 176, 968, 168)).ToList();
        }

        private static UISpecNode Image(string id, string sprite, int x, int y, int w, int h)
        {
            return new UISpecNode { id = id, kind = UISpecNode.KindImage, sprite = sprite, x = x, y = y, w = w, h = h };
        }

        private static UISpecNode Text(string id, string text, int x, int y, int w, int h)
        {
            return new UISpecNode { id = id, kind = UISpecNode.KindText, text = text, x = x, y = y, w = w, h = h };
        }
    }
}
