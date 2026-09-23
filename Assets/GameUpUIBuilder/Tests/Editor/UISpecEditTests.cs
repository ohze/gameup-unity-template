using System.Collections.Generic;
using System.Linq;
using GameUp.UIBuilder.Editor;
using NUnit.Framework;

namespace GameUp.UIBuilder.Tests
{
    /// <summary>Thao tác sửa cây từ cửa sổ: gom nhóm, đổi tên, đổi cha, đổi thứ tự vẽ, bỏ node.</summary>
    public class UISpecEditTests
    {
        [Test]
        public void Group_Siblings_CreatesParentWithBoundingBoxAtFirstSiblingSlot()
        {
            var spec = Spec(
                Node("imgBg", "", 0, 0, 1080, 2160),
                Node("imgIcon", "", 100, 200, 100, 100),
                Node("txtName", "", 300, 260, 200, 40),
                Node("btnClose", "", 900, 100, 80, 80));

            var error = UISpecEdit.Group(spec, new[] { "imgIcon", "txtName" }, "grpHeader");

            Assert.IsNull(error);
            var group = spec.nodes.First(n => n.id == "grpHeader");
            Assert.AreEqual(UISpecNode.KindEmpty, group.kind);
            // Khung bao: x 100→500, y 200→300.
            Assert.AreEqual((100, 200, 400, 100), (group.x, group.y, group.w, group.h));
            Assert.AreEqual(new[] { "imgBg", "grpHeader", "imgIcon", "txtName", "btnClose" }, Ids(spec));
            Assert.AreEqual("grpHeader", spec.nodes.First(n => n.id == "imgIcon").parent);
        }

        [Test]
        public void Group_NodesUnderDifferentParents_IsRefused()
        {
            var spec = Spec(Node("imgPanel", "", 0, 0, 100, 100), Node("txtTitle", "imgPanel", 10, 10, 50, 20));

            var error = UISpecEdit.Group(spec, new[] { "imgPanel", "txtTitle" }, "grpX");

            Assert.IsNotNull(error);
            Assert.IsFalse(UISpecEdit.Exists(spec, "grpX"));
        }

        [Test]
        public void Group_NameAlreadyUsed_IsRefused()
        {
            var spec = Spec(Node("imgIcon", "", 0, 0, 10, 10), Node("txtName", "", 20, 0, 10, 10));

            Assert.IsNull(UISpecEdit.Group(spec, new[] { "imgIcon" }, "grpA"));
            Assert.IsNotNull(UISpecEdit.Group(spec, new[] { "txtName" }, "grpA"));
        }

        [Test]
        public void Rename_UpdatesChildrenAndStateGroups()
        {
            var spec = Spec(Node("grpState2", "", 0, 0, 100, 100), Node("txtTitle", "grpState2", 10, 10, 50, 20));
            spec.stateGroups = new List<string> { "", "grpState2" };

            var error = UISpecEdit.Rename(spec, "grpState2", "grpRewards");

            Assert.IsNull(error);
            Assert.AreEqual("grpRewards", spec.nodes.First(n => n.id != "txtTitle").id);
            Assert.AreEqual("grpRewards", spec.nodes.First(n => n.id == "txtTitle").parent);
            Assert.AreEqual(new[] { "", "grpRewards" }, spec.stateGroups.ToArray());
        }

        [Test]
        public void Rename_ToNameOfExcludedNode_IsRefused()
        {
            var spec = Spec(Node("imgIcon", "", 0, 0, 10, 10));
            spec.excluded.Add(Node("imgOld", "", 0, 0, 10, 10));

            Assert.IsNotNull(UISpecEdit.Rename(spec, "imgIcon", "imgOld"));
            Assert.AreEqual("imgIcon", spec.nodes[0].id);
        }

        [Test]
        public void Reparent_MovesNodeUnderNewParentAndKeepsParentFirst()
        {
            var spec = Spec(Node("imgPanel", "", 0, 0, 100, 100), Node("btnClose", "", 200, 0, 20, 20));

            UISpecEdit.Reparent(spec, new[] { "btnClose" }, "imgPanel", -1);

            Assert.AreEqual(new[] { "imgPanel", "btnClose" }, Ids(spec));
            Assert.AreEqual("imgPanel", spec.nodes[1].parent);
        }

        [Test]
        public void Reparent_IntoOwnChild_IsIgnored()
        {
            var spec = Spec(Node("imgPanel", "", 0, 0, 100, 100), Node("txtTitle", "imgPanel", 10, 10, 50, 20));

            UISpecEdit.Reparent(spec, new[] { "imgPanel" }, "txtTitle", -1);

            Assert.AreEqual(string.Empty, spec.nodes.First(n => n.id == "imgPanel").parent);
        }

        [Test]
        public void Reparent_AtIndex_InsertsBetweenSiblings()
        {
            var spec = Spec(
                Node("imgA", "", 0, 0, 10, 10),
                Node("imgB", "", 0, 0, 10, 10),
                Node("imgC", "", 0, 0, 10, 10));

            UISpecEdit.Reparent(spec, new[] { "imgC" }, string.Empty, 1);

            Assert.AreEqual(new[] { "imgA", "imgC", "imgB" }, Ids(spec));
        }

        [Test]
        public void Move_SwapsWithSibling_ChangingDrawOrder()
        {
            var spec = Spec(Node("imgA", "", 0, 0, 10, 10), Node("imgB", "", 0, 0, 10, 10));

            UISpecEdit.Move(spec, "imgB", -1);

            Assert.AreEqual(new[] { "imgB", "imgA" }, Ids(spec));
        }

        [Test]
        public void Move_PastFirstSibling_DoesNothing()
        {
            var spec = Spec(Node("imgA", "", 0, 0, 10, 10), Node("imgB", "", 0, 0, 10, 10));

            UISpecEdit.Move(spec, "imgA", -1);

            Assert.AreEqual(new[] { "imgA", "imgB" }, Ids(spec));
        }

        [Test]
        public void Exclude_TakesWholeBranchOutOfBuild()
        {
            var spec = Spec(
                Node("imgPanel", "", 0, 0, 100, 100),
                Node("txtTitle", "imgPanel", 10, 10, 50, 20),
                Node("btnClose", "", 200, 0, 20, 20));

            UISpecEdit.Exclude(spec, "imgPanel");

            Assert.AreEqual(new[] { "btnClose" }, Ids(spec));
            CollectionAssert.AreEquivalent(new[] { "txtTitle", "imgPanel" }, spec.excluded.Select(n => n.id).ToList());
        }

        [Test]
        public void ExcludeThenRestore_PutsNodeBackUnderItsParent()
        {
            var spec = Spec(Node("imgPanel", "", 0, 0, 100, 100), Node("txtTitle", "imgPanel", 10, 10, 50, 20));

            UISpecEdit.Exclude(spec, "txtTitle");
            UISpecExclusions.Restore(spec, "txtTitle");

            Assert.AreEqual(new[] { "imgPanel", "txtTitle" }, Ids(spec));
            Assert.IsEmpty(spec.excluded);
        }

        private static string[] Ids(UISpec spec) => spec.nodes.Select(n => n.id).ToArray();

        private static UISpec Spec(params UISpecNode[] nodes)
        {
            return new UISpec { name = "TestUI", referenceWidth = 1080, referenceHeight = 2160, nodes = nodes.ToList() };
        }

        private static UISpecNode Node(string id, string parent, int x, int y, int w, int h)
        {
            return new UISpecNode { id = id, parent = parent, kind = UISpecNode.KindImage, x = x, y = y, w = w, h = h };
        }
    }
}
