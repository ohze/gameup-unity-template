using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GameUp.Core
{
    public static class StringUtils
    {
        // Cache Regex để không phải khởi tạo lại nhiều lần, tăng hiệu năng rõ rệt
        private static readonly Regex UsernameRegex = new Regex(@"^[a-z0-9_ A-ZàÀảẢãÃáÁạẠăĂằẰẳẲẵẴắẮặẶâÂầẦẩẨẫẪấẤậẬđĐèÈẻẺẽẼéÉẹẸêÊềỀểỂễỄếẾệỆìÌỉỈĩĨíÍịỊòÒỏỎõÕóÓọỌôÔồỒổỔỗỖốỐộỘơƠờỜởỞỡỠớỚợỢùÙủỦũŨúÚụỤưƯừỪửỬữỮứỨựỰỳỲỷỶỹỸýÝỵỴ]+$");
        private static readonly Regex SpaceTrimRegex = new Regex(@"^\s|\s$");
        private static readonly Regex DigitRegex = new Regex(@"\d+");

        private static readonly HashSet<string> DisallowedNames = new HashSet<string>
        {
            "admin", "moderator", "hồ chí minh", "ho_chi_minh", "mod", "lồn", "địt", "cứt", "ditme"
        };

        #region Money Formatting

        public static string FormatMoney(this long v, long max = 1000000000)
        {
            // Dùng CultureInfo "de-DE" cho định dạng dấu chấm ngăn cách nghìn (1.000.000)
            if (v < max) return v.ToString("N0", CultureInfo.GetCultureInfo("de-DE"));
            return FormatMoneyK(v);
        }

        public static string FormatMoneyK(double value, int digit = 2)
        {
            if (value >= 1000000000) return $"{(value / 1000000000):F2}B";
            if (value >= 1000000) return $"{(value / 1000000):F2}M";
            if (value >= 1000) return $"{(value / 1000):F2}K";
            return value.ToString(CultureInfo.InvariantCulture);
        }

        #endregion

        #region Time Formatting

        public static string FormatTimeOffline(this long second)
        {
            var t = TimeSpan.FromSeconds(second);
            if (t.TotalSeconds < 60) return $"{(int)t.TotalSeconds} giây trước";
            if (t.TotalMinutes < 60) return $"{(int)t.TotalMinutes} phút trước";
            if (t.TotalHours < 24) return $"{(int)t.TotalHours} giờ trước";
            return $"{(int)t.TotalDays} ngày trước";
        }

        public static string ConvertSecondsToMinutesAndSeconds(this long seconds)
        {
            return string.Format("{0:D2}:{1:D2}", seconds / 60, seconds % 60);
        }

        public static string ConvertSecondsToTimeFormat(this long seconds)
        {
            return string.Format("{0:D2}:{1:D2}:{2:D2}", seconds / 3600, (seconds % 3600) / 60, seconds % 60);
        }

        #endregion

        #region Validation

        public static string CheckAvailableUserName(string userName)
        {
            if (string.IsNullOrEmpty(userName) || userName.Length < 5) return "Tên đăng ký phải có ít nhất 5 ký tự";
            if (userName.Length > 20) return "Tên đăng ký không nhiều hơn 20 ký tự";
            if (SpaceTrimRegex.IsMatch(userName)) return "Tên không được chứa khoảng trống ở đầu hoặc cuối";
            if (!UsernameRegex.IsMatch(userName)) return "Tên không được chứa ký tự đặc biệt";

            string lowerName = userName.ToLower();
            foreach (var badWord in DisallowedNames)
            {
                if (lowerName.Contains(badWord)) return "Tên đăng ký sử dụng từ ngữ không hợp lệ";
            }

            return string.Empty;
        }

        #endregion

        #region Rich Text & Processing

        public static string ToBold(this string msg) => $"<b>{msg}</b>";

        public static string ToColor(this string msg, string color) => $"<color={color}>{msg}</color>";

        public static string ToColor(this string msg, Color color) 
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{msg}</color>";
        }

        public static string ProcessStringLine(this string msg, int maxLengthInLine)
        {
            if (string.IsNullOrEmpty(msg)) return "";
            
            StringBuilder sb = new StringBuilder();
            string[] words = msg.Split(' ');
            int currentLineLength = 0;

            foreach (var word in words)
            {
                if (currentLineLength + word.Length > maxLengthInLine)
                {
                    sb.Append("<br>");
                    currentLineLength = 0;
                }
                
                if (currentLineLength > 0)
                {
                    sb.Append(" ");
                    currentLineLength++;
                }

                sb.Append(word);
                currentLineLength += word.Length;
            }

            return sb.ToString();
        }

        public static string CutNumberFromString(this string msg, string color)
        {
            // Sử dụng Regex để tìm các cụm số, hiệu năng cao và code sạch hơn loop thủ công
            return DigitRegex.Replace(msg, m => 
            {
                long val = long.Parse(m.Value);
                return $"<color={color}>{val.FormatMoney(10000)}</color>";
            });
        }

        #endregion
    }
}