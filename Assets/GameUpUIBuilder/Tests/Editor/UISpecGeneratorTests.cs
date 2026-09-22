using System.Collections.Generic;
using System.Linq;
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
        public void Generate_DimAlpha_AddsFullScreenDimDrawnFirst()
        {
            var locate = Locate(Sprite("popup", Match(24, 250, 1032, 1656, false)));
            locate.dimAlpha = 0.45f;
            locate.dimEstimated = true;

            var spec = UISpecGenerator.Generate(locate, "Popup", "Assets/demo.png", "Assets/Popup.prefab", null);

            var dim = spec.nodes[0];
            Assert.AreEqual("imgDim", dim.id);
            Assert.AreEqual(string.Empty, dim.parent);
            Assert.AreEqual("stretch", dim.anchor);
            Assert.AreEqual("#00000073", dim.color);
            StringAssert.Contains("ước lượng", string.Join("\n", spec.notes));
        }

        [Test]
        public void Generate_SkipRegion_DropsNodesInsideAndPlacesPrefabInstance()
        {
            var locate = Locate(
                Sprite("btn_upgrade", Match(278, 1714, 440, 120, false)),
                Sprite("bottom_tab", Match(0, 1946, 200, 214, false), Match(480, 1946, 200, 214, false)),
                Sprite("icon_feature_store", Match(39, 1991, 123, 134, false)));
            var skips = new[] { new UISkipRegion { name = "NavBar", x = 0, y = 1930, w = 1080, h = 230, prefab = "Assets/UI/NavBar.prefab" } };

            var spec = UISpecGenerator.Generate(new[] { locate }, "Party", new[] { "Assets/demo.png" }, "Assets/Party.prefab", null, null, skips);

            CollectionAssert.AreEqual(new[] { "btn_upgrade", "NavBar" }, spec.nodes.ConvertAll(n => n.id));
            var nav = spec.nodes[1];
            Assert.AreEqual(UISpecNode.KindInstance, nav.kind);
            Assert.AreEqual("Assets/UI/NavBar.prefab", nav.prefab);
            Assert.AreEqual(1930, nav.y);
            Assert.AreEqual(1, spec.skipRegions.Count);
        }

        [Test]
        public void Generate_SkipRegionWithoutPrefab_JustDropsNodes()
        {
            var locate = Locate(Sprite("bottom_tab", Match(0, 1946, 200, 214, false)), Sprite("btn_upgrade", Match(278, 1714, 440, 120, false)));
            var skips = new[] { new UISkipRegion { name = "NavBar", x = 0, y = 1930, w = 1080, h = 230 } };

            var spec = UISpecGenerator.Generate(new[] { locate }, "Party", new[] { "Assets/demo.png" }, "Assets/Party.prefab", null, null, skips);

            CollectionAssert.AreEqual(new[] { "btn_upgrade" }, spec.nodes.ConvertAll(n => n.id));
            StringAssert.Contains("NavBar", string.Join("\n", spec.notes));
        }

        [Test]
        public void Generate_FullScreenBackground_IsNotParentOfEverything()
        {
            var locate = Locate(
                Sprite("bg", Match(0, 0, 1080, 2160, false)),
                Sprite("btn_back", Match(40, 40, 140, 150, false)),
                Sprite("icon_back", Match(73, 70, 72, 66, false)));
            var tray = Sprite("bottom_tray", Match(0, 912, 1080, 1034, false));
            tray.lowTexture = true; // panel phẳng — vẫn phải vẽ trên nền
            locate.sprites.Add(tray);

            var spec = UISpecGenerator.Generate(locate, "Party", "Assets/demo.png", "Assets/Party.prefab", null);

            Assert.AreEqual("bg", spec.nodes[0].id);
            Assert.AreEqual("stretch", spec.nodes[0].anchor);
            Assert.AreEqual(string.Empty, spec.nodes.Find(n => n.id == "bottom_tray").parent);
            Assert.AreEqual(string.Empty, spec.nodes.Find(n => n.id == "btn_back").parent);
            Assert.AreEqual("btn_back", spec.nodes.Find(n => n.id == "icon_back").parent);
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

        [TestCase("Remove Ads", "txtRemoveAds")]
        [TestCase("2,000 coins", "txt2000Coins")]
        [TestCase("5 Random Fighter cards", "txt5RandomFighterCards")]
        [TestCase("Phần thưởng đặc biệt hôm nay nè", "txtPhanThuongDacBiet")]
        [TestCase("₫49,000", "txt49000")]
        [TestCase("!!!", "txt")]
        public void TextId_BuildsReadableAsciiId(string text, string expected)
        {
            Assert.AreEqual(expected, UISpecGenerator.TextId(text));
        }

        [Test]
        public void Generate_OcrText_UsedAsContentAndId_LowConfidenceNoted()
        {
            var locate = Locate(Sprite("popup", Match(116, 413, 848, 1191, false)));
            locate.ocr = "ok";
            locate.texts.Add(new LocateText { x = 296, y = 473, w = 489, h = 60, text = "Remove Ads", confidence = 1f });
            locate.texts.Add(new LocateText { x = 390, y = 1403, w = 301, h = 65, text = "49,000", confidence = 0.6f });

            var spec = UISpecGenerator.Generate(locate, "Popup", "Assets/demo.png", "Assets/Popup.prefab", null);

            var title = spec.nodes.Find(n => n.id == "txtRemoveAds");
            Assert.IsNotNull(title);
            Assert.AreEqual("Remove Ads", title.text);
            StringAssert.Contains("txt49000", string.Join("\n", spec.notes));
        }

        [Test]
        public void Generate_TwoDemos_SharedOnceAndStateOnlyNodesInGroups()
        {
            var banner = Sprite("banner_title", Match(28, 75, 1024, 148, false));
            var tab1 = Locate(banner, Sprite("crown_top1", Match(72, 663, 132, 132, false)));
            tab1.texts.Add(new LocateText { x = 222, y = 1793, w = 221, h = 45, text = "Leaderboard", confidence = 1f });
            var tab2 = Locate(Sprite("banner_title", Match(28, 75, 1024, 148, false)), Sprite("crown_top2", Match(72, 663, 132, 132, false)));
            tab2.texts.Add(new LocateText { x = 222, y = 1793, w = 221, h = 45, text = "Leaderboard", confidence = 1f });

            var spec = UISpecGenerator.Generate(new[] { tab1, tab2 }, "Popup", new[] { "Assets/d1.png", "Assets/d2.png" }, "Assets/Popup.prefab", null);

            Assert.AreEqual(1, spec.nodes.Count(n => n.id == "banner_title"), "phần giống nhau dựng một lần");
            Assert.AreEqual(1, spec.nodes.Count(n => n.text == "Leaderboard"));
            Assert.AreEqual(2, spec.stateGroups.Count);
            var group1 = spec.nodes.Single(n => n.id == spec.stateGroups[0]);
            var group2 = spec.nodes.Single(n => n.id == spec.stateGroups[1]);
            Assert.IsTrue(group1.active);
            Assert.IsFalse(group2.active);
            Assert.AreEqual(group1.id, spec.nodes.Single(n => n.id == "crown_top1").parent);
            Assert.AreEqual(group2.id, spec.nodes.Single(n => n.id == "crown_top2").parent);
            CollectionAssert.AreEqual(new[] { "Assets/d2.png" }, spec.extraDemos);
        }

        [Test]
        public void Generate_SharedLabelOnSwappedTabButtons_CopiedIntoEachTabUnderButton()
        {
            // PSD Dungeon ranking: nhãn tab ở cùng chỗ trong 2 tab, nhưng nút tab đổi art (btn_tab_1 / btn_tab_2) → nút là phần
            // riêng từng tab, vẽ sau phần chung. Nhãn để chung sẽ bị nút che.
            var tab1 = Locate(Sprite("btn_tab_1", Match(138, 1768, 376, 91, false)));
            tab1.texts.Add(new LocateText { x = 231, y = 1801, w = 202, h = 27, text = "Leaderboard", confidence = 1f });
            var tab2 = Locate(Sprite("btn_tab_2", Match(138, 1768, 376, 91, false)));
            tab2.texts.Add(new LocateText { x = 231, y = 1801, w = 202, h = 27, text = "Leaderboard", confidence = 1f });

            var spec = UISpecGenerator.Generate(new[] { tab1, tab2 }, "Popup", new[] { "Assets/d1.png", "Assets/d2.png" }, "Assets/Popup.prefab", null);

            var labels = spec.nodes.Where(n => n.text == "Leaderboard").ToList();
            Assert.AreEqual(2, labels.Count, "mỗi tab một nhãn");
            CollectionAssert.AreEquivalent(new[] { "btn_tab_1", "btn_tab_2" }, labels.Select(n => n.parent));
            foreach (var label in labels)
                Assert.Greater(spec.nodes.IndexOf(label), spec.nodes.FindIndex(n => n.id == label.parent), "nhãn vẽ sau nút");
        }

        [Test]
        public void Generate_PsdText_KeepsLayerAlignmentAndIdIgnoresRichTextTags()
        {
            var locate = Locate(Sprite("border_popup", Match(24, 131, 1032, 1656, false)));
            locate.source = LocateResult.SourcePsd;
            locate.ocr = "psd";
            locate.notes.Add("Font trong PSD: Pusia-Bold");
            // căn giữa theo lề thì đoán "center" — text layer ghi "left" phải được giữ
            locate.texts.Add(new LocateText
            {
                x = 182, y = 1731, w = 716, h = 28, text = "Top <color=#37F352>10</color> Promote", align = "left", confidence = 1f
            });

            var spec = UISpecGenerator.Generate(locate, "Popup", "Assets/demo.png", "Assets/Popup.prefab", null);

            var text = spec.nodes.Single(n => n.kind == UISpecNode.KindText);
            Assert.AreEqual("txtTop10Promote", text.id);
            Assert.AreEqual("left", text.align);
            Assert.AreEqual("Top <color=#37F352>10</color> Promote", text.text);
            var notes = string.Join("\n", spec.notes);
            StringAssert.Contains("Pusia-Bold", notes);
            StringAssert.DoesNotContain("OCR", notes);
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
