using System;
using System.Collections.Generic;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>File JSON do <c>Tools~/ui_locate.py</c> sinh ra.</summary>
    [Serializable]
    public class LocateResult
    {
        public int version;
        public string demo;
        public int demoWidth;
        public int demoHeight;
        public int elapsedMs;
        public int cachedCount;
        public List<LocateSprite> sprites = new List<LocateSprite>();
        public List<LocateText> texts = new List<LocateText>();

        /// <summary>Độ đậm lớp dim đen phía sau popup (0-1) đo từ gameplay bị làm tối; 0 = không thấy lớp dim.</summary>
        public float dimAlpha;

        /// <summary>ok | unavailable (venv chưa có OCR) | skipped | none (không có chữ).</summary>
        public string ocr;
    }
}
