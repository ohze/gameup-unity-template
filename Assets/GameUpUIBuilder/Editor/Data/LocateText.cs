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
    }
}
