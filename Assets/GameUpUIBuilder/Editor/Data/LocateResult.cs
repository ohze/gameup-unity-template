using System;
using System.Collections.Generic;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>File JSON do <c>Tools~/ui_locate.py</c> (dò sprite trên demo) hoặc <c>Tools~/ui_psd.py</c> (đọc PSD) sinh ra.</summary>
    [Serializable]
    public class LocateResult
    {
        public const string SourcePsd = "psd";

        public int version;

        /// <summary>"psd" = đọc từ file PSD; rỗng = dò sprite trên ảnh demo.</summary>
        public string source;

        /// <summary>PSD: file nguồn, tên nhóm layer của trạng thái này và tên mọi trạng thái theo thứ tự tab.</summary>
        public string psd;
        public string state;
        public List<string> states = new List<string>();

        /// <summary>PSD: PNG xuất từ layer không có art (tuyệt đối) — Unity import thành Sprite trước khi sinh spec.</summary>
        public List<string> exported = new List<string>();

        /// <summary>PSD: ghi chú của bước đọc (demo khác PSD, layer thiếu hiệu ứng, font…) — chép sang spec.</summary>
        public List<string> notes = new List<string>();
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

        /// <summary>ok | unavailable (venv chưa có OCR) | skipped | none (không có chữ) | psd (lấy từ text layer).</summary>
        public string ocr;

        public bool IsPsd => source == SourcePsd;
    }
}
