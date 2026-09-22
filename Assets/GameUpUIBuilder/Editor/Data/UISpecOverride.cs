using System;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Ghi đè một node con của item prefab trên một instance (hàng khác nhau trong danh sách).</summary>
    [Serializable]
    public class UISpecOverride
    {
        /// <summary>id node trong template (tên GameObject con của item prefab).</summary>
        public string id;

        /// <summary>Ẩn node này ở instance (hàng không có phần tử đó).</summary>
        public bool hide;

        /// <summary>Asset path sprite thay thế; rỗng = giữ của template.</summary>
        public string sprite;

        /// <summary>true thì gán <see cref="text"/> (cho phép gán chuỗi rỗng).</summary>
        public bool setText;

        public string text;

        /// <summary>Màu Image thay thế (#RRGGBB / #RRGGBBAA) — hàng cùng sprite nền trắng nhưng khác màu; rỗng = giữ.</summary>
        public string color;
    }
}
