using System.Security.Cryptography;
using System.Text;

namespace GameUp.Extensions
{
    public static class StringExtensions
    {
        public static bool IsNullOrEmpty(this string s) => string.IsNullOrEmpty(s);

        public static bool IsNullOrWhiteSpace(this string s) => string.IsNullOrWhiteSpace(s);

        /// <summary>Truncate string to maxLength, appending "..." if truncated.</summary>
        public static string Truncate(this string s, int maxLength, string suffix = "...")
        {
            if (s.IsNullOrEmpty() || s.Length <= maxLength) return s;
            return s[..(maxLength - suffix.Length)] + suffix;
        }

        /// <summary>Simple MD5 hash for non-security purposes (e.g. cache keys).</summary>
        public static string ToMD5(this string input)
        {
            using var md5 = MD5.Create();
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(32);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>Convert "camelCase" or "PascalCase" to "Camel Case" or "Pascal Case".</summary>
        public static string ToTitleCase(this string s)
        {
            if (s.IsNullOrEmpty()) return s;
            var sb = new StringBuilder(s.Length + 4);
            sb.Append(char.ToUpper(s[0]));
            for (int i = 1; i < s.Length; i++)
            {
                if (char.IsUpper(s[i]) && !char.IsUpper(s[i - 1]))
                    sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}
