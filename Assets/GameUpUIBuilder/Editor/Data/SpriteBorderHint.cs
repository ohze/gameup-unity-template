using System;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Border 9-slice ước lượng từ pixel của sprite (px, theo ảnh gốc).</summary>
    [Serializable]
    public class SpriteBorderHint
    {
        public int left;
        public int right;
        public int top;
        public int bottom;

        public bool IsEmpty => left == 0 && right == 0 && top == 0 && bottom == 0;

        public override string ToString() => $"L{left} R{right} T{top} B{bottom}";
    }
}
