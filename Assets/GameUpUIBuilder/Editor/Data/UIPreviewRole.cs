namespace GameUp.UIBuilder.Editor
{
    /// <summary>Vai trò của một object trong cây xem trước — quyết định cách đồng bộ ngược về spec.</summary>
    public enum UIPreviewRole
    {
        /// <summary>Node của spec chính — đồng bộ ngược thành một <see cref="UISpecNode"/>.</summary>
        Node = 0,

        /// <summary>Node bên trong một instance (thuộc item prefab) — xoá/ẩn thành <see cref="UISpecOverride"/>.</summary>
        TemplateChild = 1
    }
}
