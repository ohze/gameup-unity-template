using System;
using UnityEngine;

namespace GameUp.SDK
{
    /// <summary>
    /// Quy tắc hiển thị quảng cáo: inter_capping_time, inter_start_level, và mở rộng sau này.
    /// Dùng Firebase Remote Config cho giá trị; class này quản lý logic (capping, level).
    /// </summary>
    public static class AdsRules
    {
        private static double _lastInterstitialShowTime;
        private static double _pausedSinceLastInterstitialShowSeconds;
        private static int _interstitialCappingPauseDepth;
        private static double _interstitialCappingPauseStartTime;

        /// <summary>
        /// Kiểm tra có được phép hiển thị Interstitial tại level hiện tại không.
        /// Điều kiện: level >= inter_start_level và đã qua ít nhất inter_capping_time (giây) kể từ lần show trước.
        /// </summary>
        /// <param name="currentLevel">Level hiện tại (tính từ 1).</param>
        /// <returns>True nếu được phép show interstitial.</returns>
        public static bool CanShowInterstitial(int currentLevel)
        {
            if (FirebaseRemoteConfigUtils.Instance == null)
                return true;

            int startLevel = FirebaseRemoteConfigUtils.Instance.inter_start_level;
            if (currentLevel < startLevel)
                return false;

            int cappingSeconds = FirebaseRemoteConfigUtils.Instance.inter_capping_time;
            if (cappingSeconds <= 0)
                return true;

            double now = GetCurrentTimeSeconds();
            double elapsed = now - _lastInterstitialShowTime;
            // Loại trừ thời gian đang xem rewarded khỏi bộ đếm inter (pause countdown during rewarded).
            // Nếu đang trong trạng thái pause (ad đang hiển thị), loại trừ cả phần đang chạy (now - pauseStart).
            var paused = _pausedSinceLastInterstitialShowSeconds;
            if (_interstitialCappingPauseDepth > 0)
                paused += Math.Max(0, now - _interstitialCappingPauseStartTime);
            double elapsedActive = elapsed - paused;
            return elapsedActive >= cappingSeconds;
        }

        /// <summary>
        /// Gọi sau khi đã hiển thị Interstitial thành công để cập nhật capping.
        /// </summary>
        public static void RecordInterstitialShown()
        {
            _lastInterstitialShowTime = GetCurrentTimeSeconds();
            _pausedSinceLastInterstitialShowSeconds = 0;
        }

        /// <summary>
        /// Pause bộ đếm capping của Interstitial (dùng khi đang xem Rewarded để không tính thời gian đó vào countdown).
        /// Gọi Begin trước khi show rewarded full-screen, và End khi ad đóng/failed.
        /// </summary>
        public static void BeginInterstitialCappingPause()
        {
            if (_interstitialCappingPauseDepth == 0)
                _interstitialCappingPauseStartTime = GetCurrentTimeSeconds();
            _interstitialCappingPauseDepth++;
        }

        /// <summary>
        /// Kết thúc pause của capping Interstitial, cộng dồn thời gian pause vào tổng thời gian bị loại trừ.
        /// Safe-guard nếu End bị gọi thừa.
        /// </summary>
        public static void EndInterstitialCappingPause()
        {
            if (_interstitialCappingPauseDepth <= 0)
            {
                _interstitialCappingPauseDepth = 0;
                return;
            }

            _interstitialCappingPauseDepth--;
            if (_interstitialCappingPauseDepth == 0)
            {
                var now = GetCurrentTimeSeconds();
                _pausedSinceLastInterstitialShowSeconds += Math.Max(0, now - _interstitialCappingPauseStartTime);
                _interstitialCappingPauseStartTime = 0;
            }
        }

        /// <summary>
        /// Kiểm tra có được phép hiển thị Banner không (theo Remote Config enable_banner).
        /// </summary>
        public static bool IsBannerEnabled()
        {
            if (FirebaseRemoteConfigUtils.Instance == null)
                return true;
            return FirebaseRemoteConfigUtils.Instance.enable_banner;
        }

        private static double GetCurrentTimeSeconds()
        {
            return (double)DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
        }

        /// <summary>
        /// Reset thời gian capping (test hoặc khi cần bỏ qua capping). Không gọi trong production.
        /// </summary>
        public static void ResetInterstitialCappingForTest()
        {
            _lastInterstitialShowTime = 0;
            _pausedSinceLastInterstitialShowSeconds = 0;
            _interstitialCappingPauseDepth = 0;
            _interstitialCappingPauseStartTime = 0;
        }
    }
}
