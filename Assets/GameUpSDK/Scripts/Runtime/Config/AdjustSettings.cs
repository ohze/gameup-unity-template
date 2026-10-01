using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameUp.SDK
{
    [Serializable]
    public class AdjustSettings
    {
        [Tooltip("App Token Android trên Adjust dashboard.")]
        public string appTokenAndroid;

        [Tooltip("App Token iOS trên Adjust dashboard. Để trống = Adjust không init trên iOS (app iOS/Android thường là 2 app riêng, dùng chung token sẽ ghi install iOS vào app Android).")]
        public string appTokenIOS;

        [Tooltip("Bật = môi trường Sandbox (test). BẮT BUỘC tắt khi build release, nếu không install sẽ không được tính.")]
        public bool sandbox;

        [Tooltip("Log Verbose của Adjust SDK. Tắt khi release.")]
        public bool verboseLog;

        [Tooltip("Cho phép gửi dữ liệu khi app chạy nền.")]
        public bool sendInBackground;

        [Tooltip("Nhận cost data (chi phí campaign) trong attribution callback.")]
        public bool costDataInAttribution;

        [Tooltip("iOS: số giây Adjust chờ user trả lời hộp thoại ATT trước khi gửi install (0 = không chờ, tối đa 360).")]
        [Range(0, 360)]
        public int attConsentWaitingInterval = 120;

        [Tooltip("Map tên event GameUp → event token Adjust. Event không có token sẽ bị bỏ qua.")]
        public List<AdjustEventToken> eventTokens = new List<AdjustEventToken>();

        public string GetAppToken(bool isIOS)
        {
            return isIOS ? appTokenIOS : appTokenAndroid;
        }

        public bool TryGetEventToken(string eventName, out string token)
        {
            token = null;
            if (string.IsNullOrEmpty(eventName) || eventTokens == null) return false;
            foreach (var entry in eventTokens)
            {
                if (entry == null || entry.eventName != eventName || string.IsNullOrWhiteSpace(entry.token)) continue;
                token = entry.token.Trim();
                return true;
            }
            return false;
        }
    }
}
