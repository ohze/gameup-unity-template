using GameUp.Core;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
#if APPMETRICA_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
using Io.AppMetrica;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// Gửi custom events / ad revenue / IAP revenue lên AppMetrica.
    /// Bật/tắt qua <see cref="AppMetricaActivator.EnableEventLogging"/> (GameUp SDK Setup).
    /// Debug xác nhận gửi: bật <see cref="AppMetricaActivator.IsUtilsDebugLogEnabled"/> (SDK debug logs).
    /// </summary>
    public static class AppMetricaUtils
    {
        private const string LogTag = "[GameUp.SDK][AppMetrica]";

        public static bool IsEventLoggingEnabled =>
#if APPMETRICA_DEPENDENCIES_INSTALLED
            AppMetricaActivator.EnableEventLogging;
#else
            false;
#endif

        public static void LogEvent(string eventName, Dictionary<string, string> parameters = null)
        {
#if APPMETRICA_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
            if (!TryPrepareSend("LogEvent", eventName, out var skipReason))
            {
                DebugLogSkip("LogEvent", eventName, skipReason);
                return;
            }

            var json = DictionaryToJson(parameters);
            if (string.IsNullOrEmpty(json))
                AppMetrica.ReportEvent(eventName);
            else
                AppMetrica.ReportEvent(eventName, json);

            DebugLogSent("ReportEvent", eventName, json);
#elif APPMETRICA_DEPENDENCIES_INSTALLED
            DebugLogEditorStub("LogEvent", eventName, parameters);
#else
            DebugLogUnavailable("LogEvent", eventName);
#endif
        }

        public static void LogAdRevenue(AdImpressionData data)
        {
#if APPMETRICA_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
            if (!IsEventLoggingEnabled)
            {
                DebugLogSkip("LogAdRevenue", AppMetricaEvent.AfAdRevenue, "enableEventLogging=false");
                return;
            }

            if (data == null || !data.Revenue.HasValue)
            {
                DebugLogSkip("LogAdRevenue", AppMetricaEvent.AfAdRevenue, "missing revenue");
                return;
            }

            if (!AppMetrica.IsActivated())
            {
                DebugLogSkip("LogAdRevenue", AppMetricaEvent.AfAdRevenue, "AppMetrica not activated");
                return;
            }

            var adRevenue = new AdRevenue(data.Revenue.Value, "USD")
            {
                AdNetwork = data.AdNetwork,
                AdUnitId = data.AdUnit,
                AdPlacementName = data.InstanceName,
                AdType = MapAdType(data.AdFormat)
            };
            AppMetrica.ReportAdRevenue(adRevenue);
            DebugLogSent("ReportAdRevenue", $"{data.Revenue.Value} USD", $"network={data.AdNetwork}");

            var eventParams = BuildAfAdRevenueParams(data);
            if (eventParams != null && eventParams.Count > 0)
                LogEvent(AppMetricaEvent.AfAdRevenue, eventParams);
#elif APPMETRICA_DEPENDENCIES_INSTALLED
            DebugLogEditorStub("LogAdRevenue", AppMetricaEvent.AfAdRevenue, null);
#else
            DebugLogUnavailable("LogAdRevenue", AppMetricaEvent.AfAdRevenue);
#endif
        }

        public static void LogPurchaseRevenue(
            string currencyCode,
            int quantity,
            string contentId,
            string purchasePrice,
            string orderId,
            int? level = null)
        {
#if APPMETRICA_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
            if (!IsEventLoggingEnabled)
            {
                DebugLogSkip("LogPurchaseRevenue", AppMetricaEvent.AfPurchase, "enableEventLogging=false");
                return;
            }

            if (!AppMetrica.IsActivated())
            {
                DebugLogSkip("LogPurchaseRevenue", AppMetricaEvent.AfPurchase, "AppMetrica not activated");
                return;
            }

            string currency = string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode.Trim().ToUpperInvariant();
            if (!double.TryParse(
                    purchasePrice,
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out var amount))
                amount = 0d;

            long priceMicros = (long)(amount * 1_000_000d);
            var revenue = new Revenue(priceMicros, currency)
            {
                ProductID = contentId,
                Quantity = quantity > 0 ? quantity : 1
            };

            var payload = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(orderId)) payload["order_id"] = orderId;
            if (level.HasValue) payload[AnalyticsEvent.ParamLevel] = level.Value.ToString();
            var payloadJson = DictionaryToJson(payload);
            if (!string.IsNullOrEmpty(payloadJson)) revenue.Payload = payloadJson;

            AppMetrica.ReportRevenue(revenue);
            DebugLogSent("ReportRevenue", $"{amount} {currency}", $"product={contentId}, qty={quantity}");
#elif APPMETRICA_DEPENDENCIES_INSTALLED
            DebugLogEditorStub("LogPurchaseRevenue", AppMetricaEvent.AfPurchase, null);
#else
            DebugLogUnavailable("LogPurchaseRevenue", AppMetricaEvent.AfPurchase);
#endif
        }

        public static void LogAfPurchaseEvent(Dictionary<string, string> afParams)
        {
            LogEvent(AppMetricaEvent.AfPurchase, afParams);
        }

        public static void SendEventsBuffer()
        {
#if APPMETRICA_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
            if (!IsEventLoggingEnabled)
            {
                DebugLogSkip("SendEventsBuffer", "buffer", "enableEventLogging=false");
                return;
            }

            if (!AppMetrica.IsActivated())
            {
                DebugLogSkip("SendEventsBuffer", "buffer", "AppMetrica not activated");
                return;
            }

            AppMetrica.SendEventsBuffer();
            DebugLogSent("SendEventsBuffer", "flush", null);
#elif APPMETRICA_DEPENDENCIES_INSTALLED
            DebugLogEditorStub("SendEventsBuffer", "buffer", null);
#else
            DebugLogUnavailable("SendEventsBuffer", "buffer");
#endif
        }

#if APPMETRICA_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
        private static bool TryPrepareSend(string method, string eventName, out string skipReason)
        {
            skipReason = null;
            if (!IsEventLoggingEnabled)
            {
                skipReason = "enableEventLogging=false";
                return false;
            }

            if (string.IsNullOrEmpty(eventName))
            {
                skipReason = "empty eventName";
                return false;
            }

            if (!AppMetrica.IsActivated())
            {
                skipReason = "AppMetrica not activated";
                return false;
            }

            return true;
        }

        private static Dictionary<string, string> BuildAfAdRevenueParams(AdImpressionData data)
        {
            if (data == null || !data.Revenue.HasValue) return null;
            var p = new Dictionary<string, string>
            {
                [AppMetricaEvent.ParamMonetizationNetwork] = data.AdNetwork ?? "",
                [AppMetricaEvent.ParamAfRevenue] = data.Revenue.Value.ToString(CultureInfo.InvariantCulture),
                [AppMetricaEvent.ParamAfCurrency] = "USD"
            };
            if (!string.IsNullOrEmpty(data.AdUnit)) p[AppMetricaEvent.ParamAdUnit] = data.AdUnit;
            if (!string.IsNullOrEmpty(data.AdFormat)) p["ad_format"] = data.AdFormat;
            if (!string.IsNullOrEmpty(data.InstanceName)) p["ad_placement"] = data.InstanceName;
            return p;
        }

        private static AdType? MapAdType(string adFormat)
        {
            if (string.IsNullOrEmpty(adFormat)) return null;
            var f = adFormat.Trim().ToLowerInvariant();
            if (f.Contains("banner")) return AdType.Banner;
            if (f.Contains("inter")) return AdType.Interstitial;
            if (f.Contains("reward")) return AdType.Rewarded;
            if (f.Contains("app_open") || f.Contains("appopen")) return AdType.AppOpen;
            if (f.Contains("native")) return AdType.Native;
            if (f.Contains("mrec")) return AdType.Mrec;
            return AdType.Other;
        }
#endif

        private static string DictionaryToJson(Dictionary<string, string> parameters)
        {
            if (parameters == null || parameters.Count == 0) return null;

            var sb = new StringBuilder("{");
            var first = true;
            foreach (var kv in parameters)
            {
                if (kv.Value == null) continue;
                if (!first) sb.Append(',');
                sb.Append('"').Append(EscapeJson(kv.Key)).Append("\":\"").Append(EscapeJson(kv.Value)).Append('"');
                first = false;
            }

            sb.Append('}');
            return sb.Length > 2 ? sb.ToString() : null;
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }

        private static bool ShouldPrintDebug()
        {
#if APPMETRICA_DEPENDENCIES_INSTALLED
            return AppMetricaActivator.IsUtilsDebugLogEnabled;
#else
            return false;
#endif
        }

        private static void DebugLogSent(string api, string eventName, string detail)
        {
            if (!ShouldPrintDebug()) return;
            string msg = string.IsNullOrEmpty(detail)
                ? $"{LogTag} Sent {api}: {eventName}"
                : $"{LogTag} Sent {api}: {eventName} | {detail}";
            GULogger.Log(msg);
        }

        private static void DebugLogSkip(string method, string eventName, string reason)
        {
            if (!ShouldPrintDebug()) return;
            GULogger.Warning("GameUp", $"{LogTag} Skip {method} ({eventName}): {reason}");
        }

#if APPMETRICA_DEPENDENCIES_INSTALLED
        private static void DebugLogEditorStub(string method, string eventName, Dictionary<string, string> parameters)
        {
            if (!ShouldPrintDebug()) return;
            string json = DictionaryToJson(parameters);
            string detail = string.IsNullOrEmpty(json) ? "no params" : json;
            GULogger.Log($"{LogTag} Editor stub {method}: {eventName} | {detail} (chỉ gửi lên server trên build Android/iOS)");
        }
#endif

        private static void DebugLogUnavailable(string method, string eventName)
        {
            if (!ShouldPrintDebug()) return;
            GULogger.Warning("GameUp", $"{LogTag} Skip {method} ({eventName}): APPMETRICA_DEPENDENCIES_INSTALLED không bật");
        }
    }
}
