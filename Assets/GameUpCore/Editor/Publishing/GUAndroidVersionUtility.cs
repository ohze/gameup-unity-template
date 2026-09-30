#if UNITY_EDITOR
using System;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Tính version name / version code kế tiếp cho bản Android đẩy lên Google Play.
    /// Quy ước code suy từ name: <c>major*100 + minor*10 + patch</c> (1.0.7 → 107), chỉ áp dụng khi minor và patch đều &lt; 10.
    /// </summary>
    public static class GUAndroidVersionUtility
    {
        public const int NoCode = -1;

        public static bool TryParseVersionName(string versionName, out int[] parts)
        {
            parts = null;
            if (string.IsNullOrWhiteSpace(versionName)) return false;

            var tokens = versionName.Trim().Split('.');
            var result = new int[tokens.Length];
            for (var i = 0; i < tokens.Length; i++)
            {
                if (!int.TryParse(tokens[i], out result[i]) || result[i] < 0) return false;
            }

            parts = result;
            return true;
        }

        /// <summary>So sánh theo từng phần số, phần thiếu coi là 0 (1.1 == 1.1.0). Name không hợp lệ luôn nhỏ hơn name hợp lệ.</summary>
        public static int CompareVersionNames(string a, string b)
        {
            var validA = TryParseVersionName(a, out var partsA);
            var validB = TryParseVersionName(b, out var partsB);
            if (!validA || !validB) return validA.CompareTo(validB);

            var length = Math.Max(partsA.Length, partsB.Length);
            for (var i = 0; i < length; i++)
            {
                var valueA = i < partsA.Length ? partsA[i] : 0;
                var valueB = i < partsB.Length ? partsB[i] : 0;
                if (valueA != valueB) return valueA.CompareTo(valueB);
            }

            return 0;
        }

        /// <summary>Tăng phần cuối, bổ sung đủ 3 phần: 1.0.7 → 1.0.8, 1.0 → 1.0.1, 1.2.3.4 → 1.2.3.5.</summary>
        public static string BumpVersionName(string versionName)
        {
            if (!TryParseVersionName(versionName, out var parts)) return "1.0.0";

            var length = Math.Max(parts.Length, 3);
            var bumped = new int[length];
            Array.Copy(parts, bumped, parts.Length);
            bumped[length - 1]++;
            return string.Join(".", bumped);
        }

        /// <summary>1.0.7 → 107. Trả <see cref="NoCode"/> khi name không theo được quy ước (hơn 3 phần, minor hoặc patch ≥ 10).</summary>
        public static int DeriveVersionCode(string versionName)
        {
            if (!TryParseVersionName(versionName, out var parts) || parts.Length > 3) return NoCode;

            var major = parts[0];
            var minor = parts.Length > 1 ? parts[1] : 0;
            var patch = parts.Length > 2 ? parts[2] : 0;
            if (minor > 9 || patch > 9) return NoCode;

            return major * 100 + minor * 10 + patch;
        }

        /// <summary>Bump từ name cao hơn giữa store và local.</summary>
        public static string ComputeNextVersionName(string storeVersionName, string localVersionName)
        {
            var baseName = CompareVersionNames(storeVersionName, localVersionName) >= 0 ? storeVersionName : localVersionName;
            return BumpVersionName(baseName);
        }

        /// <summary>
        /// Code phải lớn hơn mọi code đã dùng: code suy từ name trên store, code local, code lớn nhất trên Play Console (mọi track).
        /// Nếu code suy từ <paramref name="nextVersionName"/> còn lớn hơn thì ưu tiên nó để giữ đúng quy ước name ↔ code.
        /// </summary>
        public static int ComputeNextVersionCode(string nextVersionName, string storeVersionName, int localVersionCode, int apiMaxVersionCode)
        {
            var usedMax = Math.Max(DeriveVersionCode(storeVersionName), Math.Max(localVersionCode, apiMaxVersionCode));
            return Math.Max(usedMax + 1, DeriveVersionCode(nextVersionName));
        }
    }
}
#endif
