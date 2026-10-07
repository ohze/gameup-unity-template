namespace GameUp.Core
{
    /// <summary>
    /// Backend mặc định của <see cref="LocalStorageUtils"/>: PlayerPrefs của Unity.
    /// </summary>
    /// <remarks>
    /// Ghi rõ <c>UnityEngine.PlayerPrefs</c>: SDK của vài nền tảng (TTSDK) khai một class
    /// <c>PlayerPrefs</c> ở namespace gốc, gọi trống tên là trình biên dịch lặng lẽ chọn bản đó.
    /// </remarks>
    public sealed class UnityPlayerPrefsBackend : ILocalStorageBackend
    {
        public bool HasKey(string key)
        {
            return UnityEngine.PlayerPrefs.HasKey(key);
        }

        public string GetString(string key)
        {
            return UnityEngine.PlayerPrefs.GetString(key);
        }

        public void SetString(string key, string value)
        {
            UnityEngine.PlayerPrefs.SetString(key, value);
        }

        public void Save()
        {
            UnityEngine.PlayerPrefs.Save();
        }
    }
}
