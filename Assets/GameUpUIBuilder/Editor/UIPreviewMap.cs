using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Bảng object trong cây xem trước ↔ node spec. Unity không cho gắn MonoBehaviour của assembly Editor lên GameObject,
    /// nên thẻ được giữ ở đây thay vì trên object.
    /// <para>
    /// Trong một phiên dựng, tra thẳng theo tham chiếu object nên luôn đúng. Sau khi Unity recompile (bảng tham chiếu mất,
    /// phần ghi ra <c>UserSettings/</c> còn), tra theo đường dẫn lúc dựng rồi theo tên node — object bị đổi tên và chuyển
    /// chỗ ngay trước lần recompile mới cần đồng bộ lại.
    /// </para>
    /// <para>
    /// Không dùng <c>GetInstanceID</c> làm khoá: Unity cấp lại id của object/component đã huỷ, nên node dựng sau có thể
    /// trùng id với thẻ của node trước và lấy nhầm dữ liệu.
    /// </para>
    /// </summary>
    [FilePath("UserSettings/GameUpUIBuilderPreview.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class UIPreviewMap : ScriptableSingleton<UIPreviewMap>
    {
        /// <summary>instanceID của root cây xem trước; 0 = chưa mở.</summary>
        public int rootId;

        /// <summary>Tên UI (thư mục <c>UIBuilder/&lt;Tên&gt;/</c>) mà cây đang xem trước.</summary>
        public string jobName;

        /// <summary>Trạng thái/tab đang hiện (chỉ số trong <see cref="UISpec.stateGroups"/>).</summary>
        public int state;

        public List<UIPreviewEntry> entries = new List<UIPreviewEntry>();

        [NonSerialized] private Dictionary<GameObject, UIPreviewEntry> _live;

        private Dictionary<GameObject, UIPreviewEntry> Live =>
            _live ?? (_live = new Dictionary<GameObject, UIPreviewEntry>());

        public void Reset(int newRootId, string newJobName)
        {
            rootId = newRootId;
            jobName = newJobName;
            ClearEntries();
        }

        public void ClearEntries()
        {
            entries.Clear();
            Live.Clear();
        }

        public void Add(GameObject go, string path, UISpecNode node, UIPreviewRole role)
        {
            var entry = new UIPreviewEntry
            {
                path = path,
                originalId = node?.id,
                role = role,
                source = node != null ? UISpecClone.Of(node) : null
            };
            entries.Add(entry);
            Live[go] = entry;
        }

        /// <summary>Thẻ của object; <paramref name="path"/> là đường dẫn hiện tại trong cây (dùng khi bảng tham chiếu đã mất).</summary>
        public UIPreviewEntry Find(GameObject go, string path)
        {
            if (Live.TryGetValue(go, out var live)) return live;
            return entries.FirstOrDefault(e => e.path == path)
                   ?? entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.originalId) && e.originalId == go.name);
        }

        public void Persist() => Save(true);
    }
}
