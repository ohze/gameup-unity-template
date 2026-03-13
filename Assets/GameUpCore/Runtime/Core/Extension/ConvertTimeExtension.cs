using System;

namespace GameUp.Core
{
    public static class ConvertTimeExtension
    {
        // Cache lại mốc thời gian gốc để tránh tạo mới liên tục
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        // --- UNIX TIMESTAMP CONVERSION ---

        public static DateTime ToDateTime(this long unixSeconds)
        {
            return UnixEpoch.AddSeconds(unixSeconds);
        }

        public static long ToUnixTimestamp(this DateTime date)
        {
            return (long)(date.ToUniversalTime() - UnixEpoch).TotalSeconds;
        }

        public static long CurrentUnixTimestamp()
        {
            return (long)(DateTime.UtcNow - UnixEpoch).TotalSeconds;
        }

        // --- FORMATTING FOR UI ---

        /// <summary>
        /// Đổi giây thành định dạng 00:00 hoặc 00:00:00
        /// </summary>
        public static string ToDurationString(this float seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1 
                ? string.Format("{0:D2}:{1:D2}:{2:D2}", (int)t.TotalHours, t.Minutes, t.Seconds) 
                : string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
        }

        /// <summary>
        /// Hiển thị thời gian rút gọn (Ví dụ: 2d 5h, 1h 30m, 45s)
        /// </summary>
        public static string ToAbbreviatedString(this float seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            if (t.TotalDays >= 1) return $"{t.Days}d {t.Hours}h";
            if (t.TotalHours >= 1) return $"{t.Hours}h {t.Minutes}m";
            if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds}s";
            return $"{t.Seconds}s";
        }

        /// <summary>
        /// Kiểm tra xem một mốc Unix Timestamp đã trôi qua chưa so với hiện tại
        /// </summary>
        public static bool IsExpired(this long targetUnixTimestamp)
        {
            return CurrentUnixTimestamp() >= targetUnixTimestamp;
        }

        /// <summary>
        /// Lấy khoảng thời gian còn lại (Remaining Time) từ hiện tại đến đích
        /// </summary>
        public static string GetTimeRemaining(this long targetUnixTimestamp)
        {
            long diff = targetUnixTimestamp - CurrentUnixTimestamp();
            return diff > 0 ? ToAbbreviatedString(diff) : "0s";
        }

        // Ví dụ: 3661 giây -> "1h 1m" hoặc "1h 1m 1s"
        public static string ToReadableTime(this int totalSeconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(totalSeconds);
            if (t.TotalDays >= 1) return string.Format("{0}d {1}h", t.Days, t.Hours);
            if (t.TotalHours >= 1) return string.Format("{0}h {1}m", t.Hours, t.Minutes);
            if (t.TotalMinutes >= 1) return string.Format("{0}m {1}s", t.Minutes, t.Seconds);
            return string.Format("{0}s", t.Seconds);
        }

        // Ví dụ: 90 giây -> "01:30"
        public static string ToTimerFormat(this float seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            if (t.TotalHours >= 1)
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)t.TotalHours, t.Minutes, t.Seconds);
            return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
        }
    }
}