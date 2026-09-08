using System;
using GameUp.Core;
using UnityEngine.Purchasing.Security;
#if !UNITY_EDITOR
using UnityEngine;
using UnityEngine.Purchasing;
#endif

namespace GameUp.IAP
{
    /// <summary>
    /// Bọc <see cref="CrossPlatformValidator"/> của Unity IAP để kiểm tra chữ ký receipt
    /// ngay trên máy trước khi cấp hàng. Chặn được các bản hack billing kiểu
    /// Lucky Patcher / fake store vì chúng không ký được receipt bằng khoá của store.
    /// </summary>
    public sealed class IAPReceiptValidator
    {
        private const string Tag = "IAP";
        private const string MissingStoreSecretExceptionName = "MissingStoreSecretException";
        private const string StoreNotSupportedExceptionName = "StoreNotSupportedException";

        private CrossPlatformValidator _validator;

        public bool IsAvailable => _validator != null;

        /// <summary>
        /// Dựng validator cho store hiện tại. Gọi sau khi store đã connect.
        /// </summary>
        public void Initialize()
        {
            _validator = null;

#if UNITY_EDITOR
            GULogger.Warning(Tag, "Receipt validation is not available in the Editor. Purchases are granted without validation.");
#else
            var appStore = StandardPurchasingModule.Instance().appStore;
            switch (appStore)
            {
                case AppStore.GooglePlay:
                    InitializeGooglePlayValidator();
                    break;
                case AppStore.AppleAppStore:
                case AppStore.MacAppStore:
                    InitializeAppleValidator();
                    break;
                default:
                    GULogger.Warning(Tag, $"Receipt validation is not supported on store {appStore}.");
                    break;
            }
#endif
        }

        /// <summary>
        /// Kiểm tra receipt của một order.
        /// </summary>
        /// <param name="receipt">Chuỗi receipt hợp nhất lấy từ <c>order.Info.Receipt</c>.</param>
        /// <param name="parsedReceipts">Danh sách receipt đã parse khi hợp lệ.</param>
        public ReceiptValidationResult Validate(string receipt, out IPurchaseReceipt[] parsedReceipts)
        {
            parsedReceipts = Array.Empty<IPurchaseReceipt>();

            if (_validator == null)
            {
                return ReceiptValidationResult.ValidatorUnavailable;
            }

            if (string.IsNullOrWhiteSpace(receipt))
            {
                GULogger.Error(Tag, "Receipt is empty while a validator is available.");
                return ReceiptValidationResult.Invalid;
            }

            try
            {
                parsedReceipts = _validator.Validate(receipt) ?? Array.Empty<IPurchaseReceipt>();
                return ReceiptValidationResult.Valid;
            }
            catch (IAPSecurityException exception)
            {
                // MissingStoreSecretException và StoreNotSupportedException chỉ tồn tại trong
                // assembly Unity.Purchasing.Security (device-only), bắt theo tên để file này
                // vẫn biên dịch được trong Editor và các nền tảng dùng SecurityStub.
                switch (exception.GetType().Name)
                {
                    case MissingStoreSecretExceptionName:
                        GULogger.Error(Tag, $"Receipt validation skipped because a store secret is missing: {exception.Message}");
                        return ReceiptValidationResult.ValidatorUnavailable;
                    case StoreNotSupportedExceptionName:
                        GULogger.Warning(Tag, $"Receipt validation skipped: {exception.Message}");
                        return ReceiptValidationResult.Unsupported;
                    default:
                        GULogger.Error(Tag, $"Receipt rejected: {exception.GetType().Name} {exception.Message}");
                        return ReceiptValidationResult.Invalid;
                }
            }
            catch (Exception exception)
            {
                GULogger.Exception(exception, Tag);
                return ReceiptValidationResult.ValidatorUnavailable;
            }
        }

        /// <summary>
        /// Ghi log nội dung receipt đã parse để đối chiếu khi điều tra gian lận.
        /// </summary>
        public void LogParsedReceipts(IPurchaseReceipt[] parsedReceipts)
        {
            if (parsedReceipts == null)
            {
                return;
            }

            foreach (var parsedReceipt in parsedReceipts)
            {
                if (parsedReceipt is GooglePlayReceipt googleReceipt)
                {
                    GULogger.Log(Tag,
                        $"Receipt verified. productId={googleReceipt.productID} orderId={googleReceipt.orderID} state={googleReceipt.purchaseState}");
                    continue;
                }

                GULogger.Log(Tag,
                    $"Receipt verified. productId={parsedReceipt.productID} transactionId={parsedReceipt.transactionID}");
            }
        }

#if !UNITY_EDITOR
        private void InitializeGooglePlayValidator()
        {
            var googlePublicKey = IAPTangleProvider.GetGooglePlayPublicKey();
            if (googlePublicKey == null)
            {
                GULogger.Error(Tag, "Google Play receipt validation is disabled because GooglePlayTangle is missing.");
                return;
            }

            _validator = new CrossPlatformValidator(googlePublicKey, Application.identifier);
            GULogger.Log(Tag, "Google Play receipt validator is ready.");
        }

        private void InitializeAppleValidator()
        {
            var appleRootCertificate = IAPTangleProvider.GetAppleRootCertificate();
            if (appleRootCertificate == null)
            {
                GULogger.Error(Tag, "Apple receipt validation is disabled because AppleTangle is missing.");
                return;
            }

            _validator = new CrossPlatformValidator(null, appleRootCertificate, Application.identifier, Application.identifier);
            GULogger.Log(Tag, "Apple receipt validator is ready.");
        }
#endif
    }
}
