using System.Collections.Generic;
using GameUp.UIBuilder.Editor;
using NUnit.Framework;

namespace GameUp.UIBuilder.Tests
{
    public class UISpecGeneratorTests
    {
        [Test]
        public void Generate_IconInsideButton_BecomesChildAfterParent()
        {
            var locate = Locate(
                Sprite("icon_ads", Match(721, 1683, 91, 72, false)),
                Sprite("btn_green", Match(225, 1629, 631, 217, true)));

            var spec = UISpecGenerator.Generate(locate, "Popup", "Assets/demo.png", "Assets/Popup.prefab", null);

            Assert.AreEqual("btn_green", spec.nodes[0].id);
            Assert.AreEqual(UISpecNode.KindButton, spec.nodes[0].kind);
            Assert.IsTrue(spec.nodes[0].sliced);
            Assert.AreEqual("icon_ads", spec.nodes[1].id);
            Assert.AreEqual("btn_green", spec.nodes[1].parent);
        }

        [Test]
        public void Generate_RepeatedSprite_GetsNumberedUniqueIds()
        {
            var locate = Locate(Sprite("icon_coin", Match(363, 1390, 103, 103, false), Match(612, 1390, 103, 103, false)));

            var spec = UISpecGenerator.Generate(locate, "Popup", "Assets/demo.png", "Assets/Popup.prefab", null);

            CollectionAssert.AreEquivalent(new[] { "icon_coin_1", "icon_coin_2" }, spec.nodes.ConvertAll(n => n.id));
        }

        [Test]
        public void Generate_SoftAlphaSprite_ListedInNotes()
        {
            var locate = Locate();
            locate.sprites.Add(new LocateSprite { name = "glow_character", status = "unmatched", reason = "soft-alpha" });

            var spec = UISpecGenerator.Generate(locate, "Popup", "Assets/demo.png", "Assets/Popup.prefab", null);

            Assert.IsEmpty(spec.nodes);
            StringAssert.Contains("glow_character", string.Join("\n", spec.notes));
        }

        [Test]
        public void Generate_TextRegionOnSprite_BecomesTextChildWithAutoFontSize()
        {
            var locate = Locate(Sprite("btn_green", Match(225, 1629, 631, 217, true)));
            locate.texts.Add(new LocateText { x = 294, y = 1691, w = 385, h = 54, color = "#FEFEFE" });

            var spec = UISpecGenerator.Generate(locate, "Popup", "Assets/demo.png", "Assets/Popup.prefab", "Assets/Font.asset");

            var text = spec.nodes.Find(n => n.kind == UISpecNode.KindText);
            Assert.AreEqual("btn_green", text.parent);
            Assert.AreEqual(0f, text.fontSize);
            Assert.AreEqual("#FEFEFE", text.color);
            Assert.AreEqual("Assets/Font.asset", text.font);
            Assert.AreEqual(UISpecGenerator.PlaceholderText, text.text);
        }

        [Test]
        public void Generate_TextAlignment_FollowsColumnAndMargins()
        {
            var locate = Locate(Sprite("frame", Match(224, 989, 634, 334, false)), Sprite("popup", Match(116, 413, 848, 1191, false)));
            locate.texts.Add(new LocateText { x = 296, y = 473, w = 489, h = 60 });   // tiêu đề giữa popup
            locate.texts.Add(new LocateText { x = 362, y = 1049, w = 300, h = 26 });  // 3 dòng danh sách thẳng mép trái
            locate.texts.Add(new LocateText { x = 362, y = 1133, w = 214, h = 36 });
            locate.texts.Add(new LocateText { x = 362, y = 1225, w = 421, h = 38 });

            var spec = UISpecGenerator.Generate(locate, "Popup", "Assets/demo.png", "Assets/Popup.prefab", null);

            var byY = spec.nodes.FindAll(n => n.kind == UISpecNode.KindText);
            byY.Sort((a, b) => a.y.CompareTo(b.y));
            CollectionAssert.AreEqual(new[] { "center", "left", "left", "left" }, byY.ConvertAll(n => n.align));
        }

        private static LocateResult Locate(params LocateSprite[] sprites)
        {
            return new LocateResult { demoWidth = 1080, demoHeight = 2160, sprites = new List<LocateSprite>(sprites) };
        }

        private static LocateSprite Sprite(string name, params LocateMatch[] matches)
        {
            // File không cần tồn tại: generator chỉ quy đổi đường dẫn và đọc border (không có asset → coi như chưa set).
            return new LocateSprite
            {
                name = name,
                sprite = UIBuilderPaths.ToAbsolute($"Assets/__missing__/{name}.png"),
                status = LocateSprite.StatusMatched,
                matches = new List<LocateMatch>(matches)
            };
        }

        private static LocateMatch Match(int x, int y, int w, int h, bool sliced)
        {
            return new LocateMatch { x = x, y = y, w = w, h = h, scale = 1f, sliced = sliced, zncc = 1f };
        }
    }
}
