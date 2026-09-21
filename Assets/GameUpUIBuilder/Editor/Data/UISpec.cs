using System;
using System.Collections.Generic;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Spec dựng một prefab UI — file <c>UIBuilder/&lt;Tên&gt;/spec.json</c>, người và AI cùng sửa được.</summary>
    [Serializable]
    public class UISpec
    {
        /// <summary>Tên prefab và GameObject root.</summary>
        public string name;

        /// <summary>Ảnh demo gốc (để đối chiếu).</summary>
        public string demo;

        public int referenceWidth = 1080;
        public int referenceHeight = 2160;

        /// <summary>Asset path prefab đầu ra (.prefab).</summary>
        public string output;

        /// <summary>Tên type (Name hoặc FullName) của component gắn vào root, vd <c>PopupResult</c>; rỗng = không gắn.</summary>
        public string rootComponent;

        /// <summary>Thứ tự trong danh sách = thứ tự vẽ: node sau nằm trên node trước cùng cha.</summary>
        public List<UISpecNode> nodes = new List<UISpecNode>();

        /// <summary>Ghi chú cho người review (art thiếu, phần ước lượng, border cần set…) — builder bỏ qua.</summary>
        public List<string> notes = new List<string>();
    }
}
