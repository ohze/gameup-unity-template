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
        /// <summary>Demo các trạng thái khác của cùng UI (tab 2, 3…).</summary>
        public List<string> extraDemos = new List<string>();
        public List<string> artFolders = new List<string>();
        public bool includeSubfolders;
        public string outputFolder = "Assets/_MainProject/Prefabs/UI/Popups";
        public float overlayOpacity = 0.5f;
        public string textFont;
        /// <summary>Material TMP gán cho chữ có viền trên demo; rỗng = tự chọn preset outline trong thư mục font.</summary>
        public string textOutlineMaterial;
        public bool showDemoPreview = true;
        public bool showMatchRects = true;

        public void Save() => Save(true);
    }
}
