using GameUp.Core.Editor;
using NUnit.Framework;

namespace GameUp.Core.Tests
{
    public class GUAndroidVersionUtilityTests
    {
        [TestCase("1.0.7", "1.0.8")]
        [TestCase("1.0", "1.0.1")]
        [TestCase("1.0.9", "1.0.10")]
        [TestCase("1.2.3.4", "1.2.3.5")]
        [TestCase("Varies with device", "1.0.0")]
        public void BumpVersionName_IncrementsLastPart(string input, string expected)
        {
            Assert.AreEqual(expected, GUAndroidVersionUtility.BumpVersionName(input));
        }

        [TestCase("1.0.7", 107)]
        [TestCase("1.1.0", 110)]
        [TestCase("2", 200)]
        [TestCase("1.0.10", GUAndroidVersionUtility.NoCode)]
        [TestCase("1.10.0", GUAndroidVersionUtility.NoCode)]
        [TestCase("1.2.3.4", GUAndroidVersionUtility.NoCode)]
        [TestCase(null, GUAndroidVersionUtility.NoCode)]
        public void DeriveVersionCode_FollowsMajorMinorPatchConvention(string input, int expected)
        {
            Assert.AreEqual(expected, GUAndroidVersionUtility.DeriveVersionCode(input));
        }

        [Test]
        public void CompareVersionNames_TreatsMissingPartsAsZeroAndInvalidAsLowest()
        {
            Assert.AreEqual(0, GUAndroidVersionUtility.CompareVersionNames("1.1", "1.1.0"));
            Assert.Less(GUAndroidVersionUtility.CompareVersionNames("1.0.9", "1.0.10"), 0);
            Assert.Greater(GUAndroidVersionUtility.CompareVersionNames("1.0.0", null), 0);
        }

        [Test]
        public void ComputeNext_StoreAhead_BumpsFromStore()
        {
            var nextName = GUAndroidVersionUtility.ComputeNextVersionName("1.0.7", "1.0");
            var nextCode = GUAndroidVersionUtility.ComputeNextVersionCode(nextName, "1.0.7", 1, GUAndroidVersionUtility.NoCode);

            Assert.AreEqual("1.0.8", nextName);
            Assert.AreEqual(108, nextCode);
        }

        [Test]
        public void ComputeNext_TestTrackUsedHigherCode_SkipsPastApiMax()
        {
            var nextCode = GUAndroidVersionUtility.ComputeNextVersionCode("1.1.1", "1.1.0", 1, 115);

            Assert.AreEqual(116, nextCode);
        }

        [Test]
        public void ComputeNext_LocalAheadOfStore_BumpsFromLocal()
        {
            var nextName = GUAndroidVersionUtility.ComputeNextVersionName("1.1.0", "1.1.1");
            var nextCode = GUAndroidVersionUtility.ComputeNextVersionCode(nextName, "1.1.0", 111, GUAndroidVersionUtility.NoCode);

            Assert.AreEqual("1.1.2", nextName);
            Assert.AreEqual(112, nextCode);
        }

        [Test]
        public void ComputeNext_PatchOverflowsConvention_StillIncrementsCode()
        {
            var nextName = GUAndroidVersionUtility.ComputeNextVersionName("1.0.9", "1.0");
            var nextCode = GUAndroidVersionUtility.ComputeNextVersionCode(nextName, "1.0.9", 1, GUAndroidVersionUtility.NoCode);

            Assert.AreEqual("1.0.10", nextName);
            Assert.AreEqual(110, nextCode);
        }
    }
}
