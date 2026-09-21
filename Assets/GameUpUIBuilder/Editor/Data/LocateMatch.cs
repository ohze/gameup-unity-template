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

        /// <summary>Màu tô (Image.color) ước lượng khi sprite bị tint trên demo, "#RRGGBB"; rỗng = không tint.</summary>
        public string tint;
    }
}
