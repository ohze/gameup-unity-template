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

        [Test]
        public void ExtractLists_LoosePsdRows_DifferentBackgroundsBecomeSpriteAndColorOverrides()
        {
            // PSD Dungeon leaderboard: top 1-3 = shape trắng bo góc tô màu (962×162/163, x 59), hàng 4-5 = PNG xuất (968×172, x 56)
            const string shape = "Assets/out/shape_round_20.png";
            const string flatten = "Assets/out/imgPart.png";
            var nodes = new List<UISpecNode>
            {
                Tinted("row1", shape, 59, 650, 962, 162, "#FFFBC1FF"),
                Tinted("row2", shape, 59, 821, 962, 163, "#A8F2FFFF"),
                Tinted("row3", shape, 59, 997, 962, 163, "#C4FF98FF"),
                Image("row4", flatten, 56, 1169, 968, 172),
                Image("row5", flatten, 56, 1345, 968, 170)
            };
            var templates = new List<UITemplateSpec>();

            UIListExtractor.ExtractLists(nodes, new[] { "RankItem" }, "Assets/Out", templates, new HashSet<string>(), new List<string>(), true);

            var instances = nodes.Where(n => n.kind == UISpecNode.KindInstance).OrderBy(n => n.y).ToList();
            Assert.AreEqual(5, instances.Count, "khác sprite nền vẫn là một danh sách");
            Assert.IsFalse(instances[0].overrides.Any(), "hàng mẫu không ghi đè");
            Assert.IsTrue(instances[1].overrides.Any(o => o.id == UIListExtractor.BackgroundId && o.color == "#A8F2FFFF" && string.IsNullOrEmpty(o.sprite)));
            Assert.IsTrue(instances[3].overrides.Any(o => o.id == UIListExtractor.BackgroundId && o.sprite == flatten && o.color == "#FFFFFFFF"));
        }

        [Test]
        public void ExtractLists_ShortAndLongRankTexts_ShareSlotWideEnoughForLongest()
        {
            var nodes = Rows(4);
            nodes.Add(Text("rank1", "1", 118, 707, 36, 54));
            nodes.Add(Text("rank4", "4-10", 80, 1235, 112, 39)); // hàng 4 (y 1175)
            var templates = new List<UITemplateSpec>();

            UIListExtractor.ExtractLists(nodes, new[] { "RankItem" }, "Assets/Out", templates, new HashSet<string>(), new List<string>());

            var slot = templates.Single().nodes.Single(n => n.kind == UISpecNode.KindText);
            Assert.GreaterOrEqual(slot.w, 112, "khung chữ đủ cho '4-10', không chỉ '1'");
            Assert.AreEqual("center", slot.align, "khung đã nới: căn giữa theo tâm chung, không lệch trái");
        }

        [Test]
        public void ExtractLists_FrameAndAvatarAtSameSpot_AreTwoSlots()
        {
            // PSD Dungeon: khung card_list_frame 125 px và avatar 120 px cùng góc trong mỗi hàng — cỡ gần bằng, chồng > 50%
            var nodes = Rows(3);
            foreach (var row in nodes.ToList())
            {
                nodes.Add(Image($"frame_{row.id}", "Assets/art/card_list_frame.png", row.x + 173, row.y + 20, 125, 125));
                nodes.Add(Image($"avatar_{row.id}", "Assets/art/avatar_001.png", row.x + 173, row.y + 20, 120, 120));
            }
            var templates = new List<UITemplateSpec>();

            UIListExtractor.ExtractLists(nodes, new[] { "RankItem" }, "Assets/Out", templates, new HashSet<string>(), new List<string>());

            var slots = templates.Single().nodes.Skip(1).ToList();
            Assert.AreEqual(2, slots.Count, "khung và avatar là 2 slot, không gộp một");
            CollectionAssert.AreEquivalent(new[] { "Assets/art/card_list_frame.png", "Assets/art/avatar_001.png" }, slots.Select(s => s.sprite));
            Assert.IsFalse(nodes.Where(n => n.kind == UISpecNode.KindInstance).SelectMany(n => n.overrides).Any(o => o.hide));
        }

        private static UISpecNode Tinted(string id, string sprite, int x, int y, int w, int h, string color)
        {
            var node = Image(id, sprite, x, y, w, h);
            node.color = color;
            return node;
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
