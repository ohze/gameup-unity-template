using System.Collections.Generic;
using System.Linq;
using GameUp.UIBuilder.Editor;
using NUnit.Framework;

namespace GameUp.UIBuilder.Tests
{
    /// <summary>Quyết định "bỏ node này" phải sống qua lần sinh lại spec, và khôi phục lại được nguyên cụm.</summary>
    public class UISpecExclusionsTests
    {
        [Test]
        public void Carry_PreviouslyExcludedNode_IsNotRebuilt()
        {
            var previous = Spec(Node("imgPanel", ""), Node("btnClose", ""));
            previous.excluded.Add(Node("btnClose", ""));
            previous.nodes.RemoveAll(n => n.id == "btnClose");
            var fresh = Spec(Node("imgPanel", ""), Node("btnClose", ""));

            UISpecExclusions.Carry(fresh, previous);

            Assert.AreEqual(new[] { "imgPanel" }, fresh.nodes.Select(n => n.id).ToArray());
            Assert.AreEqual(new[] { "btnClose" }, fresh.excluded.Select(n => n.id).ToArray());
        }

        [Test]
        public void Carry_ExcludedParent_AlsoDropsChildren()
        {
            var previous = Spec(Node("imgPanel", ""));
            previous.excluded.Add(Node("grpHeader", ""));
            var fresh = Spec(Node("grpHeader", ""), Node("txtTitle", "grpHeader"), Node("imgPanel", ""));

            UISpecExclusions.Carry(fresh, previous);

            Assert.AreEqual(new[] { "imgPanel" }, fresh.nodes.Select(n => n.id).ToArray());
            CollectionAssert.AreEquivalent(new[] { "grpHeader", "txtTitle" }, fresh.excluded.Select(n => n.id).ToList());
        }

        [Test]
        public void Restore_ChildOfExcludedParent_BringsBackParentFirst()
        {
            var spec = Spec(Node("imgPanel", ""));
            spec.excluded.Add(Node("grpHeader", ""));
            spec.excluded.Add(Node("txtTitle", "grpHeader"));

            UISpecExclusions.Restore(spec, "txtTitle");

            Assert.IsEmpty(spec.excluded.Where(n => n.id == "grpHeader" || n.id == "txtTitle"));
            var ids = spec.nodes.Select(n => n.id).ToList();
            Assert.Less(ids.IndexOf("grpHeader"), ids.IndexOf("txtTitle"), "cha phải đứng trước con thì builder mới dựng được");
        }

        [Test]
        public void RestoreAll_EmptiesExcludedList()
        {
            var spec = Spec(Node("imgPanel", ""));
            spec.excluded.Add(Node("btnClose", ""));
            spec.excluded.Add(Node("imgIcon", ""));

            UISpecExclusions.RestoreAll(spec);

            Assert.IsEmpty(spec.excluded);
            Assert.AreEqual(3, spec.nodes.Count);
        }

        private static UISpec Spec(params UISpecNode[] nodes)
        {
            return new UISpec { name = "TestUI", nodes = nodes.ToList(), excluded = new List<UISpecNode>() };
        }

        private static UISpecNode Node(string id, string parent)
        {
            return new UISpecNode { id = id, parent = parent, kind = UISpecNode.KindImage, w = 10, h = 10 };
        }
    }
}
