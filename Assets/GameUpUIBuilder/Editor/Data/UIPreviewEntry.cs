using System;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Một object trong cây xem trước ↔ node spec đã sinh ra nó.</summary>
    [Serializable]
    public class UIPreviewEntry
    {
        /// <summary>Đường dẫn trong cây lúc dựng (vd <c>imgPanel/txtTitle</c>) — khoá dự phòng sau khi Unity recompile.</summary>
        public string path;

        /// <summary>id node lúc dựng — đổi tên object vẫn biết đây là node nào, không tính là xoá rồi thêm mới.</summary>
        public string originalId;

        public UIPreviewRole role;

        /// <summary>Node spec đã dựng ra object này (rỗng với node bên trong item prefab).</summary>
        public UISpecNode source;
    }
}
