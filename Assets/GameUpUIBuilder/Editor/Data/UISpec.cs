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

        /// <summary>Ảnh demo các trạng thái khác (tab 2, 3…) — node chỉ có ở trạng thái k nằm trong nhóm <c>grpState{k}</c>.</summary>
        public List<string> extraDemos = new List<string>();

        /// <summary>id nhóm node riêng của từng trạng thái, theo thứ tự demo (rỗng = trạng thái không có node riêng).</summary>
        public List<string> stateGroups = new List<string>();

        /// <summary>Prefab con (item danh sách) — dựng trước prefab chính.</summary>
        public List<UITemplateSpec> templates = new List<UITemplateSpec>();

        /// <summary>Tên type (Name hoặc FullName) của component gắn vào root, vd <c>PopupResult</c>; rỗng = không gắn.</summary>
        public string rootComponent;

        /// <summary>Thứ tự trong danh sách = thứ tự vẽ: node sau nằm trên node trước cùng cha.</summary>
        public List<UISpecNode> nodes = new List<UISpecNode>();

        /// <summary>Ghi chú cho người review (art thiếu, phần ước lượng, border cần set…) — builder bỏ qua.</summary>
        public List<string> notes = new List<string>();
    }
}
