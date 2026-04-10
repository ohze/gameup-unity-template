using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using GameUp.Core;
using GameUp.SDK;
using UnityEngine;
using UnityEngine.Purchasing;

namespace GameUp.IAP
{
    [Serializable]
    public sealed class IAPProductDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private ProductType type = ProductType.NonConsumable;
        [SerializeField] private string localPackCost = "0.99";

        public IAPProductDefinition(
            string productId,
            ProductType productType = ProductType.NonConsumable,
            string productLocalPackCost = "0.99")
        {
            id = productId;
            type = productType;
            localPackCost = productLocalPackCost;
        }

        public string Id => id;
        public ProductType Type => type;
        public string LocalPackCost => localPackCost;
    }

    public class MyIAPManager : MonoSingleton<MyIAPManager>
    {
        private const string Tag = "IAP";
        private const string EventIapInitialize = "iap_initialize";
        private const string EventIapPurchaseStart = "iap_purchase_start";
        private const string EventIapPurchaseFail = "iap_purchase_fail";

        [SerializeField] private bool testMode;
        [SerializeField] private bool enableAnalytics = true;

        private List<IAPProductDefinition> _products = new();
        private readonly Dictionary<string, Action<bool>> _purchaseCallbacks = new();
        private readonly HashSet<string> _configuredProductIds = new();

        private StoreController _storeController;
        private bool _isInitializing;
        private bool _isInitialized;
        private bool _eventsBound;

        public bool IsIAPInitialized => _isInitialized && _storeController != null;
        public IReadOnlyList<IAPProductDefinition> Products => _products;

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            UnbindStoreEvents();
        }

        public void Initialize(IEnumerable<IAPProductDefinition> externalProducts)
        {
            SetProducts(externalProducts);
            _ = InitializeAsync();
        }

        public Task<bool> InitializeAsync(IEnumerable<IAPProductDefinition> externalProducts)
        {
            SetProducts(externalProducts);
            return InitializeAsync();
        }

        public void SetProducts(IEnumerable<IAPProductDefinition> externalProducts)
        {
            if (_isInitializing)
            {
                GULogger.Warning(Tag, "SetProducts skipped because initialization is in progress.");
                return;
            }

            if (IsIAPInitialized)
            {
                GULogger.Warning(Tag, "SetProducts skipped because IAP is already initialized.");
                return;
            }

            _products.Clear();
            if (externalProducts == null)
            {
                return;
            }

            foreach (var definition in externalProducts)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (_products.Any(item => string.Equals(item.Id, definition.Id, StringComparison.Ordinal)))
                {
                    continue;
                }

                _products.Add(new IAPProductDefinition(definition.Id, definition.Type, definition.LocalPackCost));
            }
        }

        public void SetProducts(IEnumerable<string> productIds, ProductType defaultType = ProductType.NonConsumable)
        {
            if (productIds == null)
            {
                SetProducts((IEnumerable<IAPProductDefinition>)null);
                return;
            }

            var mappedProducts = productIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => new IAPProductDefinition(id, defaultType));
            SetProducts(mappedProducts);
        }

        public async Task<bool> InitializeAsync()
        {
            if (IsIAPInitialized)
            {
                return true;
            }

            if (_isInitializing)
            {
                GULogger.Warning(Tag, "Initialize skipped because initialization is already running.");
                LogIapInitialize(false, "initializing");
                return false;
            }

            _isInitializing = true;

            try
            {
                _storeController = UnityIAPServices.StoreController();
                if (_storeController == null)
                {
                    GULogger.Error(Tag, "Cannot get StoreController from UnityIAPServices.");
                    _isInitializing = false;
                    LogIapInitialize(false, "store_controller_null");
                    return false;
                }

                BindStoreEvents();
                await _storeController.Connect();
                FetchProducts();
                return true;
            }
            catch (Exception exception)
            {
                _isInitializing = false;
                _isInitialized = false;
                GULogger.Exception(exception, Tag);
                LogIapInitialize(false, exception.GetType().Name);
                return false;
            }
        }

        public void BuyProduct(string productId, Action<bool> onPurchaseComplete = null)
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                GULogger.Warning(Tag, "BuyProduct failed because productId is empty.");
                LogPurchaseFail(productId, "empty_product_id");
                onPurchaseComplete?.Invoke(false);
                return;
            }

            if (testMode)
            {
                LogPurchaseStart(productId);
                LogPurchaseSuccess(productId, "TEST", "0", "TEST_ORDER");
                onPurchaseComplete?.Invoke(true);
                return;
            }

            if (!IsIAPInitialized)
            {
                GULogger.Warning(Tag, $"BuyProduct failed because IAP is not initialized yet. productId={productId}");
                LogPurchaseFail(productId, "iap_not_initialized");
                onPurchaseComplete?.Invoke(false);
                return;
            }

            var product = FindProductById(productId);
            if (product == null || !product.availableToPurchase)
            {
                GULogger.Warning(Tag, $"BuyProduct failed because product is unavailable. productId={productId}");
                LogPurchaseFail(productId, "product_unavailable");
                onPurchaseComplete?.Invoke(false);
                return;
            }

            _purchaseCallbacks[productId] = onPurchaseComplete;
            LogPurchaseStart(productId);
            _storeController.PurchaseProduct(product);
        }

        public string GetLocalizedPrice(string productId, string defaultPrice)
        {
            var product = FindProductById(productId);
            if (product?.metadata == null)
            {
                return defaultPrice;
            }

            return string.IsNullOrWhiteSpace(product.metadata.localizedPriceString)
                ? defaultPrice
                : product.metadata.localizedPriceString;
        }

        public string GetLocalizedPrice(string productId)
        {
            var localPackCost = GetLocalPackCost(productId);
            return GetLocalizedPrice(productId, localPackCost);
        }

        public string GetMultipliedLocalizedPrice(string productId, string defaultPrice, int multiplier)
        {
            if (multiplier <= 0)
            {
                return defaultPrice;
            }

            var product = FindProductById(productId);
            if (product?.metadata == null)
            {
                return defaultPrice;
            }

            var localizedPrice = product.metadata.localizedPrice;
            var multipliedPrice = localizedPrice * multiplier;
            var formattedNumber = multipliedPrice.ToString("N0", CultureInfo.GetCultureInfo("de"));
            var currencySymbol = ExtractCurrencySymbol(product.metadata.localizedPriceString, out var symbolAtStart);

            if (string.IsNullOrWhiteSpace(currencySymbol))
            {
                return formattedNumber;
            }

            return symbolAtStart ? $"{currencySymbol}{formattedNumber}" : $"{formattedNumber}{currencySymbol}";
        }

        public bool TryGetSubscriptionInfo(string productId, out SubscriptionInfo subscriptionInfo)
        {
            subscriptionInfo = null;

            var product = FindProductById(productId);
            if (product == null || product.definition.type != ProductType.Subscription)
            {
                return false;
            }

            // IAP v5 no longer exposes the legacy SubscriptionManager flow.
            // Keep the method for API compatibility and return false until a
            // project-specific subscription parser is added.
            return false;
        }

        private void FetchProducts()
        {
            var catalogProvider = new CatalogProvider();
            _configuredProductIds.Clear();

            foreach (var definition in _products)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                if (_configuredProductIds.Contains(definition.Id))
                {
                    continue;
                }

                _configuredProductIds.Add(definition.Id);
                catalogProvider.AddProduct(definition.Id, definition.Type);
            }

            if (_configuredProductIds.Count == 0)
            {
                GULogger.Warning(Tag, "No IAP products configured. FetchProducts is skipped.");
                _isInitializing = false;
                return;
            }

            catalogProvider.FetchProducts(productList => _storeController.FetchProducts(productList));
            GULogger.Log(Tag, $"FetchProducts requested for {_configuredProductIds.Count} products.");
        }

        private Product FindProductById(string productId)
        {
            if (_storeController == null || string.IsNullOrWhiteSpace(productId))
            {
                return null;
            }

            var fetchedProducts = _storeController.GetProducts();
            if (fetchedProducts == null)
            {
                return null;
            }

            return fetchedProducts.FirstOrDefault(item =>
                string.Equals(item.definition.id, productId, StringComparison.Ordinal));
        }

        private string GetLocalPackCost(string productId)
        {
            var definition = _products.FirstOrDefault(item =>
                string.Equals(item.Id, productId, StringComparison.Ordinal));
            if (definition == null || string.IsNullOrWhiteSpace(definition.LocalPackCost))
            {
                return "0";
            }

            return definition.LocalPackCost;
        }

        private static string ExtractCurrencySymbol(string localizedPriceString, out bool symbolAtStart)
        {
            symbolAtStart = true;

            if (string.IsNullOrWhiteSpace(localizedPriceString))
            {
                return string.Empty;
            }

            var firstDigitIndex = -1;
            for (var index = 0; index < localizedPriceString.Length; index++)
            {
                if (char.IsDigit(localizedPriceString[index]))
                {
                    firstDigitIndex = index;
                    break;
                }
            }

            if (firstDigitIndex < 0)
            {
                return string.Empty;
            }

            symbolAtStart = firstDigitIndex > 0;
            if (symbolAtStart)
            {
                return localizedPriceString[..firstDigitIndex].Trim();
            }

            var lastDigitIndex = localizedPriceString.Length - 1;
            for (var index = localizedPriceString.Length - 1; index >= 0; index--)
            {
                if (char.IsDigit(localizedPriceString[index]))
                {
                    lastDigitIndex = index;
                    break;
                }
            }

            if (lastDigitIndex >= localizedPriceString.Length - 1)
            {
                return string.Empty;
            }

            return localizedPriceString[(lastDigitIndex + 1)..].Trim();
        }

        private void BindStoreEvents()
        {
            if (_eventsBound || _storeController == null)
            {
                return;
            }

            _storeController.OnStoreDisconnected += OnStoreDisconnected;
            _storeController.OnProductsFetched += OnProductsFetched;
            _storeController.OnProductsFetchFailed += OnProductsFetchFailed;
            _storeController.OnPurchasesFetched += OnPurchasesFetched;
            _storeController.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
            _storeController.OnPurchasePending += OnPurchasePending;
            _eventsBound = true;
        }

        private void UnbindStoreEvents()
        {
            if (!_eventsBound || _storeController == null)
            {
                return;
            }

            _storeController.OnStoreDisconnected -= OnStoreDisconnected;
            _storeController.OnProductsFetched -= OnProductsFetched;
            _storeController.OnProductsFetchFailed -= OnProductsFetchFailed;
            _storeController.OnPurchasesFetched -= OnPurchasesFetched;
            _storeController.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
            _storeController.OnPurchasePending -= OnPurchasePending;
            _eventsBound = false;
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
        {
            _isInitializing = false;
            _isInitialized = false;
            GULogger.Warning(Tag, $"Store disconnected: {failure}");
        }

        private void OnProductsFetched(List<Product> fetchedProducts)
        {
            _isInitializing = false;
            _isInitialized = true;
            var count = fetchedProducts?.Count ?? 0;
            GULogger.Log(Tag, $"Products fetched successfully. count={count}");
            LogIapInitialize(true, "products_fetched");
            _storeController.FetchPurchases();
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            _isInitializing = false;
            _isInitialized = false;
            GULogger.Error(Tag, $"Products fetch failed: {failure}");
            LogIapInitialize(false, "products_fetch_failed");
        }

        private void OnPurchasesFetched(Orders orders)
        {
            var totalOrders = orders == null
                ? 0
                : orders.ConfirmedOrders.Count + orders.PendingOrders.Count + orders.DeferredOrders.Count;
            GULogger.Log(Tag, $"Purchases fetched. orders={totalOrders}");
        }

        private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            GULogger.Warning(Tag, $"Fetch purchases failed: {failure}");
            LogPurchaseFail(string.Empty, "purchases_fetch_failed");
        }

        private void OnPurchasePending(PendingOrder order)
        {
            var productId = GetProductIdFromOrder(order);
            if (!string.IsNullOrWhiteSpace(productId) && _purchaseCallbacks.TryGetValue(productId, out var callback))
            {
                callback?.Invoke(true);
                _purchaseCallbacks.Remove(productId);
            }

            var product = FindProductById(productId);
            if (product != null)
            {
                var currencyCode = string.IsNullOrWhiteSpace(product.metadata.isoCurrencyCode)
                    ? "USD"
                    : product.metadata.isoCurrencyCode;
                var purchasePrice = product.metadata.localizedPrice.ToString(CultureInfo.InvariantCulture);
                LogPurchaseSuccess(productId, currencyCode, purchasePrice, string.Empty);
            }
            else
            {
                LogPurchaseSuccess(productId, "USD", "0", string.Empty);
            }

            _storeController.ConfirmPurchase(order);
            GULogger.Log(Tag, $"Purchase processed and confirmed. productId={productId}");
        }

        private static string GetProductIdFromOrder(PendingOrder order)
        {
            var item = order?.CartOrdered?.Items()?.FirstOrDefault();
            return item?.Product?.definition?.id;
        }

        private void LogIapInitialize(bool success, string status)
        {
            if (!enableAnalytics)
            {
                return;
            }

            GameUpAnalytics.LogFirebaseParams(EventIapInitialize, new Dictionary<string, string>
            {
                ["source"] = status ?? string.Empty,
                ["value"] = success ? "1" : "0"
            });
        }

        private void LogPurchaseStart(string productId)
        {
            if (!enableAnalytics)
            {
                return;
            }

            GameUpAnalytics.LogFirebaseParams(EventIapPurchaseStart, new Dictionary<string, string>
            {
                ["af_content_id"] = productId ?? string.Empty
            });
        }

        private void LogPurchaseFail(string productId, string reason)
        {
            if (!enableAnalytics)
            {
                return;
            }

            GameUpAnalytics.LogFirebaseParams(EventIapPurchaseFail, new Dictionary<string, string>
            {
                ["af_content_id"] = productId ?? string.Empty,
                ["source"] = reason ?? string.Empty
            });
        }

        private void LogPurchaseSuccess(string productId, string currencyCode, string purchasePrice, string orderId)
        {
            if (!enableAnalytics)
            {
                return;
            }

            GameUpAnalytics.LogPurchase(currencyCode, 1, productId, purchasePrice, orderId);
        }
    }
}