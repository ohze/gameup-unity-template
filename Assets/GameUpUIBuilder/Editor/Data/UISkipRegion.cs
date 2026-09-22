using System;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Vùng trên demo đã có sẵn (thanh điều hướng, thanh trên cùng…) — không dựng lại. Sprite/chữ nằm phần lớn trong vùng
    /// bị bỏ khỏi spec; có <see cref="prefab"/> thì đặt instance prefab đó vào đúng khung.
    /// </summary>
    [Serializable]
    public class UISkipRegion
    {
        /// <summary>Tên gợi nhớ, cũng là tên node instance (vd <c>NavBar</c>).</summary>
        public string name;

        public int x;
        public int y;
        public int w;
        public int h;

        /// <summary>Asset path prefab thay cho vùng; rỗng = bỏ hẳn, không đặt gì.</summary>
        public string prefab;
    }
}
