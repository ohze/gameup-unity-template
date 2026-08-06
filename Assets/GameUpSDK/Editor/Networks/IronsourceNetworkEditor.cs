using GameUp.SDK;
using UnityEditor;

namespace GameUp.SDK.Editor.Setup
{
    [CustomEditor(typeof(IronSourceNetwork))]
    public class IronSourceNetworkEditor : AdNetworkEditorBase
    {
        protected override string HeaderLabel => "IronSource (LevelPlay) Configuration";

        protected override void DrawSection(SerializedObject config, AdMobIdEditorPlatform platform)
            => NetworkEditorUI.DrawIronSourceSection(config.FindProperty("ironSource"), platform);
    }
}
