using UnityEngine;
using System.Collections.Generic;
using GameUp.Core;
#if APPSFLYER_DEPENDENCIES_INSTALLED
using AppsFlyerSDK;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// Gửi event / ad revenue AppsFlyer. SDK được init bởi <c>AppsFlyerObjectScript</c> — devKey / appID lấy từ GameUpSdkConfig.
    /// </summary>
    public class AppsFlyerUtils : MonoSingleton<AppsFlyerUtils>
#if APPSFLYER_DEPENDENCIES_INSTALLED && !GAMEUP_MMP_ADJUST
        , IAppsFlyerPurchaseValidation, IAppsFlyerPurchaseRevenueDataSource, IAppsFlyerPurchaseRevenueDataSourceStoreKit2
#endif
    {
        [Tooltip("Để trống = dùng asset GameUpSdkConfig chung của project (Resources/GameUpSDK/GameUpSdkConfig).")]
        [SerializeField] private GameUpSdkConfig configOverride;

        /// <summary>
        /// Đẩy devKey / appID / isDebug từ GameUpSdkConfig sang <c>AppsFlyerObjectScript</c> (component của
        /// AppsFlyer SDK) trước khi <c>Start()</c> của nó chạy — mọi Awake đều chạy trước mọi Start,
        /// nên SDK init bằng giá trị trong asset mà không cần sửa prefab của package.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            // Bản trùng (SDK prefab đặt ở nhiều scene) đang bị Destroy — không được tạo/tắt AppsFlyerObject lần nữa.
            if (!ReferenceEquals(Instance, this)) return;
            ApplyConfigToAppsFlyerObject();
        }

        private void ApplyConfigToAppsFlyerObject()
        {
#if APPSFLYER_DEPENDENCIES_INSTALLED
            // Project cũ còn AppsFlyerObject trong SDK prefab / scene thì dùng lại; không có thì tạo ở dưới.
            var afObject = GetComponentInChildren<AppsFlyerObjectScript>(true)
                           ?? FindFirstObjectByType<AppsFlyerObjectScript>(FindObjectsInactive.Include);
#if GAMEUP_MMP_ADJUST
            // MMP đang chọn là Adjust: tắt component trước Start() để AppsFlyer không init/startSDK,
            // tránh 2 MMP cùng đếm install (chỉ xảy ra với project cũ còn AppsFlyerObject trong prefab/scene).
            if (afObject != null)
            {
                afObject.enabled = false;
                GULogger.Log("GameUp", "AppsFlyerUtils: MMP = Adjust — đã tắt AppsFlyerObject.");
            }
#else
            var settings = GameUpSdkConfig.Resolve(configOverride)?.appsFlyer;
            if (settings == null) return;

            // SDK.prefab không còn nhúng AppsFlyerObject (để project chọn Adjust không dính missing script),
            // nên tự tạo khi scene chưa có. Start() của component mới vẫn chạy sau Awake này → nhận đủ config.
            if (afObject == null) afObject = CreateAppsFlyerObject();

            // Chuỗi rỗng trong asset không ghi đè giá trị đang có trên prefab (tránh xoá key khi chưa migrate).
            if (!string.IsNullOrWhiteSpace(settings.devKey)) afObject.devKey = settings.devKey;
            if (!string.IsNullOrWhiteSpace(settings.appIdIOS)) afObject.appID = settings.appIdIOS;
            afObject.isDebug = settings.isDebug;
            afObject.getConversionData = settings.getConversionData;

            if (string.IsNullOrWhiteSpace(afObject.devKey))
                GULogger.Warning("GameUp", "AppsFlyerUtils: chưa có Dev Key (GameUp → SDK → Setup → AppsFlyer) — AppsFlyer sẽ không ghi nhận install.");
#endif
#endif
        }

#if APPSFLYER_DEPENDENCIES_INSTALLED && !GAMEUP_MMP_ADJUST
        private AppsFlyerObjectScript CreateAppsFlyerObject()
        {
            // Giữ đúng tên "AppsFlyerObject" như prefab gốc của AppsFlyer; làm con để sống cùng SDK root.
            var go = new GameObject("AppsFlyerObject");
            go.transform.SetParent(transform, false);
            return go.AddComponent<AppsFlyerObjectScript>();
        }
#endif

#if APPSFLYER_DEPENDENCIES_INSTALLED && !GAMEUP_MMP_ADJUST
        private static bool _purchaseConnectorInitialized;
        private static bool _purchaseConnectorInitializing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceForPurchaseConnector()
        {
#if !UNITY_EDITOR
            _ = Instance;
#endif
        }

        private void Start()
        {
            TryInitPurchaseConnector();
        }

        private static void TryInitPurchaseConnector()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (_purchaseConnectorInitialized || _purchaseConnectorInitializing) return;

            // AppsFlyerObjectScript starts the core SDK. Delaying by one frame keeps initialization order safe.
            _purchaseConnectorInitializing = true;
            Instance.StartCoroutine(InitPurchaseConnectorNextFrame());
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        private static System.Collections.IEnumerator InitPurchaseConnectorNextFrame()
        {
            if (_purchaseConnectorInitialized)
            {
                _purchaseConnectorInitializing = false;
                yield break;
            }
            yield return null;
            if (_purchaseConnectorInitialized)
            {
                _purchaseConnectorInitializing = false;
                yield break;
            }

            AppsFlyerPurchaseConnector.init(Instance, Store.GOOGLE);
            AppsFlyerPurchaseConnector.setStoreKitVersion(StoreKitVersion.SK2);
            AppsFlyerPurchaseConnector.setAutoLogPurchaseRevenue(
                AppsFlyerAutoLogPurchaseRevenueOptions.AppsFlyerAutoLogPurchaseRevenueOptionsAutoRenewableSubscriptions,
                AppsFlyerAutoLogPurchaseRevenueOptions.AppsFlyerAutoLogPurchaseRevenueOptionsInAppPurchases);
            AppsFlyerPurchaseConnector.setPurchaseRevenueValidationListeners(true);
            AppsFlyerPurchaseConnector.setPurchaseRevenueDataSource(Instance);
            AppsFlyerPurchaseConnector.setPurchaseRevenueDataSourceStoreKit2(Instance);
            AppsFlyerPurchaseConnector.startObservingTransactions();

            _purchaseConnectorInitialized = true;
            _purchaseConnectorInitializing = false;
            GULogger.Log("GameUp", "AppsFlyer Purchase Connector initialized for ROI360 (iOS).");
        }
#endif

        /// <summary>
        /// ROI360 Purchase Connector auto-logs IAP on iOS, so skip manual af_purchase revenue events.
        /// </summary>
        public static bool ShouldSkipManualPurchaseRevenueEvent()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return _purchaseConnectorInitialized;
#else
            return false;
#endif
        }

        /// <summary>
        /// Set AppsFlyer Customer User ID (CUID) for ROI360 matching.
        /// </summary>
        public static void SetCustomerUserId(string userId)
        {
            TryInitPurchaseConnector();
            if (string.IsNullOrEmpty(userId)) return;
            AppsFlyer.setCustomerUserId(userId);
        }

        /// <summary>
        /// Gửi ad revenue lên AppsFlyer bằng AFAdRevenueData.
        /// </summary>
        public static void LogAdRevenue(AFAdRevenueData adRevenueData, Dictionary<string, string> additionalParameters = null)
        {
            TryInitPurchaseConnector();
            if (adRevenueData == null) return;
            AppsFlyer.logAdRevenue(adRevenueData, additionalParameters);
        }

        /// <summary>
        /// Gửi ad revenue lên AppsFlyer. Dùng enum MediationNetwork của SDK (GoogleAdMob, IronSource, ApplovinMax, ...).
        /// </summary>
        public static void LogAdRevenue(string monetizationNetwork, MediationNetwork mediationNetwork,
            double eventRevenue, string revenueCurrency, Dictionary<string, string> additionalParameters = null)
        {
            var adRevenueData = new AFAdRevenueData(monetizationNetwork, mediationNetwork, revenueCurrency, eventRevenue);
            LogAdRevenue(adRevenueData, additionalParameters);
        }

        public static void LogEvents(string eventName, Dictionary<string, string> eventValues = null)
        {
            TryInitPurchaseConnector();
            AppsFlyer.sendEvent(eventName, eventValues);
        }

        public void didReceivePurchaseRevenueValidationInfo(string validationInfo)
        {
            AppsFlyer.AFLog("didReceivePurchaseRevenueValidationInfo", validationInfo);
        }

        public void didReceivePurchaseRevenueError(string error)
        {
            AppsFlyer.AFLog("didReceivePurchaseRevenueError", error);
            GULogger.Error("GameUp", $"AppsFlyer purchase validation error: {error}");
        }

        public Dictionary<string, object> PurchaseRevenueAdditionalParametersForProducts(HashSet<object> products, HashSet<object> transactions)
        {
            return BuildPurchaseConnectorAdditionalParameters(products, transactions, "sk1");
        }

        public Dictionary<string, object> PurchaseRevenueAdditionalParametersStoreKit2ForProducts(HashSet<object> products, HashSet<object> transactions)
        {
            return BuildPurchaseConnectorAdditionalParameters(products, transactions, "sk2");
        }

        private static Dictionary<string, object> BuildPurchaseConnectorAdditionalParameters(HashSet<object> products, HashSet<object> transactions, string storeKitVersion)
        {
            return new Dictionary<string, object>
            {
                ["storekit_version"] = storeKitVersion,
                ["products_count"] = products != null ? products.Count : 0,
                ["transactions_count"] = transactions != null ? transactions.Count : 0
            };
        }
#else
        public static bool ShouldSkipManualPurchaseRevenueEvent() { return false; }

        public static void SetCustomerUserId(string userId) { }

        public static void LogAdRevenue(object adRevenueData, Dictionary<string, string> additionalParameters = null) { }

        public static void LogAdRevenue(string monetizationNetwork, int mediationNetwork,
            double eventRevenue, string revenueCurrency, Dictionary<string, string> additionalParameters = null) { }

        public static void LogEvents(string eventName, Dictionary<string, string> eventValues = null) { }
#endif
    }
}
