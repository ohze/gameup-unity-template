using System.Collections.Generic;
using GameUp.UIBuilder.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameUp.UIBuilder.Tests
{
    public class UISpecBuilderTests
    {
        private const string TempFolder = "Assets/__UIBuilderTests";
        private const string PrefabPath = TempFolder + "/TestPopup.prefab";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempFolder);
        }

        [Test]
        public void Build_CenterNode_KeepsExactPixelRect()
        {
            // Tâm node (540, 1080) = tâm màn 1080×2160 → anchoredPosition (0, 0).
            var spec = CreateSpec(Node("imgCenter", "", 440, 980, 200, 200, "center"));

            var rt = BuildAndFind(spec, "imgCenter");

            Assert.AreEqual(new Vector2(0.5f, 0.5f), rt.anchorMin);
            Assert.AreEqual(Vector2.zero, rt.anchoredPosition);
            Assert.AreEqual(new Vector2(200, 200), rt.sizeDelta);
        }

        [Test]
        public void Build_AutoAnchorNearTop_AnchorsTopWithOffsetFromTopEdge()
        {
            var spec = CreateSpec(Node("imgTitle", "", 171, 181, 736, 344, "auto"));

            var rt = BuildAndFind(spec, "imgTitle");

            Assert.AreEqual(new Vector2(0.5f, 1f), rt.anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 1f), rt.anchorMax);
            // Tâm x = 171 + 368 = 539 → -1 so với giữa; tâm y = 181 + 172 = 353 dưới mép trên.
            Assert.AreEqual(new Vector2(-1f, -353f), rt.anchoredPosition);
            Assert.AreEqual(new Vector2(736, 344), rt.sizeDelta);
        }

        [Test]
        public void Build_ChildNode_PositionedRelativeToParent()
        {
            var spec = CreateSpec(
                Node("btnReward", "", 225, 1629, 631, 217, "auto"),
                Node("imgAds", "btnReward", 721, 1683, 91, 72, "auto"));

            var rt = BuildAndFind(spec, "btnReward/imgAds");

            // Tâm con (766.5, 1719) − tâm cha (540.5, 1737.5) → (226, +18.5) theo trục y hướng lên.
            Assert.AreEqual(new Vector2(226f, 18.5f), rt.anchoredPosition);
            Assert.AreEqual(new Vector2(91, 72), rt.sizeDelta);
        }

        [Test]
        public void Build_StretchFullScreen_HasZeroOffsets()
        {
            var spec = CreateSpec(Node("imgDim", "", 0, 0, 1080, 2160, "auto"));

            var rt = BuildAndFind(spec, "imgDim");

            Assert.AreEqual(Vector2.zero, rt.anchorMin);
            Assert.AreEqual(Vector2.one, rt.anchorMax);
            Assert.AreEqual(Vector2.zero, rt.offsetMin);
            Assert.AreEqual(Vector2.zero, rt.offsetMax);
        }

        [Test]
        public void Build_ExistingPrefab_KeepsManuallyAddedObjects()
        {
            var spec = CreateSpec(Node("imgA", "", 0, 0, 100, 100, "center"));
            Assert.IsTrue(UISpecBuilder.Build(spec).Success);

            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            new GameObject("ManualChild", typeof(RectTransform)).transform.SetParent(contents.transform, false);
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            spec.nodes[0].x = 50;
            var report = UISpecBuilder.Build(spec);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.AreEqual(1, report.Updated);
            Assert.AreEqual(0, report.Created);
            Assert.IsNotNull(prefab.transform.Find("ManualChild"));
        }

        [Test]
        public void Build_KindChangedToText_ReplacesImageWithTmp()
        {
            var spec = CreateSpec(Node("txtLabel", "", 0, 0, 300, 60, "center"));
            UISpecBuilder.Build(spec);

            spec.nodes[0].kind = UISpecNode.KindText;
            spec.nodes[0].text = "Hello";
            UISpecBuilder.Build(spec);

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).transform.Find("txtLabel").gameObject;
            Assert.IsNull(go.GetComponent<Image>());
            Assert.AreEqual("Hello", go.GetComponent<TextMeshProUGUI>().text);
        }

        [Test]
        public void Build_TextWithoutFontSize_FitsCapHeightToBox()
        {
            var node = Node("txtTitle", "", 290, 400, 500, 60, "center");
            node.kind = UISpecNode.KindText;
            node.text = "VICTORY";

            var text = BuildAndFind(CreateSpec(node), "txtTitle").GetComponent<TextMeshProUGUI>();

            var face = text.font.faceInfo;
            var capHeight = text.fontSize * face.capLine / face.pointSize;
            Assert.IsFalse(text.enableAutoSizing);
            Assert.That(capHeight, Is.EqualTo(60f).Within(1.5f));
        }

        [Test]
        public void Build_LongTextWithoutFontSize_ShrinksToFitWidth()
        {
            var node = Node("txtLong", "", 440, 1000, 200, 60, "center");
            node.kind = UISpecNode.KindText;
            node.text = "A very long label that cannot fit";

            var text = BuildAndFind(CreateSpec(node), "txtLong").GetComponent<TextMeshProUGUI>();

            Assert.That(text.GetPreferredValues(text.text).x, Is.LessThanOrEqualTo(200f));
        }

        [Test]
        public void Build_ButtonKind_WiresTargetGraphic()
        {
            var node = Node("btnPlay", "", 0, 0, 300, 100, "center");
            node.kind = UISpecNode.KindButton;

            var go = BuildAndFind(CreateSpec(node), "btnPlay").gameObject;

            Assert.AreSame(go.GetComponent<Image>(), go.GetComponent<Button>().targetGraphic);
            Assert.IsTrue(go.GetComponent<Image>().raycastTarget);
        }

        [Test]
        public void Build_ParentDeclaredAfterChild_FailsValidation()
        {
            var spec = CreateSpec(
                Node("imgChild", "grpParent", 0, 0, 10, 10, "center"),
                Node("grpParent", "", 0, 0, 100, 100, "center"));

            var report = UISpecBuilder.Build(spec);

            Assert.IsFalse(report.Success);
            StringAssert.Contains("grpParent", string.Join(";", report.Errors));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        }

        private static RectTransform BuildAndFind(UISpec spec, string path)
        {
            var report = UISpecBuilder.Build(spec);
            Assert.IsTrue(report.Success, report.Summary);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var found = prefab.transform.Find(path);
            Assert.IsNotNull(found, $"Không thấy node {path}");
            return (RectTransform)found;
        }

        private static UISpec CreateSpec(params UISpecNode[] nodes)
        {
            return new UISpec
            {
                name = "TestPopup",
                output = PrefabPath,
                referenceWidth = 1080,
                referenceHeight = 2160,
                nodes = new List<UISpecNode>(nodes)
            };
        }

        private static UISpecNode Node(string id, string parent, int x, int y, int w, int h, string anchor)
        {
            return new UISpecNode { id = id, parent = parent, kind = UISpecNode.KindImage, x = x, y = y, w = w, h = h, anchor = anchor };
        }
    }
}
