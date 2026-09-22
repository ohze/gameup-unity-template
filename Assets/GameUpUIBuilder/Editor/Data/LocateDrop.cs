using System;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Một vị trí sprite đã khớp nhưng bị bộ lọc chéo bỏ đi (nằm sau lớp dim, trùng sprite khác…).</summary>
    [Serializable]
    public class LocateDrop
    {
        public string name;
        public string reason;
        public int x;
        public int y;
        public int w;
        public int h;

        public string ReasonLabel => LocateReason.Label(reason);
    }
}
