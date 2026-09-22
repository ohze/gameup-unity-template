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

        /// <summary>Vị trí đã khớp nhưng bị bộ lọc chéo bỏ đi — để người dùng thấy máy đã loại gì, vì sao.</summary>
        public List<LocateDrop> dropped = new List<LocateDrop>();

        /// <summary>Độ đậm lớp dim đen phía sau popup (0-1) đo từ gameplay bị làm tối; 0 = không thấy lớp dim.</summary>
        public float dimAlpha;

        /// <summary>
        /// true = <see cref="dimAlpha"/> ước lượng từ điểm sáng nhất của phần bị phủ (giả định phía sau có chỗ trắng) —
        /// là cận trên, nên kiểm lại; false = đo từ sprite gameplay khớp dạng tint xám.
        /// </summary>
        public bool dimEstimated;

        /// <summary>Vùng UI chính (không nằm dưới lớp phủ tối); rỗng = màn không có lớp phủ, cả màn là UI.</summary>
        public List<LocateRect> uiRegions = new List<LocateRect>();

        /// <summary>ok | unavailable (venv chưa có OCR) | skipped | none (không có chữ).</summary>
        public string ocr;
    }
}
