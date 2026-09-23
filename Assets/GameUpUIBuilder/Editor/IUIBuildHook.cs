using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Thay đổi cách <see cref="UISpecBuilder"/> dựng node, dùng cho bản xem trước: node <c>instance</c> ở preview là một
    /// nhóm chứa node của template (chưa có prefab item), và mỗi node dựng xong được gắn thẻ để đồng bộ ngược về spec.
    /// </summary>
    public interface IUIBuildHook
    {
        /// <summary>Tạo node <c>instance</c> thay cho việc gắn prefab; trả null = bỏ node.</summary>
        RectTransform CreateInstance(RectTransform parent, UISpecNode node, UIBuildReport report);

        /// <summary>Gọi sau khi node đã đặt rect và nội dung.</summary>
        void OnNodeBuilt(UISpecNode node, RectTransform rt);
    }
}
