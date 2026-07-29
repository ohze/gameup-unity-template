using System.Runtime.InteropServices;
using GameUp.Core;
using UnityEngine;

namespace GameUp.SDK
{
    /// <summary>
    /// Cầu nối cấu hình Native Ads xuống lớp native (Android/iOS).
    /// Hiện dùng để set tỉ lệ biến toàn bộ vùng quảng cáo thành CTA (overlay trap).
    /// </summary>
    public static class NativeAdConfigBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void NativeBanner_SetCtaRate(int rate);

        [DllImport("__Internal")]
        private static extern void _iosSetNativeFullScreenCtaRate(int rate);
#endif

        /// <summary>Gọi hàm này khi nhận được giá trị từ Firebase Remote Config (native_cta_click_rate).</summary>
        public static void SetGlobalCtaClickRate(int ratePercent)
        {
            int safeRate = Mathf.Clamp(ratePercent, 0, 100);
            GULogger.Log($"[GameUp Ads] Set Native CTA Click Rate to: {safeRate}%");

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var bannerClass = new AndroidJavaClass("com.gameup.ads.NativeBannerManager"))
                {
                    bannerClass.CallStatic("setCtaClickRate", safeRate);
                }
                using (var fsClass = new AndroidJavaClass("com.plugins.nativebridge.UnityNativeFullScreen"))
                {
                    fsClass.CallStatic("setCtaClickRate", safeRate);
                }
            }
            catch (System.Exception e)
            {
                GULogger.Warning("GameUp", $"[GameUp Ads] Failed to set Android CTA rate: {e.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            NativeBanner_SetCtaRate(safeRate);
            _iosSetNativeFullScreenCtaRate(safeRate);
#endif
        }
    }
}
