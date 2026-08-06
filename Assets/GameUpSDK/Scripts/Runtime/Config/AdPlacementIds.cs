using System;
using UnityEngine;

namespace GameUp.SDK
{
    /// <summary>
    /// Một placement ("where") kèm toàn bộ ID theo từng tầng eCPM.
    /// Đây là đơn vị dữ liệu duy nhất mà cả Inspector lẫn runtime dùng chung:
    /// UI không cần gom/tách list phẳng nữa, nên không còn nguy cơ lệch nhóm.
    /// </summary>
    [Serializable]
    public class AdPlacementIds
    {
        [Tooltip("Tên placement dùng trong code (tham số 'where'). VD: main_menu, end_level.")]
        public string where = "default";

        [Tooltip("ID cho tầng High (chỉ dùng khi bật Waterfall Floor).")]
        public string idHigh;

        [Tooltip("ID cho tầng Medium (chỉ dùng khi bật Waterfall Floor).")]
        public string idMedium;

        [Tooltip("ID tiêu chuẩn. Luôn được dùng khi tắt Waterfall Floor.")]
        public string idAll;

        [Header("Banner Only")]
        public BannerFormatType bannerFormat = BannerFormatType.StandardBanner;
        public BannerSize bannerSize = BannerSize.Adaptive;
        public CollapsibleBannerPlacement collapsible = CollapsibleBannerPlacement.None;

        public string GetId(EcpmFloor floor)
        {
            switch (floor)
            {
                case EcpmFloor.High: return idHigh;
                case EcpmFloor.Medium: return idMedium;
                default: return idAll;
            }
        }

        public void SetId(EcpmFloor floor, string value)
        {
            switch (floor)
            {
                case EcpmFloor.High: idHigh = value; break;
                case EcpmFloor.Medium: idMedium = value; break;
                default: idAll = value; break;
            }
        }

        public bool HasId(EcpmFloor floor) => !string.IsNullOrWhiteSpace(GetId(floor));

        public bool HasAnyId() => HasId(EcpmFloor.High) || HasId(EcpmFloor.Medium) || HasId(EcpmFloor.All);

        public bool Matches(string placementName)
        {
            if (string.IsNullOrWhiteSpace(where) || string.IsNullOrWhiteSpace(placementName)) return false;
            return string.Equals(where.Trim(), placementName.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
