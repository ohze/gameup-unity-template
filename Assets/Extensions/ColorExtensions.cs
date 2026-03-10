using UnityEngine;

namespace GameUp.Extensions
{
    public static class ColorExtensions
    {
        public static Color WithAlpha(this Color c, float a) => new(c.r, c.g, c.b, a);

        /// <summary>Convert hex string (#RRGGBB or #RRGGBBAA) to Color.</summary>
        public static Color HexToColor(string hex)
        {
            if (hex.StartsWith("#")) hex = hex[1..];
            float r = int.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber) / 255f;
            float g = int.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber) / 255f;
            float b = int.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber) / 255f;
            float a = hex.Length >= 8
                ? int.Parse(hex[6..8], System.Globalization.NumberStyles.HexNumber) / 255f
                : 1f;
            return new Color(r, g, b, a);
        }

        /// <summary>Convert Color to hex string #RRGGBB.</summary>
        public static string ToHex(this Color c, bool includeAlpha = false)
        {
            var r = Mathf.RoundToInt(c.r * 255);
            var g = Mathf.RoundToInt(c.g * 255);
            var b = Mathf.RoundToInt(c.b * 255);
            if (!includeAlpha) return $"#{r:X2}{g:X2}{b:X2}";
            var a = Mathf.RoundToInt(c.a * 255);
            return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
        }
    }
}
