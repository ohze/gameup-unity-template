using System.Collections.Generic;
using GameUp.Core;
using UnityEngine;
#if ADJUST_DEPENDENCIES_INSTALLED && GAMEUP_MMP_ADJUST
using AdjustSdk;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// Adjust MMP — vai trò tương đương <see cref="AppsFlyerUtils"/>: init SDK từ <see cref="GameUpSdkConfig.adjust"/>,
    /// gửi event chuyển đổi, ad revenue và purchase. Không cần đặt prefab Adjust vào scene: SDK được init lúc load
    /// scene đầu. Nếu scene đã có prefab <c>Adjust</c> tự init (Start Manually = false) thì để prefab lo, không init lần hai.
    /// Chỉ hoạt động khi MMP đang chọn là Adjust (define GAMEUP_MMP_ADJUST); ngược lại mọi hàm là no-op.
    /// </summary>
    public static class AdjustAnalyticsUtils
    {
        private const string LogTag = "GameUp";
        private const string CustomerUserIdKey = "customer_user_id";

        /// <summary>SDK đã init (bởi GameUp hoặc bởi prefab Adjust). Luôn false trong Editor — Adjust không chạy trên Editor.</summary>
        public static bool IsInitialized { get; private set; }

#if ADJUST_DEPENDENCIES_INSTALLED && GAMEUP_MMP_ADJUST
        private static readonly HashSet<string> _missingTokenWarned = new HashSet<string>();

        /// <summary>User id được set trước khi SDK init xong — áp dụng ngay khi init.</summary>
        private static string _pendingCustomerUserId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeOnLoad()
        {
#if !UNITY_EDITOR
            Initialize();
#endif
        }

        /// <summary>Init Adjust bằng cấu hình trong GameUpSdkConfig. Gọi nhiều lần an toàn.</summary>
        public static void Initialize()
        {
            if (IsInitialized) return;

            var prefab = Object.FindFirstObjectByType<Adjust>(FindObjectsInactive.Include);
            if (prefab != null && !prefab.startManually)
            {
                GULogger.Warning(LogTag, "AdjustAnalyticsUtils: scene có prefab Adjust tự init — bỏ qua cấu hình GameUpSdkConfig.adjust.");
                MarkInitialized();
                return;
            }

            var settings = GameUpSdkConfig.Instance?.adjust;
            if (settings == null) return;

            bool isIOS = Application.platform == RuntimePlatform.IPhonePlayer;
            string appToken = settings.GetAppToken(isIOS);
            if (string.IsNullOrWhiteSpace(appToken))
            {
                GULogger.Warning(LogTag,
                    $"AdjustAnalyticsUtils: chưa có App Token {(isIOS ? "iOS" : "Android")} (GameUp → SDK → Setup → Adjust) — Adjust không được init.");
                return;
            }

            var config = new AdjustConfig(appToken.Trim(),
                settings.sandbox ? AdjustEnvironment.Sandbox : AdjustEnvironment.Production)
            {
                LogLevel = settings.verboseLog ? AdjustLogLevel.Verbose : AdjustLogLevel.Warn,
                IsSendingInBackgroundEnabled = settings.sendInBackground,
                IsCostDataInAttributionEnabled = settings.costDataInAttribution
            };
            if (settings.attConsentWaitingInterval > 0)
                config.AttConsentWaitingInterval = settings.attConsentWaitingInterval;

            Adjust.InitSdk(config);
            GULogger.Log(LogTag, $"Adjust initialized ({(settings.sandbox ? "Sandbox" : "Production")}).");
            MarkInitialized();
        }

        private static void MarkInitialized()
        {
            IsInitialized = true;
            if (_pendingCustomerUserId == null) return;
            Adjust.AddGlobalCallbackParameter(CustomerUserIdKey, _pendingCustomerUserId);
            _pendingCustomerUserId = null;
        }

        /// <summary>Gửi event theo tên GameUp; token lấy từ bảng Event Tokens. <paramref name="eventValues"/> đi kèm dạng callback parameter.</summary>
        public static void LogEvent(string eventName, Dictionary<string, string> eventValues = null)
        {
            var adjustEvent = CreateEvent(eventName, eventValues);
            if (adjustEvent != null) Adjust.TrackEvent(adjustEvent);
        }

        /// <summary>Event doanh thu IAP. <paramref name="orderId"/> dùng làm DeduplicationId để không đếm trùng.</summary>
        public static void LogPurchase(string eventName, double revenue, string currency, string orderId, string productId,
            Dictionary<string, string> eventValues = null)
        {
            var adjustEvent = CreateEvent(eventName, eventValues);
            if (adjustEvent == null) return;

            // Purchase không có giá (vd thiếu localized price) vẫn gửi event, nhưng không gắn doanh thu 0.
            if (revenue > 0d) adjustEvent.SetRevenue(revenue, string.IsNullOrWhiteSpace(currency) ? "USD" : currency);
            if (!string.IsNullOrEmpty(orderId))
            {
                adjustEvent.DeduplicationId = orderId;
                adjustEvent.TransactionId = orderId;
            }
            if (!string.IsNullOrEmpty(productId)) adjustEvent.ProductId = productId;
            Adjust.TrackEvent(adjustEvent);
        }

        /// <summary>Ad revenue; source lấy theo mạng đã phục vụ impression (một project có thể chạy nhiều mediation cùng lúc).</summary>
        public static void LogAdRevenue(AdImpressionData data)
        {
            if (!IsInitialized || data == null || !data.Revenue.HasValue) return;

            var adRevenue = new AdjustAdRevenue(GetAdRevenueSource(data));
            adRevenue.SetRevenue(data.Revenue.Value, data.ResolvedCurrency);
            adRevenue.AdImpressionsCount = 1;
            if (!string.IsNullOrEmpty(data.AdNetwork)) adRevenue.AdRevenueNetwork = data.AdNetwork;
            if (!string.IsNullOrEmpty(data.AdUnit)) adRevenue.AdRevenueUnit = data.AdUnit;
            string placement = data.InstanceName ?? data.AdFormat;
            if (!string.IsNullOrEmpty(placement)) adRevenue.AdRevenuePlacement = placement;
            Adjust.TrackAdRevenue(adRevenue);
        }

        /// <summary>Gắn user id vào mọi request sau đó (global callback parameter) — tương đương Customer User ID của AppsFlyer.</summary>
        public static void SetCustomerUserId(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;
            if (!IsInitialized)
            {
                _pendingCustomerUserId = userId;
                return;
            }
            Adjust.AddGlobalCallbackParameter(CustomerUserIdKey, userId);
        }

        private static AdjustEvent CreateEvent(string eventName, Dictionary<string, string> eventValues)
        {
            if (!IsInitialized || string.IsNullOrEmpty(eventName)) return null;

            var settings = GameUpSdkConfig.Instance?.adjust;
            if (settings == null || !settings.TryGetEventToken(eventName, out var token))
            {
                if (_missingTokenWarned.Add(eventName))
                    GULogger.Warning(LogTag, $"AdjustAnalyticsUtils: event '{eventName}' chưa có token (GameUp → SDK → Setup → Adjust) — bỏ qua.");
                return null;
            }

            var adjustEvent = new AdjustEvent(token);
            if (eventValues != null)
            {
                foreach (var pair in eventValues)
                {
                    if (!string.IsNullOrEmpty(pair.Key) && pair.Value != null)
                        adjustEvent.AddCallbackParameter(pair.Key, pair.Value);
                }
            }
            return adjustEvent;
        }

        /// <summary>Source của Adjust = mediation đã phục vụ impression (không phải ad network con, vd AppLovin chạy trong LevelPlay).</summary>
        private static string GetAdRevenueSource(AdImpressionData data)
        {
            return data.Mediation switch
            {
                MediationProvider.Admob => "admob_sdk",
                MediationProvider.Max => "applovin_max_sdk",
                MediationProvider.IronSource => "ironsource_sdk",
                _ => "publisher_sdk",
            };
        }
#else
        public static void Initialize() { }

        public static void LogEvent(string eventName, Dictionary<string, string> eventValues = null) { }

        public static void LogPurchase(string eventName, double revenue, string currency, string orderId, string productId,
            Dictionary<string, string> eventValues = null) { }

        public static void LogAdRevenue(AdImpressionData data) { }

        public static void SetCustomerUserId(string userId) { }
#endif
    }
}
