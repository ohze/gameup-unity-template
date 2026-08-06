using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    // ==========================================
    // ADMOB SETUP TAB -> GameUpAdsConfig.admob
    // ==========================================
    public class AdmobSetupTab : AdsConfigTabBase
    {
        public override string Title => "AdMob / AppOpen";

        protected override void DrawSection(SerializedObject so)
        {
            NetworkEditorUI.DrawAdmobSection(so.FindProperty("admob"), Platform);
        }

        public override void Save()
        {
            base.Save();
            // Google Mobile Ads bắt buộc đọc App ID từ asset riêng của nó khi build.
            GameUpAdsConfigAsset.PushAdmobAppIdsToGoogleSettings(Config);
        }
    }

    // ==========================================
    // MAX SETUP TAB -> GameUpAdsConfig.max
    // ==========================================
    public class MaxSetupTab : AdsConfigTabBase
    {
        public override string Title => "MAX Mediation";

#if MAXSDK_DEPENDENCIES_INSTALLED
        public override bool IsVisible => true;
#else
        public override bool IsVisible => false;
#endif

        protected override void DrawSection(SerializedObject so)
        {
            NetworkEditorUI.DrawMaxSection(so.FindProperty("max"), Platform);
        }
    }

    // ==========================================
    // IRONSOURCE SETUP TAB -> GameUpAdsConfig.ironSource
    // ==========================================
    public class IronSourceSetupTab : AdsConfigTabBase
    {
        public override string Title => "IronSource Mediation";

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        public override bool IsVisible => true;
#else
        public override bool IsVisible => false;
#endif

        protected override void DrawSection(SerializedObject so)
        {
            NetworkEditorUI.DrawIronSourceSection(so.FindProperty("ironSource"), Platform);
        }
    }
}
