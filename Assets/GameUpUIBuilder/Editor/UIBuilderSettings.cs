using System.Collections.Generic;
using UnityEditor;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Trạng thái cửa sổ UI Builder theo từng dev (UserSettings/ — không commit).</summary>
    [FilePath("UserSettings/GameUpUIBuilder.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class UIBuilderSettings : ScriptableSingleton<UIBuilderSettings>
    {
        public string jobName = "NewUI";
        public string demoPath;
        public List<string> artFolders = new List<string>();
        public bool includeSubfolders;
        public string outputFolder = "Assets/_MainProject/Prefabs/UI/Popups";
        public float overlayOpacity = 0.5f;
        public string textFont;
        public bool showDemoPreview = true;
        public bool showMatchRects = true;

        public void Save() => Save(true);
    }
}
