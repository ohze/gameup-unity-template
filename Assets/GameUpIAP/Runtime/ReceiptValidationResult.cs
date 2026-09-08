namespace GameUp.IAP
{
    /// <summary>
    /// Kết quả kiểm tra receipt cục bộ của một order.
    /// </summary>
    public enum ReceiptValidationResult
    {
        /// <summary>Receipt hợp lệ, chữ ký khớp store.</summary>
        Valid,

        /// <summary>Receipt giả hoặc bị sửa — không được cấp hàng.</summary>
        Invalid,

        /// <summary>Store hiện tại không hỗ trợ validate cục bộ (Editor, fake store, Apple StoreKit 2).</summary>
        Unsupported,

        /// <summary>Chưa sinh tangle hoặc thiếu store secret nên không validate được.</summary>
        ValidatorUnavailable
    }
}
