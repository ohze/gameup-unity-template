using System;
using UnityEngine;

namespace GameUp.SDK
{
    /// <summary>
    /// Quy táº¯c hiá»ƒn thá»‹ quáº£ng cÃ¡o: inter_capping_time, inter_start_level, vÃ  má»Ÿ rá»™ng sau nÃ y.
    /// DÃ¹ng Firebase Remote Config cho giÃ¡ trá»‹; class nÃ y quáº£n lÃ½ logic (capping, level).
    /// </summary>
    public static class AdsRules
    {
        private static double _lastInterstitialShowTime;
        private static double _pausedSinceLastInterstitialShowSeconds;
        private static int _interstitialCappingPauseDepth;
        private static double _interstitialCappingPauseStartTime;

        /// <summary>
        /// Kiá»ƒm tra cÃ³ Ä‘Æ°á»£c phÃ©p hiá»ƒn thá»‹ Interstitial táº¡i level hiá»‡n táº¡i khÃ´ng.
        /// Äiá»u kiá»‡n: level >= inter_start_level vÃ  Ä‘Ã£ qua Ã­t nháº¥t inter_capping_time (giÃ¢y) ká»ƒ tá»« láº§n show trÆ°á»›c.
        /// </summary>
        /// <param name="currentLevel">Level hiá»‡n táº¡i (tÃ­nh tá»« 1).</param>
        /// <returns>True náº¿u Ä‘Æ°á»£c phÃ©p show interstitial.</returns>
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
            // Loáº¡i trá»« thá»i gian Ä‘ang xem rewarded khá»i bá»™ Ä‘áº¿m inter (pause countdown during rewarded).
            // Náº¿u Ä‘ang trong tráº¡ng thÃ¡i pause (ad Ä‘ang hiá»ƒn thá»‹), loáº¡i trá»« cáº£ pháº§n Ä‘ang cháº¡y (now - pauseStart).
            var paused = _pausedSinceLastInterstitialShowSeconds;
            if (_interstitialCappingPauseDepth > 0)
                paused += Math.Max(0, now - _interstitialCappingPauseStartTime);
            double elapsedActive = elapsed - paused;
            return elapsedActive >= cappingSeconds;
        }

        /// <summary>
        /// Gá»i sau khi Ä‘Ã£ hiá»ƒn thá»‹ Interstitial thÃ nh cÃ´ng Ä‘á»ƒ cáº­p nháº­t capping.
        /// </summary>
        public static void RecordInterstitialShown()
        {
            _lastInterstitialShowTime = GetCurrentTimeSeconds();
            _pausedSinceLastInterstitialShowSeconds = 0;
        }

        /// <summary>
        /// Pause bá»™ Ä‘áº¿m capping cá»§a Interstitial (dÃ¹ng khi Ä‘ang xem Rewarded Ä‘á»ƒ khÃ´ng tÃ­nh thá»i gian Ä‘Ã³ vÃ o countdown).
        /// Gá»i Begin trÆ°á»›c khi show rewarded full-screen, vÃ  End khi ad Ä‘Ã³ng/failed.
        /// </summary>
        public static void BeginInterstitialCappingPause()
        {
            if (_interstitialCappingPauseDepth == 0)
                _interstitialCappingPauseStartTime = GetCurrentTimeSeconds();
            _interstitialCappingPauseDepth++;
        }

        /// <summary>
        /// Káº¿t thÃºc pause cá»§a capping Interstitial, cá»™ng dá»“n thá»i gian pause vÃ o tá»•ng thá»i gian bá»‹ loáº¡i trá»«.
        /// Safe-guard náº¿u End bá»‹ gá»i thá»«a.
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
        /// Kiá»ƒm tra cÃ³ Ä‘Æ°á»£c phÃ©p hiá»ƒn thá»‹ Banner khÃ´ng (theo Remote Config enable_banner).
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
        /// Reset thá»i gian capping (test hoáº·c khi cáº§n bá» qua capping). KhÃ´ng gá»i trong production.
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
