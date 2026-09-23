using System.Collections.Generic;
using UnityEditor;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Trạng thái cửa sổ UI Builder theo từng dev (UserSettings/ — không commit).</summary>
    [FilePath("UserSettings/GameUpUIBuilder.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class UIBuilderSettings : ScriptableSingleton<UIBuilderSettings>
    {
        public string jobName = "NewUI";
        /// <summary>Nguồn tọa độ: dò sprite trên ảnh demo, hay đọc file PSD.</summary>
        public UIBuilderSource source = UIBuilderSource.Demo;
        /// <summary>File PSD/PSB (đường dẫn tuyệt đối hoặc tương đối gốc project — thường nằm ngoài Assets).</summary>
        public string psdPath;
        /// <summary>Xuất PNG từ layer PSD không có art đã cắt (vào <see cref="psdExportFolder"/>/&lt;Tên UI&gt;).</summary>
        public bool psdExportMissing = true;
        public string psdExportFolder = "Assets/_MainProject/Art/UIBuilderExport";
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
        /// <summary>Định vị xong thì tự sinh spec nháp và mở cây xem trước trong scene tạm.</summary>
        public bool autoPreview = true;
        /// <summary>Tên gợi ý cho nhóm tạo ở bước xem trước.</summary>
        public string previewGroupName = "grpNew";
        /// <summary>Phần đã có sẵn trên demo (thanh điều hướng…) — không dựng; tọa độ pixel demo, dùng chung mọi tab.</summary>
        public List<UISkipRegion> skipRegions = new List<UISkipRegion>();
        /// <summary>Số process định vị song song; 0 = tự động (số luồng CPU − 1, tối đa 12).</summary>
        public int locateWorkers;
        /// <summary>Lớp khung vẽ đè lên ảnh demo (sprite khớp / chữ / vị trí bị loại).</summary>
        public PreviewLayers previewLayers = PreviewLayers.Sprites | PreviewLayers.Texts | PreviewLayers.UIRegion | PreviewLayers.Nodes;

        public void Save() => Save(true);
    }
}
