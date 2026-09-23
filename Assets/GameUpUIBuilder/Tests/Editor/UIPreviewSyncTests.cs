using System.Collections.Generic;
using System.Linq;
using GameUp.UIBuilder.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GameUp.UIBuilder.Tests
{
    /// <summary>
    /// Cây xem trước → spec: dựng spec thành object thật, sửa cây như người dùng làm trong Hierarchy, rồi kiểm tra spec
    /// đọc ngược ra đúng thứ mong đợi.
    /// </summary>
    public class UIPreviewSyncTests
    {
        private GameObject _canvas;
        private RectTransform _root;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas", typeof(RectTransform));
            var canvasRect = (RectTransform)_canvas.transform;
            canvasRect.anchorMin = canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = new Vector2(1080, 2160);

            var rootGo = new GameObject("TestUI", typeof(RectTransform));
            _root = (RectTransform)rootGo.transform;
            _root.SetParent(canvasRect, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
        }

        [Test]
        public void Read_UntouchedTree_KeepsIdsParentsAndRects()
        {
            var spec = Spec(
                Node("imgPanel", "", 100, 200, 800, 900),
                Node("txtTitle", "imgPanel", 200, 260, 600, 80, UISpecNode.KindText),
                Node("btnClose", "imgPanel", 820, 220, 70, 70, UISpecNode.KindButton));
            UIPreviewBuilder.Populate(_root, spec);

            var synced = UIPreviewSync.Read(_root, spec);

            Assert.AreEqual(new[] { "imgPanel", "txtTitle", "btnClose" }, synced.nodes.Select(n => n.id).ToArray());
            Assert.AreEqual(new[] { "", "imgPanel", "imgPanel" }, synced.nodes.Select(n => n.parent ?? string.Empty).ToArray());
            var title = synced.nodes.First(n => n.id == "txtTitle");
            Assert.AreEqual(new Vector4(200, 260, 600, 80), new Vector4(title.x, title.y, title.w, title.h));
            Assert.AreEqual(UISpecNode.KindButton, synced.nodes.First(n => n.id == "btnClose").kind);
            Assert.IsEmpty(synced.excluded);
        }

        [Test]
        public void Read_DeletedObject_MovesNodeToExcluded()
        {
            var spec = Spec(Node("imgPanel", "", 100, 200, 800, 900), Node("btnClose", "", 820, 220, 70, 70));
            UIPreviewBuilder.Populate(_root, spec);
            Object.DestroyImmediate(_root.Find("btnClose").gameObject);

            var synced = UIPreviewSync.Read(_root, spec);

            Assert.AreEqual(new[] { "imgPanel" }, synced.nodes.Select(n => n.id).ToArray());
            Assert.AreEqual(new[] { "btnClose" }, synced.excluded.Select(n => n.id).ToArray());
            Assert.AreEqual(70, synced.excluded[0].w, "giữ nguyên dữ liệu node để khôi phục được");
        }

        [Test]
        public void Read_DeletedParent_MovesWholeBranchToExcluded()
        {
            var spec = Spec(Node("imgPanel", "", 100, 200, 800, 900), Node("btnClose", "imgPanel", 820, 220, 70, 70));
            UIPreviewBuilder.Populate(_root, spec);
            Object.DestroyImmediate(_root.Find("imgPanel").gameObject);

            var synced = UIPreviewSync.Read(_root, spec);

            Assert.IsEmpty(synced.nodes);
            CollectionAssert.AreEquivalent(new[] { "imgPanel", "btnClose" }, synced.excluded.Select(n => n.id).ToList());
        }

        [Test]
        public void Read_RenamedObject_RenamesNodeWithoutExcluding()
        {
            var spec = Spec(Node("imgPanel", "", 100, 200, 800, 900));
            UIPreviewBuilder.Populate(_root, spec);
            _root.Find("imgPanel").name = "imgBackground";

            var synced = UIPreviewSync.Read(_root, spec);

            Assert.AreEqual(new[] { "imgBackground" }, synced.nodes.Select(n => n.id).ToArray());
            Assert.IsEmpty(synced.excluded);
        }

        [Test]
        public void Read_GroupWithoutRectTransform_WrapsChildren()
        {
            var spec = Spec(Node("imgIcon", "", 100, 200, 100, 100), Node("txtName", "", 300, 260, 200, 40, UISpecNode.KindText));
            UIPreviewBuilder.Populate(_root, spec);
            var group = new GameObject("grpHeader");
            group.transform.SetParent(_root, false);
            _root.Find("imgIcon").SetParent(group.transform, true);
            _root.Find("txtName").SetParent(group.transform, true);

            var synced = UIPreviewSync.Read(_root, spec);

            var header = synced.nodes.First(n => n.id == "grpHeader");
            Assert.AreEqual(UISpecNode.KindEmpty, header.kind);
            // Khung bao hai con: x 100→500, y 200→300.
            Assert.AreEqual(new Vector4(100, 200, 400, 100), new Vector4(header.x, header.y, header.w, header.h));
            Assert.AreEqual("grpHeader", synced.nodes.First(n => n.id == "imgIcon").parent);
        }

        [Test]
        public void Read_ReorderedSiblings_FollowsHierarchyOrder()
        {
            var spec = Spec(Node("imgA", "", 0, 0, 100, 100), Node("imgB", "", 0, 0, 100, 100));
            UIPreviewBuilder.Populate(_root, spec);
            _root.Find("imgB").SetSiblingIndex(0);

            var synced = UIPreviewSync.Read(_root, spec);

            Assert.AreEqual(new[] { "imgB", "imgA" }, synced.nodes.Select(n => n.id).ToArray());
        }

        [Test]
        public void Read_DeactivatedObject_MarksNodeInactive()
        {
            var spec = Spec(Node("imgPanel", "", 100, 200, 800, 900));
            UIPreviewBuilder.Populate(_root, spec);
            _root.Find("imgPanel").gameObject.SetActive(false);

            var synced = UIPreviewSync.Read(_root, spec);

            Assert.IsFalse(synced.nodes[0].active);
        }

        [Test]
        public void Read_ScrollChildren_StayUnderScrollNode()
        {
            var spec = Spec(
                Node("scrList", "", 100, 400, 880, 1000, UISpecNode.KindScroll),
                Node("imgRow", "scrList", 120, 420, 840, 120));
            UIPreviewBuilder.Populate(_root, spec);

            var synced = UIPreviewSync.Read(_root, spec);

            Assert.AreEqual(new[] { "scrList", "imgRow" }, synced.nodes.Select(n => n.id).ToArray());
            Assert.AreEqual("scrList", synced.nodes[1].parent);
            Assert.IsFalse(synced.nodes.Any(n => n.id == "Viewport" || n.id == "Content"), "phần bên trong ScrollRect không vào spec");
        }

        [Test]
        public void Read_InstanceWithDeletedChild_WritesHideOverride()
        {
            var spec = Spec(Node("itemRow", "", 100, 400, 800, 160, UISpecNode.KindInstance));
            spec.nodes[0].prefab = "Assets/Items/RowItem.prefab";
            spec.templates.Add(new UITemplateSpec
            {
                name = "RowItem",
                output = "Assets/Items/RowItem.prefab",
                width = 800,
                height = 160,
                nodes = new List<UISpecNode> { Node("imgCrown", "", 10, 10, 60, 60), Node("imgAvatar", "", 90, 10, 60, 60) }
            });
            UIPreviewBuilder.Populate(_root, spec);
            Object.DestroyImmediate(_root.Find("itemRow/imgCrown").gameObject);

            var synced = UIPreviewSync.Read(_root, spec);

            Assert.AreEqual(new[] { "itemRow" }, synced.nodes.Select(n => n.id).ToArray(), "node của item prefab không vào spec chính");
            var hidden = synced.nodes[0].overrides.Single(o => o.id == "imgCrown");
            Assert.IsTrue(hidden.hide);
        }

        [Test]
        public void Read_RenamedStateGroup_UpdatesStateGroups()
        {
            var spec = Spec(Node("grpState2", "", 0, 0, 1080, 2160));
            spec.stateGroups = new List<string> { string.Empty, "grpState2" };
            UIPreviewBuilder.Populate(_root, spec);
            _root.Find("grpState2").name = "grpRewards";

            var synced = UIPreviewSync.Read(_root, spec);

            Assert.AreEqual(new[] { string.Empty, "grpRewards" }, synced.stateGroups.ToArray());
        }

        [Test]
        public void Read_NodeNamedLikeTemplateChild_KeepsBothAsSeparateNodes()
        {
            // Object trùng tên với một node bên trong item prefab: thẻ phải tra đúng object, không lẫn sang nhau.
            var spec = Spec(Node("itemRow", "", 100, 400, 800, 160, UISpecNode.KindInstance), Node("txtScore", "", 100, 700, 200, 40, UISpecNode.KindText));
            spec.nodes[0].prefab = "Assets/Items/RowItem.prefab";
            spec.templates.Add(new UITemplateSpec
            {
                name = "RowItem",
                output = "Assets/Items/RowItem.prefab",
                width = 800,
                height = 160,
                nodes = new List<UISpecNode> { Node("txtScore", "", 10, 10, 60, 60, UISpecNode.KindText) }
            });
            UIPreviewBuilder.Populate(_root, spec);

            var synced = UIPreviewSync.Read(_root, spec);

            Assert.AreEqual(new[] { "itemRow", "txtScore" }, synced.nodes.Select(n => n.id).ToArray());
            Assert.IsEmpty(synced.excluded);
            var score = synced.nodes.First(n => n.id == "txtScore");
            Assert.AreEqual(700, score.y, "phải là node của spec chính, không phải node cùng tên trong item");
        }

        [Test]
        public void Read_UntouchedAutoAnchor_StaysAuto()
        {
            var spec = Spec(Node("imgTitle", "", 171, 181, 736, 344));
            spec.nodes[0].anchor = "auto";
            UIPreviewBuilder.Populate(_root, spec);

            var synced = UIPreviewSync.Read(_root, spec);

            Assert.AreEqual("auto", synced.nodes[0].anchor);
        }

        [Test]
        public void Read_AnchorChangedByHand_IsWrittenToSpec()
        {
            var spec = Spec(Node("imgTitle", "", 171, 181, 736, 344));
            spec.nodes[0].anchor = "auto";
            UIPreviewBuilder.Populate(_root, spec);
            SetAnchor((RectTransform)_root.Find("imgTitle"), Vector2.zero);

            var synced = UIPreviewSync.Read(_root, spec);

            Assert.AreEqual("bottom-left", synced.nodes[0].anchor);
        }

        /// <summary>Đổi anchor như bảng preset trong Inspector: rect trên màn giữ nguyên, chỉ đổi cách neo.</summary>
        private static void SetAnchor(RectTransform rt, Vector2 anchor)
        {
            var parent = (RectTransform)rt.parent;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var min = parent.InverseTransformPoint(corners[0]);
            var max = parent.InverseTransformPoint(corners[2]);

            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(max.x - min.x, max.y - min.y);
            var point = new Vector2(parent.rect.xMin + parent.rect.width * anchor.x,
                parent.rect.yMin + parent.rect.height * anchor.y);
            rt.anchoredPosition = new Vector2((min.x + max.x) / 2f - point.x, (min.y + max.y) / 2f - point.y);
        }

        private static UISpec Spec(params UISpecNode[] nodes)
        {
            return new UISpec
            {
                name = "TestUI",
                output = "Assets/__UIBuilderTests/TestUI.prefab",
                referenceWidth = 1080,
                referenceHeight = 2160,
                nodes = nodes.ToList()
            };
        }

        private static UISpecNode Node(string id, string parent, int x, int y, int w, int h, string kind = UISpecNode.KindImage)
        {
            return new UISpecNode { id = id, parent = parent, kind = kind, x = x, y = y, w = w, h = h, anchor = "center" };
        }
    }
}
