using System;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Một vị trí sprite xuất hiện trên demo — pixel, gốc trên-trái của ảnh demo.</summary>
    [Serializable]
    public class LocateMatch
    {
        public int x;
        public int y;
        public int w;
        public int h;
        public float scale;
        public bool sliced;
        public float diff;
        public float zncc;

        /// <summary>Tỉ lệ pixel trùng gần tuyệt đối với sprite (0-1) — cao dù ZNCC thấp = sprite bị che một phần.</summary>
        public float inlier;

        /// <summary>Màu tô (Image.color) ước lượng khi sprite bị tint trên demo, "#RRGGBB"; rỗng = không tint.</summary>
        public string tint;

        /// <summary>PSD: tên layer đặt art này và đường dẫn nhóm layer chứa nó ("popup/top1-3/flag").</summary>
        public string layer;
        public string group;

        /// <summary>Cách sprite xuất hiện trên demo: "1:1", "scale 0.75", "9-slice", kèm tint nếu có.</summary>
        public string MethodLabel
        {
            get
            {
                var method = sliced ? "9-slice" : Math.Abs(scale - 1f) < 0.001f ? "1:1" : $"scale {scale:0.##}";
                return string.IsNullOrEmpty(tint) ? method : $"{method} · tint {tint}";
            }
        }

        /// <summary>Độ tin cậy: tương quan cấu trúc, lệch màu trung bình (0-255), tỉ lệ pixel trùng.</summary>
        public string ScoreLabel => $"ZNCC {zncc:0.00} · lệch màu {diff:0.#} · trùng {inlier:P0}";
    }
}
