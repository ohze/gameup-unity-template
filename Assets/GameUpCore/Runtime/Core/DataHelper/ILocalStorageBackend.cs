namespace GameUp.Core
{
    /// <summary>
    /// Nơi <see cref="LocalStorageUtils"/> thực sự cất dữ liệu (key → chuỗi đã mã hoá).
    /// </summary>
    /// <remarks>
    /// Mặc định là <see cref="UnityPlayerPrefsBackend"/>. Nền tảng mà PlayerPrefs của Unity không
    /// lưu được qua các lần mở game (ví dụ TikTok Mini Games, phải dùng <c>TT.PlayerPrefs</c>) thì
    /// game cắm backend riêng bằng <see cref="LocalStorageUtils.SetBackend"/> — trước khi đọc save.
    /// </remarks>
    public interface ILocalStorageBackend
    {
        bool HasKey(string key);

        string GetString(string key);

        void SetString(string key, string value);

        /// <summary>Đẩy dữ liệu đang giữ trong bộ nhớ xuống bộ nhớ thật (nếu backend có đệm).</summary>
        void Save();
    }
}
