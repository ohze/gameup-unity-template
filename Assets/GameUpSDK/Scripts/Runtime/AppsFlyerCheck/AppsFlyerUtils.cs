using UnityEngine;
using System.Collections.Generic;
using GameUp.Core;
#if APPSFLYER_DEPENDENCIES_INSTALLED
using AppsFlyerSDK;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// Gá»i event / ad revenue AppsFlyer. SDK Ä‘Æ°á»£c khá»Ÿi táº¡o bá»Ÿi AppsFlyerObject (AppsFlyerObjectScript) â€” devKey vÃ  appID cáº¥u hÃ¬nh trÃªn object Ä‘Ã³.
    /// </summary>
    public class AppsFlyerUtils : MonoSingleton<AppsFlyerUtils>
#if APPSFLYER_DEPENDENCIES_INSTALLED
        , IAppsFlyerPurchaseValidation, IAppsFlyerPurchaseRevenueDataSource, IAppsFlyerPurchaseRevenueDataSourceStoreKit2
#endif
    {
#if APPSFLYER_DEPENDENCIES_INSTALLED
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
            Debug.Log("[GameUpSDK] AppsFlyer Purchase Connector initialized for ROI360 (iOS).");
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
        /// Gá»­i ad revenue lÃªn AppsFlyer báº±ng AFAdRevenueData.
        /// </summary>
        public static void LogAdRevenue(AFAdRevenueData adRevenueData, Dictionary<string, string> additionalParameters = null)
        {
            TryInitPurchaseConnector();
            if (adRevenueData == null) return;
            AppsFlyer.logAdRevenue(adRevenueData, additionalParameters);
        }

        /// <summary>
        /// Gá»­i ad revenue lÃªn AppsFlyer. DÃ¹ng enum MediationNetwork cá»§a SDK (GoogleAdMob, IronSource, ApplovinMax, ...).
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
            Debug.LogError("[GameUpSDK] AppsFlyer purchase validation error: " + error);
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
