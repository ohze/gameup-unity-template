using System;
using System.Globalization;
using GameUp.Core.Serializer;

namespace GameUp.Core
{
    /// <summary>
    /// Lưu key-value có mã hóa, mặc định trên PlayerPrefs. Mọi getter đều fail-safe: dữ liệu hỏng/đổi
    /// format sẽ trả về giá trị mặc định thay vì ném exception làm crash lúc khởi động.
    /// Số luôn đọc/ghi theo InvariantCulture để không phụ thuộc ngôn ngữ máy.
    /// </summary>
    /// <remarks>
    /// Nơi cất thật là một <see cref="ILocalStorageBackend"/> đổi được qua <see cref="SetBackend"/>.
    /// </remarks>
    public static class LocalStorageUtils
    {
        private const string DEVICE_ID = "dv";

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        private static ILocalStorageBackend _backend = new UnityPlayerPrefsBackend();

        /// <summary>
        /// Đổi nơi cất dữ liệu. Gọi trước lần đọc save đầu tiên (ví dụ
        /// <c>[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]</c>) — đổi giữa chừng là đọc bên này,
        /// ghi bên kia.
        /// </summary>
        public static void SetBackend(ILocalStorageBackend backend)
        {
            if (backend == null)
            {
                GULogger.Error("LocalStorage", "SetBackend called with null — giữ backend hiện tại.");
                return;
            }

            _backend = backend;
        }

        /// <summary>
        /// Đẩy dữ liệu xuống bộ nhớ thật. Với PlayerPrefs của Unity, không gọi thì chỉ tới
        /// <c>OnApplicationQuit</c> mới ghi — WebGL không có bước đó. Gây giật một nhịp, đừng gọi giữa gameplay.
        /// </summary>
        public static void Save()
        {
            _backend.Save();
        }

        public static bool HasKey(string key)
        {
            return _backend.HasKey(key);
        }

        /// <summary>Đọc và giải mã raw string. Trả về chuỗi rỗng nếu key trống hoặc dữ liệu không giải mã được.</summary>
        private static string ReadRaw(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            var value = _backend.GetString(key);
            if (string.IsNullOrEmpty(value)) return string.Empty;

            try
            {
                return EncryptUtils.Decrypt(value);
            }
            catch (Exception e)
            {
                // Warning chứ không phải Error: dữ liệu cũ/hỏng là tình huống lường trước và đã có
                // đường lui (trả về default). Error để dành cho lỗi thật sự, vì Error không bị strip
                // khỏi build nên sẽ đổ thẳng vào crash reporter.
                GULogger.Warning("LocalStorage", $"Decrypt failed for key '{key}', dùng giá trị mặc định: {e.Message}");
                return string.Empty;
            }
        }

        private static void WriteRaw(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                GULogger.Error("LocalStorage", "WriteRaw called with an empty key");
                return;
            }

            try
            {
                _backend.SetString(key, EncryptUtils.Encrypt(value ?? string.Empty));
            }
            catch (Exception e)
            {
                GULogger.Error("LocalStorage", $"Encrypt failed for key '{key}': {e.Message}");
            }
        }

        public static string GetString(string key, string defaultStr = "")
        {
            var value = ReadRaw(key);
            return string.IsNullOrEmpty(value) ? defaultStr : value;
        }

        public static void SetString(string key, string value)
        {
            WriteRaw(key, value);
        }

        public static void SetInt(string key, int value)
        {
            WriteRaw(key, value.ToString(Culture));
        }

        public static int GetInt(string key, int d = 0)
        {
            var value = ReadRaw(key);
            if (string.IsNullOrEmpty(value)) return d;

            return int.TryParse(value, NumberStyles.Integer, Culture, out var result) ? result : d;
        }

        public static void SetLong(string key, long value)
        {
            WriteRaw(key, value.ToString(Culture));
        }

        public static long GetLong(string key, long d = 0)
        {
            var value = ReadRaw(key);
            if (string.IsNullOrEmpty(value)) return d;

            return long.TryParse(value, NumberStyles.Integer, Culture, out var result) ? result : d;
        }

        public static void SetFloat(string key, float value)
        {
            WriteRaw(key, value.ToString("R", Culture));
        }

        public static float GetFloat(string key, float d = 0)
        {
            var value = ReadRaw(key);
            if (string.IsNullOrEmpty(value)) return d;

            // Dữ liệu cũ có thể được ghi bằng culture dùng dấu phẩy — vẫn đọc lại được.
            if (float.TryParse(value, NumberStyles.Float, Culture, out var result)) return result;
            if (float.TryParse(value.Replace(',', '.'), NumberStyles.Float, Culture, out result)) return result;

            return d;
        }

        public static string GetDeviceID()
        {
            return GetString(DEVICE_ID);
        }

        public static void SetDeviceID(string id)
        {
            SetString(DEVICE_ID, id);
        }

        public static void SetBoolean(string key, bool v)
        {
            SetInt(key, v ? 1 : 0);
        }

        public static bool GetBoolean(string key, bool v = default)
        {
            return GetInt(key, v ? 1 : 0) == 1;
        }

        /// <summary>
        /// Lưu object (Dictionary, List, class...) dùng FullSerializer — hỗ trợ Dictionary, polymorphism.
        /// </summary>
        public static void SetObject<T>(string key, T obj)
        {
            if (obj == null)
            {
                SetString(key, string.Empty);
                return;
            }

            try
            {
                SetString(key, obj.Serialize());
            }
            catch (Exception e)
            {
                GULogger.Error("LocalStorage", $"SetObject failed for key '{key}': {e.Message}");
            }
        }

        /// <summary>
        /// Đọc object (Dictionary, List, class...) đã lưu bằng SetObject.
        /// </summary>
        public static T GetObject<T>(string key, T defaultValue = default)
        {
            string json = GetString(key);
            if (string.IsNullOrEmpty(json)) return defaultValue;

            try
            {
                var result = json.Deserialize<T>();
                return result != null ? result : defaultValue;
            }
            catch (Exception e)
            {
                // Cùng lý do như ReadRaw: đọc hỏng thì có default để lui, không phải lỗi cần báo động.
                GULogger.Warning("LocalStorage", $"GetObject failed for key '{key}', dùng giá trị mặc định: {e.Message}");
                return defaultValue;
            }
        }
    }
}
