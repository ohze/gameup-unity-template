using System;
using UnityEngine;

namespace GameUp.SDK
{
    /// <summary>
    /// Adjust không nhận tên event tự do như AppsFlyer — mỗi event phải có token 6 ký tự tạo trên dashboard.
    /// Bảng này map tên event GameUp (vd <c>af_level_achieved</c>) sang token tương ứng.
    /// </summary>
    [Serializable]
    public class AdjustEventToken
    {
        [Tooltip("Tên event GameUp gửi cho MMP (vd af_level_achieved, af_purchase, af_inters_displayed).")]
        public string eventName;

        [Tooltip("Event token 6 ký tự trên Adjust dashboard (All apps → app → Events).")]
        public string token;
    }
}
