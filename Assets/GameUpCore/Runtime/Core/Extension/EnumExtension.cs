using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace GameUp.Core
{
    public static class EnumExtension
    {
        // Cache để tránh gọi Enum.GetValues liên tục gây tốn tài nguyên
        private static readonly Dictionary<Type, Array> _cache = new Dictionary<Type, Array>();

        public static T[] GetValues<T>() where T : Enum
        {
            var type = typeof(T);
            if (!_cache.TryGetValue(type, out var values))
            {
                values = Enum.GetValues(type);
                _cache[type] = values;
            }
            return (T[])values;
        }

        // --- RANDOM ---

        public static T GetRandom<T>() where T : Enum
        {
            var values = GetValues<T>();
            return values[Random.Range(0, values.Length)];
        }

        public static T GetRandom<T>(params T[] excludedValues) where T : Enum
        {
            var values = GetValues<T>().Where(v => !excludedValues.Contains(v)).ToArray();
            if (values.Length == 0) return default;
            return values[Random.Range(0, values.Length)];
        }

        // --- TIỆN ÍCH MỞ RỘNG ---

        /// <summary>
        /// Lấy giá trị tiếp theo trong Enum (hữu ích cho việc chuyển trạng thái tuần tự)
        /// </summary>
        public static T Next<T>(this T src) where T : Enum
        {
            T[] arr = GetValues<T>();
            int j = Array.IndexOf(arr, src) + 1;
            return (arr.Length == j) ? arr[0] : arr[j];
        }

        /// <summary>
        /// Chuyển string sang Enum an toàn (không phân biệt hoa thường)
        /// </summary>
        public static T ToEnum<T>(this string value, T defaultValue = default) where T : struct, Enum
        {
            if (string.IsNullOrEmpty(value)) return defaultValue;
            return Enum.TryParse<T>(value, true, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Kiểm tra nhanh xem một chuỗi có tồn tại trong Enum không
        /// </summary>
        public static bool IsDefined<T>(string value) where T : Enum
        {
            return Enum.IsDefined(typeof(T), value);
        }
    }
}