using GameUp.Core;
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
    /// Game analytics: Firebase, AppsFlyer (MMP), AppMetrica (tùy chọn), GameAnalytics progression (Start / Complete / Fail) theo
    /// <see href="https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/event-tracking">GA Unity — Progression events</see>
    /// (world <c>main</c> → level → wave). Cần init GameAnalytics + keys trong scene.
    /// </summary>
    public static class GameUpAnalytics
    {
        /// <summary>Wave segment khi log theo cả level, chưa vào wave cụ thể.</summary>
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

        private static void LogAppMetrica(string eventName, Dictionary<string, string> parameters = null)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            AppMetricaUtils.LogEvent(eventName, parameters);
        }

        private static void LogAppMetrica(string eventName, string paramName, string paramValue)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            Dictionary<string, string> p = null;
            if (!string.IsNullOrEmpty(paramName))
                p = new Dictionary<string, string> { [paramName] = paramValue ?? "" };
            AppMetricaUtils.LogEvent(eventName, p);
        }

        private static Dictionary<string, string> BuildVideoAdsParams(
            string adType,
            string placement,
            string result,
            bool hasConnection)
        {
            var p = new Dictionary<string, string>
            {
                [AppMetricaEvent.ParamAdType] = adType ?? "",
                [AppMetricaEvent.ParamPlacement] = placement ?? "",
                [AppMetricaEvent.ParamResult] = result ?? "",
                [AppMetricaEvent.ParamConnection] = hasConnection ? "true" : "false"
            };
            return p;
        }

        /// <summary>AppMetrica: video_ads_available — mỗi lần user request show (IDLE spec).</summary>
        public static void LogVideoAdsAvailable(string adType, string placement, string result, bool hasConnection)
        {
            if (!VideoAdsAppMetricaTracker.ShouldSendAvailable(placement, adType, result)) return;
            LogAppMetrica(AppMetricaEvent.VideoAdsAvailable,
                BuildVideoAdsParams(adType, placement, result, hasConnection));
        }

        /// <summary>AppMetrica: video_ads_started — khi ad bắt đầu hiển thị (chỉ sau available success).</summary>
        public static void LogVideoAdsStarted(string adType, string placement, string result, bool hasConnection)
        {
            if (!VideoAdsAppMetricaTracker.CanSendStarted(placement, adType)) return;
            LogAppMetrica(AppMetricaEvent.VideoAdsStarted,
                BuildVideoAdsParams(adType, placement, result, hasConnection));
        }

        /// <summary>AppMetrica: video_ads_watch — sau khi ad kết thúc (watched / canceled / failed).</summary>
        public static void LogVideoAdsWatch(string adType, string placement, string result, bool hasConnection)
        {
            LogAppMetrica(AppMetricaEvent.VideoAdsWatch,
                BuildVideoAdsParams(adType, placement, result, hasConnection));
            VideoAdsAppMetricaTracker.ClearSession();
        }

        // ---------- Firebase: Virtual currency ----------

        /// <summary> start_level_1 - khi bắt đầu level 1 </summary>
        public static void LogStartLevel1()
        {
            LogFirebase(AnalyticsEvent.StartLevel1);
            LogAppMetrica(AnalyticsEvent.StartLevel1);
            LogGameAnalyticsProgression(GaProgressionStatus.Start, 1, GaWholeLevelWave);
        }

        public static void LogCompleteLevel1()
        {
            LogFirebase(AnalyticsEvent.CompleteLevel1);
            LogAppMetrica(AnalyticsEvent.CompleteLevel1);
            LogGameAnalyticsProgression(GaProgressionStatus.Complete, 1, GaWholeLevelWave);
        }

        /// <summary> earn_virtual_currency: virtual_currency_name, value, amount, source </summary>
        public static void LogEarnVirtualCurrency(string virtualCurrencyName, double value, string source)
        {
            LogVirtualCurrency(AnalyticsEvent.EarnVirtualCurrency, virtualCurrencyName, value, source, null, true);
        }

        /// <summary> earn_virtual_currency — overload string, <paramref name="value"/> phải parse được thành số (invariant culture). </summary>
        public static void LogEarnVirtualCurrency(string virtualCurrencyName, string value, string source)
        {
            LogVirtualCurrency(AnalyticsEvent.EarnVirtualCurrency, virtualCurrencyName,
                ParseVirtualCurrencyValue(AnalyticsEvent.EarnVirtualCurrency, value), source, null, true);
        }

        /// <summary>
        /// spend_virtual_currency: virtual_currency_name, amount, source, item_name.
        /// Số lượng đi qua <c>amount</c> chứ không phải <c>value</c>: event này đang được đánh dấu key event trong GA4,
        /// mà key event có <c>value</c> thì bắt buộc phải kèm <c>currency</c> (ISO 4217) — thiếu thì GA4 drop <c>value</c>
        /// và gắn <c>firebase_error=19</c>. Tiền ảo không phải doanh thu nên không thể đặt <c>currency</c>.
        /// Nhớ đăng ký <c>amount</c> làm custom metric trong GA4 để dùng được trong report.
        /// </summary>
        public static void LogSpendVirtualCurrency(string virtualCurrencyName, double value, string source, string itemName = null)
        {
            LogVirtualCurrency(AnalyticsEvent.SpendVirtualCurrency, virtualCurrencyName, value, source, itemName, false);
        }

        /// <summary> spend_virtual_currency — overload string, <paramref name="value"/> phải parse được thành số (invariant culture). </summary>
        public static void LogSpendVirtualCurrency(string virtualCurrencyName, string value, string source, string itemName = null)
        {
            LogVirtualCurrency(AnalyticsEvent.SpendVirtualCurrency, virtualCurrencyName,
                ParseVirtualCurrencyValue(AnalyticsEvent.SpendVirtualCurrency, value), source, itemName, false);
        }

        /// <summary>
        /// <c>value</c> là reserved param của GA4 và bắt buộc kiểu số — gửi dạng string sẽ bị Firebase drop kèm
        /// <c>firebase_error</c> / <c>error_value=value</c>, nên Firebase nhận <c>Parameter</c> số thật thay vì đi qua
        /// <see cref="LogFirebaseParams"/> (vốn ép mọi param về string). <c>amount</c> luôn được gửi để hai event dùng
        /// chung một metric; <paramref name="sendValueParam"/> quyết định có gửi kèm <c>value</c> chuẩn GA4 hay không.
        /// </summary>
        private static void LogVirtualCurrency(string eventName, string virtualCurrencyName, double? value, string source,
            string itemName, bool sendValueParam)
        {
            string currencyName = virtualCurrencyName ?? "";
            string sourceName = source ?? "";

#if FIREBASE_DEPENDENCIES_INSTALLED
            var fbParams = new List<Parameter>
            {
                new Parameter(AnalyticsEvent.ParamVirtualCurrencyName, currencyName),
                new Parameter(AnalyticsEvent.ParamSource, sourceName)
            };
            if (value.HasValue)
            {
                fbParams.Add(new Parameter(AnalyticsEvent.ParamAmount, value.Value));
                if (sendValueParam) fbParams.Add(new Parameter(AnalyticsEvent.ParamValue, value.Value));
            }
            if (!string.IsNullOrEmpty(itemName)) fbParams.Add(new Parameter(AnalyticsEvent.ParamItemName, itemName));
            FirebaseUtils.LogEvent(eventName, fbParams.ToArray());
#endif

            var p = new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamVirtualCurrencyName] = currencyName,
                [AnalyticsEvent.ParamSource] = sourceName
            };
            if (value.HasValue)
            {
                string amountText = value.Value.ToString(CultureInfo.InvariantCulture);
                p[AnalyticsEvent.ParamAmount] = amountText;
                if (sendValueParam) p[AnalyticsEvent.ParamValue] = amountText;
            }
            if (!string.IsNullOrEmpty(itemName)) p[AnalyticsEvent.ParamItemName] = itemName;
            LogAppMetrica(eventName, p);
        }

        /// <summary>Bỏ hẳn param <c>value</c> khi không parse được, thay vì gửi chuỗi rác cho Firebase.</summary>
        private static double? ParseVirtualCurrencyValue(string eventName, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            Debug.LogWarning($"[GameUpAnalytics] {eventName}: value '{value}' không phải số hợp lệ, bỏ qua param 'value'.");
            return null;
        }

        // ---------- Firebase: Loading ----------

        /// <summary> start_loading - khi bắt đầu loading </summary>
        public static void LogStartLoading()
        {
            LogFirebase(AnalyticsEvent.StartLoading);
            LogAppMetrica(AnalyticsEvent.StartLoading);
        }

        /// <summary> complete_loading - khi hoàn thành loading, vào màn hình home </summary>
        public static void LogCompleteLoading()
        {
            LogFirebase(AnalyticsEvent.CompleteLoading);
            LogAppMetrica(AnalyticsEvent.CompleteLoading);
        }

        // ---------- Level (Firebase + AppsFlyer af_level_achieved - chung mục đích) ----------

        /// <summary> level_start: level (từ 1), index (lần bắt đầu thứ bao nhiêu); GA progression: level → <c>w0</c>. </summary>
        public static void LogLevelStart(int level, int index)
        {
            var p = new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamLevel] = level.ToString(),
                [AnalyticsEvent.ParamIndex] = index.ToString()
            };
            LogFirebaseParams(AnalyticsEvent.LevelStart, p);
            LogAppMetrica(AppMetricaEvent.LevelStart, p);
            AppMetricaUtils.SendEventsBuffer();
            LogGameAnalyticsProgression(GaProgressionStatus.Start, level, GaWholeLevelWave, stringFields: p);
        }

        /// <summary> level_fail: level, index, time; GA progression: level → <c>w0</c>. </summary>
        public static void LogLevelFail(int level, int index, float timeSeconds)
        {
            var p = new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamLevel] = level.ToString(),
                [AnalyticsEvent.ParamIndex] = index.ToString(),
                [AnalyticsEvent.ParamTime] = timeSeconds.ToString("F0")
            };
            LogFirebaseParams(AnalyticsEvent.LevelFail, p);
            LogAppMetrica(AnalyticsEvent.LevelFail, p);
            LogGameAnalyticsProgression(GaProgressionStatus.Fail, level, GaWholeLevelWave, stringFields: p);
        }

        /// <summary> level_complete (Firebase) + af_level_achieved (AppsFlyer): level, index, time; optional af_score; GA progression: level → <c>w0</c>. </summary>
        public static void LogLevelComplete(int level, int index, float timeSeconds, int? score = null)
        {
            var fb = new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamLevel] = level.ToString(),
                [AnalyticsEvent.ParamIndex] = index.ToString(),
                [AnalyticsEvent.ParamTime] = timeSeconds.ToString("F0")
            };
            LogFirebaseParams(AnalyticsEvent.LevelComplete, fb);
            LogAppMetrica(AnalyticsEvent.LevelComplete, fb);
            LogAppMetrica(AppMetricaEvent.LevelFinish, fb);
            AppMetricaUtils.SendEventsBuffer();

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

        /// <summary> button_click: source (tên button, bao gồm vị trí) </summary>
        public static void LogButtonClick(string source)
        {
            LogFirebase(AnalyticsEvent.ButtonClick, AnalyticsEvent.ParamSource, source ?? "");
            LogAppMetrica(AnalyticsEvent.ButtonClick, AnalyticsEvent.ParamSource, source ?? "");
        }

        // ---------- Firebase: Wave ----------

        /// <summary> wave_start: level, wave; GA progression: level → <c>w</c>{wave}. </summary>
        public static void LogWaveStart(int level, int wave)
        {
            var p = new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamLevel] = level.ToString(),
                [AnalyticsEvent.ParamWave] = wave.ToString()
            };
            LogFirebaseParams(AnalyticsEvent.WaveStart, p);
            LogAppMetrica(AnalyticsEvent.WaveStart, p);
            LogGameAnalyticsProgression(GaProgressionStatus.Start, level, wave, stringFields: p);
        }

        /// <summary> wave_fail: level, wave; GA progression: level → <c>w</c>{wave}. </summary>
        public static void LogWaveFail(int level, int wave)
        {
            var p = new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamLevel] = level.ToString(),
                [AnalyticsEvent.ParamWave] = wave.ToString()
            };
            LogFirebaseParams(AnalyticsEvent.WaveFail, p);
            LogAppMetrica(AnalyticsEvent.WaveFail, p);
            LogGameAnalyticsProgression(GaProgressionStatus.Fail, level, wave, stringFields: p);
        }

        /// <summary> wave_complete: level, wave; GA progression: level → <c>w</c>{wave}. </summary>
        public static void LogWaveComplete(int level, int wave)
        {
            var p = new Dictionary<string, string>
            {
                [AnalyticsEvent.ParamLevel] = level.ToString(),
                [AnalyticsEvent.ParamWave] = wave.ToString()
            };
            LogFirebaseParams(AnalyticsEvent.WaveComplete, p);
            LogAppMetrica(AnalyticsEvent.WaveComplete, p);
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

        /// <summary> af_purchase; <paramref name="level"/> — level đang chơi khi mua (Firebase/AppsFlyer/Facebook params). </summary>
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
            AppMetricaUtils.LogAfPurchaseEvent(afParams);
            AppMetricaUtils.LogPurchaseRevenue(normalizedCurrency, quantity, contentId, purchasePrice, orderId, level);

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
        /// Set AppsFlyer Customer User ID (CUID) để khớp dữ liệu ROI360.
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
            AppMetricaUtils.LogAdRevenue(data);
            Debug.Log($"[GameUpAnalytics] Logged Ad Revenue: {revenue} {currency}, network: {adNetwork}");
        }
    }
}
