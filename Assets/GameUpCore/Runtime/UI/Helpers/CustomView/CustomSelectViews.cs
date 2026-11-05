using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameUp.Core.UI
{
    /// <summary>
    /// Áp trạng thái chọn cho các <see cref="BaseSelectView"/> trên cùng GameObject. <c>views</c> được
    /// <c>OnValidate</c> gom lại mỗi lần validate nên view kéo tay từ object con sẽ bị ghi đè;
    /// view ở object con thì gom qua <see cref="SelectViewComposite"/> đặt trên GameObject này.
    /// Trạng thái không tự áp lúc khởi động: phải gán <see cref="IsSelect"/> khi bind dữ liệu.
    /// </summary>
    public class CustomSelectViews : MonoBehaviour
    {
        [SerializeField] private List<BaseSelectView> views;

        private bool _isSelect;

        public bool IsSelect
        {
            get => _isSelect;
            set
            {
                _isSelect = value;
                if (views == null) return;

                for (var i = 0; i < views.Count; i++)
                {
                    var view = views[i];
                    if (view == null) continue;
                    view.ChangeSelect(value);
                }
            }
        }

        private void OnValidate()
        {
            views = GetComponents<BaseSelectView>().ToList();
        }
    }
}