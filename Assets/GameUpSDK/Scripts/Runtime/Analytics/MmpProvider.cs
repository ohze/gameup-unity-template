namespace GameUp.SDK
{
    /// <summary>
    /// MMP (mobile measurement partner) lo attribution + ad revenue + event chuyển đổi.
    /// AppsFlyer và Adjust giữ vai trò tương đương — mỗi project chọn một bên ở Setup Dependencies.
    /// </summary>
    public enum MmpProvider
    {
        AppsFlyer,
        Adjust
    }
}
