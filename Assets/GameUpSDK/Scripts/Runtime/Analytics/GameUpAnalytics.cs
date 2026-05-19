using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#if FIREBASE_DEPENDENCIES_INSTALLED
using Firebase.Analytics;
#endif
#if APPSFLYER_DEPENDENCIES_INSTALLED
using AppsFlyerSDK;
#endif
#if FACEBOOK_DEPENDENCIES_INSTALLED && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
using Facebook.Unity;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// Game analytics: Firebase vÃ /hoáº·c AppsFlyer (MMP). GameAnalytics: progression (Start / Complete / Fail) theo
    /// <see href="https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/event-tracking">GA Unity â€” Progression events</see>
    /// (world <c>main</c> â†’ level â†’ wave). Cáº§n init GameAnalytics + keys trong scene.
    /// </summary>
    public static class GameUpAnalytics
    {
        /// <summary>Wave segment khi log theo cáº£ level, chÆ°a vÃ o wave cá»¥ thá»ƒ.</summary>
        private const int GaWholeLevelWave = 0;

        private static string GaWavePart(int wave) => "w" + wave;

        private static void LogGameAnalyticsProgression(
            GaProgressionStatus status,
            int level,
            int wave,
            int? score = null,
            Dictionary<string, string> stringFields = null)
        {
            GameAnalyticsUtils.LogProgression(
                status,
                level.ToString(),
                GaWavePart(wave),
                null,
                score,
                stringFields);
        }

        public static void LogFirebase(string eventName, string paramName = null, string paramValue = null)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            FirebaseUtils.LogEvent(eventName, paramName, paramValue);
        }

        public static void LogFirebaseParams(string eventName, Dictionary<string, string> param)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            if (param == null || param.Count == 0) { FirebaseUtils.LogEventsAPI(eventName, null); return; }
            var fbParam = new Dictionary<object, object>();
            foreach (var p in param)
                if (p.Value != null) fbParam[p.Key] = p.Value;
            FirebaseUtils.LogEventsAPI(eventName, fbParam);
        }

        private static void LogAppsFlyer(string eventName, Dictionary<string, string> eventValues = null)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            AppsFlyerUtils.LogEvents(eventName, eventValues);
        }

        // ---------- Firebase: Virtual currency ----------

        /// <summary> start_level_1 - khi báº¯t Ä‘áº§u level 1 </summary>
        public static void LogStartLevel1()
        {
            LogFirebase(AnalyticsEvent.StartLevel1);
            LogGameAnalyticsProgression(GaProgressionStatus.Start, 1, GaWholeLevelWave);
        }

        public static void LogCompleteLevel1()
        {
            LogFirebase(AnalyticsEvent.CompleteLevel1);
            LogGameAnalyticsProgression(GaProgressionStatus.Complete, 1, GaWholeLevelWave);
        }

        /// <summary> earn_virtual_currency: virtual_currency_name, value, source </summary>
        public static void LogEarnVirtualCurrency(string virtualCurrencyName, string value, string source)
        {
            LogFirebaseParams(AnalyticsEvent.EarnVirtualCurrency, new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamVirtualCurrencyName] = virtualCurrencyName ?? "",
                [AnalyticsEvent.ParamValue] = value ?? "",
                [AnalyticsEvent.ParamSource] = source ?? ""
            });
        }

        /// <summary> spend_virtual_currency: virtual_currency_name, value, source </summary>
        public static void LogSpendVirtualCurrency(string virtualCurrencyName, string value, string source)
        {
            LogFirebaseParams(AnalyticsEvent.SpendVirtualCurrency, new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamVirtualCurrencyName] = virtualCurrencyName ?? "",
                [AnalyticsEvent.ParamValue] = value ?? "",
                [AnalyticsEvent.ParamSource] = source ?? ""
            });
        }

        // ---------- Firebase: Loading ----------

        /// <summary> start_loading - khi báº¯t Ä‘áº§u loading </summary>
        public static void LogStartLoading() => LogFirebase(AnalyticsEvent.StartLoading);

        /// <summary> complete_loading - khi hoÃ n thÃ nh loading, vÃ o mÃ n hÃ¬nh home </summary>
        public static void LogCompleteLoading() => LogFirebase(AnalyticsEvent.CompleteLoading);

        // ---------- Level (Firebase + AppsFlyer af_level_achieved - chung má»¥c Ä‘Ã­ch) ----------

        /// <summary> level_start: level (tá»« 1), index (láº§n báº¯t Ä‘áº§u thá»© bao nhiÃªu); GA progression: level â†’ <c>w0</c>. </summary>
        public static void LogLevelStart(int level, int index)
        {
            var p = new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamLevel] = level.ToString(),
                [AnalyticsEvent.ParamIndex] = index.ToString()
            };
            LogFirebaseParams(AnalyticsEvent.LevelStart, p);
            LogGameAnalyticsProgression(GaProgressionStatus.Start, level, GaWholeLevelWave, stringFields: p);
        }

        /// <summary> level_fail: level, index, time; GA progression: level â†’ <c>w0</c>. </summary>
        public static void LogLevelFail(int level, int index, float timeSeconds)
        {
            var p = new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamLevel] = level.ToString(),
                [AnalyticsEvent.ParamIndex] = index.ToString(),
                [AnalyticsEvent.ParamTime] = timeSeconds.ToString("F0")
            };
            LogFirebaseParams(AnalyticsEvent.LevelFail, p);
            LogGameAnalyticsProgression(GaProgressionStatus.Fail, level, GaWholeLevelWave, stringFields: p);
        }

        /// <summary> level_complete (Firebase) + af_level_achieved (AppsFlyer): level, index, time; optional af_score; GA progression: level â†’ <c>w0</c>. </summary>
        public static void LogLevelComplete(int level, int index, float timeSeconds, int? score = null)
        {
            var fb = new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamLevel] = level.ToString(),
                [AnalyticsEvent.ParamIndex] = index.ToString(),
                [AnalyticsEvent.ParamTime] = timeSeconds.ToString("F0")
            };
            LogFirebaseParams(AnalyticsEvent.LevelComplete, fb);

            var af = new Dictionary<string, string> { [AnalyticsEvent.ParamAfLevel] = level.ToString() };
            if (score.HasValue) af[AnalyticsEvent.ParamAfScore] = score.Value.ToString();
            LogAppsFlyer(AnalyticsEvent.AfLevelAchieved, af);

            var ga = new Dictionary<string, string>(fb);
            if (score.HasValue) ga[AnalyticsEvent.ParamAfScore] = score.Value.ToString();
            if (score.HasValue)
                LogGameAnalyticsProgression(GaProgressionStatus.Complete, level, GaWholeLevelWave, score.Value, ga);
            else
                LogGameAnalyticsProgression(GaProgressionStatus.Complete, level, GaWholeLevelWave, stringFields: ga);
        }

        // ---------- Firebase: Button ----------

        /// <summary> button_click: source (tÃªn button, bao gá»“m vá»‹ trÃ­) </summary>
        public static void LogButtonClick(string source) => LogFirebase(AnalyticsEvent.ButtonClick, AnalyticsEvent.ParamSource, source ?? "");

        // ---------- Firebase: Wave ----------

        /// <summary> wave_start: level, wave; GA progression: level â†’ <c>w</c>{wave}. </summary>
        public static void LogWaveStart(int level, int wave)
        {
            var p = new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamLevel] = level.ToString(),
                [AnalyticsEvent.ParamWave] = wave.ToString()
            };
            LogFirebaseParams(AnalyticsEvent.WaveStart, p);
            LogGameAnalyticsProgression(GaProgressionStatus.Start, level, wave, stringFields: p);
        }

        /// <summary> wave_fail: level, wave; GA progression: level â†’ <c>w</c>{wave}. </summary>
        public static void LogWaveFail(int level, int wave)
        {
            var p = new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamLevel] = level.ToString(),
                [AnalyticsEvent.ParamWave] = wave.ToString()
            };
            LogFirebaseParams(AnalyticsEvent.WaveFail, p);
            LogGameAnalyticsProgression(GaProgressionStatus.Fail, level, wave, stringFields: p);
        }

        /// <summary> wave_complete: level, wave; GA progression: level â†’ <c>w</c>{wave}. </summary>
        public static void LogWaveComplete(int level, int wave)
        {
            var p = new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamLevel] = level.ToString(),
                [AnalyticsEvent.ParamWave] = wave.ToString()
            };
            LogFirebaseParams(AnalyticsEvent.WaveComplete, p);
            LogGameAnalyticsProgression(GaProgressionStatus.Complete, level, wave, stringFields: p);
        }

        // ---------- AppsFlyer only ----------

        /// <summary> af_complete_registration: af_registration_method </summary>
        public static void LogCompleteRegistration(string registrationMethod)
        {
            var p = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(registrationMethod))
                p[AnalyticsEvent.ParamAfRegistrationMethod] = registrationMethod;
            LogAppsFlyer(AnalyticsEvent.AfCompleteRegistration, p.Count > 0 ? p : null);
        }

        /// <summary> af_purchase; <paramref name="level"/> â€” level Ä‘ang chÆ¡i khi mua (Firebase/AppsFlyer/Facebook params). </summary>
        public static void LogPurchase(string currencyCode, int quantity, string contentId, string purchasePrice, string orderId,
            string registrationMethod = null, string customerUserId = null, int? level = null)
        {
            string normalizedCurrency = string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode;
            double revenueAmount = 0d;
            bool hasRevenue = double.TryParse(
                purchasePrice,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out revenueAmount);

            var afParams = new Dictionary<string, string>
            {
                ["af_currency"] = normalizedCurrency,
                [AnalyticsEvent.ParamAfQuantity] = quantity.ToString(),
                [AnalyticsEvent.ParamAfContentId] = contentId ?? "",
                [AnalyticsEvent.ParamAfOrderId] = orderId ?? "",
                ["af_revenue"] = hasRevenue ? revenueAmount.ToString(CultureInfo.InvariantCulture) : "0"
            };
            if (!string.IsNullOrEmpty(registrationMethod)) afParams[AnalyticsEvent.ParamAfRegistrationMethod] = registrationMethod;
            if (!string.IsNullOrEmpty(customerUserId)) afParams[AnalyticsEvent.ParamAfCustomerUserId] = customerUserId;
            if (level.HasValue) afParams[AnalyticsEvent.ParamLevel] = level.Value.ToString();
            if (!AppsFlyerUtils.ShouldSkipManualPurchaseRevenueEvent())
            {
                LogAppsFlyer(AnalyticsEvent.AfPurchase, afParams);
            }
            else
            {
                Debug.Log("[GameUpAnalytics] Skip manual af_purchase for iOS because ROI360 Purchase Connector is enabled.");
            }

            var firebaseParams = new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamAfCurrencyCode] = normalizedCurrency,
                [AnalyticsEvent.ParamAfQuantity] = quantity.ToString(),
                [AnalyticsEvent.ParamAfContentId] = contentId ?? "",
                [AnalyticsEvent.ParamAfPurchasePrice] = hasRevenue ? revenueAmount.ToString(CultureInfo.InvariantCulture) : purchasePrice ?? "",
                [AnalyticsEvent.ParamAfOrderId] = orderId ?? ""
            };
            if (!string.IsNullOrEmpty(registrationMethod)) firebaseParams[AnalyticsEvent.ParamAfRegistrationMethod] = registrationMethod;
            if (!string.IsNullOrEmpty(customerUserId)) firebaseParams[AnalyticsEvent.ParamAfCustomerUserId] = customerUserId;
            if (level.HasValue) firebaseParams[AnalyticsEvent.ParamLevel] = level.Value.ToString();
            LogFirebaseParams(AnalyticsEvent.AfPurchase, firebaseParams);

#if FACEBOOK_DEPENDENCIES_INSTALLED && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            if (!FacebookSdkBootstrap.IsInitialized)
                FacebookSdkBootstrap.TryInitialize();

            if (FB.IsInitialized &&
                decimal.TryParse(
                    hasRevenue ? revenueAmount.ToString(CultureInfo.InvariantCulture) : purchasePrice,
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out var purchaseAmount))
            {
                var fbParams = new Dictionary<string, object>
                {
                    [AnalyticsEvent.ParamAfContentId] = contentId ?? "",
                    [AnalyticsEvent.ParamAfOrderId] = orderId ?? "",
                    [AnalyticsEvent.ParamAfQuantity] = quantity.ToString()
                };

                if (!string.IsNullOrEmpty(customerUserId))
                    fbParams[AnalyticsEvent.ParamAfCustomerUserId] = customerUserId;

                if (level.HasValue)
                    fbParams[AnalyticsEvent.ParamLevel] = level.Value.ToString();

                FB.LogPurchase(purchaseAmount, normalizedCurrency, fbParams);
            }
            else
            {
                Debug.LogWarning($"[GameUpAnalytics] Skip FB.LogPurchase - invalid price '{purchasePrice}' or Facebook SDK not initialized.");
            }
#endif
        }

        /// <summary>
        /// Set AppsFlyer Customer User ID (CUID) Ä‘á»ƒ khá»›p dá»¯ liá»‡u ROI360.
        /// </summary>
        public static void SetCustomerUserId(string userId)
        {
            AppsFlyerUtils.SetCustomerUserId(userId);
        }

        /// <summary> af_tutorial_completion </summary>
        public static void LogTutorialCompletion(bool success, string tutorialId = null)
        {
            var p = new Dictionary<string, string> { [AnalyticsEvent.ParamAfSuccess] = success.ToString().ToLowerInvariant() };
            if (!string.IsNullOrEmpty(tutorialId)) p[AnalyticsEvent.ParamAfTutorialId] = tutorialId;
            LogAppsFlyer(AnalyticsEvent.AfTutorialCompletion, p);
        }

        /// <summary> af_achievement_unlocked </summary>
        public static void LogAchievementUnlocked(string contentId, int? level = null)
        {
            var p = new Dictionary<string, string> { [AnalyticsEvent.ParamContentId] = contentId ?? "" };
            if (level.HasValue) p[AnalyticsEvent.ParamAfLevel] = level.Value.ToString();
            LogAppsFlyer(AnalyticsEvent.AfAchievementUnlocked, p);
        }

        // ---------- Firebase: Ad Revenue Measurement (ARM) ----------

#if APPSFLYER_DEPENDENCIES_INSTALLED
        private static MediationNetwork GetMediationNetworkFromAdNetwork(string adNetwork)
        {
            if (string.IsNullOrEmpty(adNetwork)) return MediationNetwork.Custom;
            var n = adNetwork.Trim().ToLowerInvariant();
            if (n.Contains("admob") || n.Contains("google")) return MediationNetwork.GoogleAdMob;
            if (n.Contains("unity")) return MediationNetwork.Unity;
            if (n.Contains("applovin") || n.Contains("max")) return MediationNetwork.ApplovinMax;
            if (n.Contains("meta") || n.Contains("facebook")) return MediationNetwork.Custom;
            if (n.Contains("chartboost")) return MediationNetwork.ChartBoost;
            if (n.Contains("fyber")) return MediationNetwork.Fyber;
            if (n.Contains("appodeal")) return MediationNetwork.Appodeal;
            if (n.Contains("admost")) return MediationNetwork.Admost;
            if (n.Contains("topon")) return MediationNetwork.Topon;
            if (n.Contains("tradplus")) return MediationNetwork.Tradplus;
            if (n.Contains("yandex")) return MediationNetwork.Yandex;
            if (n.Contains("ironsource")) return MediationNetwork.IronSource;
            return MediationNetwork.Custom;
        }
#endif

        /// <summary>
        /// Logs ad_impression to Firebase for Ad Revenue Measurement (ARM).
        /// Also logs ad revenue to AppsFlyer via LogAdRevenue.
        /// </summary>
        public static void LogAdImpression(AdImpressionData data)
        {
            if (data == null || !data.Revenue.HasValue) return;

            double revenue = data.Revenue.Value;
            string adNetwork = data.AdNetwork ?? "unknown";
            string currency = "USD";

#if FIREBASE_DEPENDENCIES_INSTALLED
            var parameters = new Parameter[]
            {
                new Parameter(FirebaseAnalytics.ParameterAdPlatform, "mediation"),
                new Parameter(FirebaseAnalytics.ParameterAdSource, adNetwork),
                new Parameter(FirebaseAnalytics.ParameterAdUnitName, data.AdUnit ?? ""),
                new Parameter(FirebaseAnalytics.ParameterAdFormat, data.InstanceName ?? data.AdFormat ?? ""),
                new Parameter(FirebaseAnalytics.ParameterCurrency, currency),
                new Parameter(FirebaseAnalytics.ParameterValue, revenue)
            };
            FirebaseUtils.LogEvent(FirebaseAnalytics.EventAdImpression, parameters);
#endif

#if APPSFLYER_DEPENDENCIES_INSTALLED
            var adRevenueData = new AFAdRevenueData(
                adNetwork,
                GetMediationNetworkFromAdNetwork(adNetwork),
                currency,
                revenue);
            var adRevenueParams = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(data.AdUnit)) adRevenueParams[AdRevenueScheme.AD_UNIT] = data.AdUnit;
            if (!string.IsNullOrEmpty(data.AdFormat)) adRevenueParams[AdRevenueScheme.AD_TYPE] = data.AdFormat;
            if (!string.IsNullOrEmpty(data.InstanceName)) adRevenueParams[AdRevenueScheme.PLACEMENT] = data.InstanceName;

            AppsFlyerUtils.LogAdRevenue(adRevenueData, adRevenueParams.Count > 0 ? adRevenueParams : null);
#endif
            Debug.Log($"[GameUpAnalytics] Logged Ad Revenue: {revenue} {currency}, network: {adNetwork}");
        }
    }
}
