using GameUp.Core.Editor;
using NUnit.Framework;

namespace GameUp.Core.Tests
{
    public class GUPlayStoreInfoTests
    {
        [Test]
        public void ExtractPackageName_AcceptsUrlOrBarePackage()
        {
            Assert.AreEqual("com.gu003.rogue.dungeonmaster",
                GUPlayStoreInfo.ExtractPackageName("https://play.google.com/store/apps/details?id=com.gu003.rogue.dungeonmaster&hl=vi"));
            Assert.AreEqual("com.company.game", GUPlayStoreInfo.ExtractPackageName(" com.company.game "));
            Assert.IsNull(GUPlayStoreInfo.ExtractPackageName("https://play.google.com/store"));
        }

        [Test]
        public void Parse_ReadsVersionBlockFromPage()
        {
            const string html = "<title id=\"main-title\">Dungeon Architect: Rogue RPG - Apps on Google Play</title>"
                                + "null,[[[\"1.1.0\"]],[[[36]],[[[26,\"8.0\"]]]]],null"
                                + "<div>Updated on</div><div class=\"xg1aie\">Sep 29, 2026</div>";

            var info = GUPlayStoreInfo.Parse("com.gu003.rogue.dungeonmaster", html);

            Assert.AreEqual("Dungeon Architect: Rogue RPG", info.AppTitle);
            Assert.AreEqual("1.1.0", info.VersionName);
            Assert.AreEqual(36, info.TargetSdk);
            Assert.AreEqual(26, info.MinSdk);
            Assert.AreEqual("8.0", info.MinAndroidVersion);
            Assert.AreEqual("Sep 29, 2026", info.UpdatedOn);
        }
    }
}
