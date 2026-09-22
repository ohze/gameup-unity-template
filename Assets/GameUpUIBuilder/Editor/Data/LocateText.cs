using System;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Một dòng chữ tìm được trên demo (vùng nằm trên sprite nhưng khác pixel sprite): khung, màu chữ chủ đạo và nội dung
    /// đọc bằng OCR (rỗng nếu máy chưa cài OCR).
    /// </summary>
    [Serializable]
    public class LocateText
    {
        public int x;
        public int y;
        public int w;
        public int h;
        public string color;
        public string text;
        public float confidence;

        /// <summary>Màu viền chữ đo trên demo ("#RRGGBB"); rỗng = chữ không viền.</summary>
        public string outlineColor;

        /// <summary>Độ dày viền (px ở độ phân giải demo).</summary>
        public float outlineWidth;

        /// <summary>PSD: căn lề của text layer (left | center | right); rỗng = đoán theo vị trí (OCR).</summary>
        public string align;

        /// <summary>PSD: tên font và cỡ chữ (px) trong Photoshop — chỉ để tham khảo, builder vẫn tự khớp cỡ theo khung.</summary>
        public string font;
        public float fontSize;
    }
}
