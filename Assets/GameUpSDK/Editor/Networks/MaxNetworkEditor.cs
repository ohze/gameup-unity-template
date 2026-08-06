using GameUp.SDK;
using UnityEditor;

namespace GameUp.SDK.Editor.Setup
{
    [CustomEditor(typeof(MaxNetwork))]
    public class MaxNetworkEditor : AdNetworkEditorBase
    {
        protected override string HeaderLabel => "AppLovin MAX Configuration";

        protected override void DrawSection(SerializedObject config, AdMobIdEditorPlatform platform)
            => NetworkEditorUI.DrawMaxSection(config.FindProperty("max"), platform);
    }
}
