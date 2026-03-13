using System;
using System.Globalization;
using UnityEngine;

namespace GameUp.Core
{
    public static class TimeUtils
    {
        private static GameTime _instance;
        public static GameTime GameTime => _instance ??= new GameTime();

        // Khởi tạo hệ thống với thời gian từ Server hoặc UTC máy
        public static void Initialize(DateTime? serverTime = null)
        {
            GameTime.Init(serverTime ?? DateTime.UtcNow);
        }
    }

    [Serializable]
    public class GameTime
    {
        private const string KEY_LAST_LOGIN = "gt_last_login";

        // Flags
        public bool IsFirstSessionAllTime { get; private set; }
        public bool IsFirstSessionInDay { get; private set; }
        public bool IsFirstLoginInWeek { get; private set; }
        public bool IsNewMonth { get; private set; }

        private DateTime _loginTime;       // Thời điểm bắt đầu session (UTC)
        private float _loginTimeUnity;     // Time.realtimeSinceStartup lúc login
        private DateTime _lastLoginDate;   // Ngày cuối cùng user online (chỉ lấy phần .Date)

        // Timestamp hiện tại của game (đã cộng thêm thời gian chơi từ lúc mở app)
        public long CurrentTimestamp => 
            ((DateTimeOffset)_loginTime).ToUnixTimeSeconds() + (long)(Time.realtimeSinceStartup - _loginTimeUnity);

        public void Init(DateTime currentUtcTime)
        {
            _loginTime = currentUtcTime;
            _loginTimeUnity = Time.realtimeSinceStartup;
            
            LoadAndCheckIn();
        }

        private void LoadAndCheckIn()
        {
            // Kiểm tra xem đã bao giờ login chưa
            if (!LocalStorageUtils.HasKey(KEY_LAST_LOGIN))
            {
                IsFirstSessionAllTime = true;
                IsFirstSessionInDay = true;
                IsFirstLoginInWeek = true;
                IsNewMonth = true;
                
                _lastLoginDate = _loginTime.Date;
                Save();
                return;
            }

            IsFirstSessionAllTime = false;
            // Dùng GetLong để lấy timestamp ngày cuối cùng (đã tối ưu ở file LocalStorageUtils)
            long lastTimestamp = LocalStorageUtils.GetLong(KEY_LAST_LOGIN);
            DateTime lastDate = DateTimeOffset.FromUnixTimeSeconds(lastTimestamp).UtcDateTime.Date;

            DateTime currentDate = _loginTime.Date;

            if (currentDate > lastDate)
            {
                IsFirstSessionInDay = true;
                
                // Kiểm tra tuần mới (Theo chuẩn ISO: tuần bắt đầu từ Thứ 2)
                IsFirstLoginInWeek = CheckIsNewWeek(lastDate, currentDate);
                
                // Kiểm tra tháng mới
                IsNewMonth = currentDate.Month != lastDate.Month || currentDate.Year != lastDate.Year;

                GULogger.Log("TimeUtils", $"New Day! Week: {IsFirstLoginInWeek}, Month: {IsNewMonth}");
                
                _lastLoginDate = currentDate;
                Save();
            }
            else
            {
                IsFirstSessionInDay = false;
                IsFirstLoginInWeek = false;
                IsNewMonth = false;
            }
        }

        private void Save()
        {
            // Lưu timestamp của ngày (00:00:00) để dễ so sánh
            long ts = ((DateTimeOffset)_lastLoginDate).ToUnixTimeSeconds();
            LocalStorageUtils.SetLong(KEY_LAST_LOGIN, ts);
        }

        #region Helper Methods (Tính thời gian còn lại)

        // Tính giây tới 00:00:00 ngày mai
        public long SecondsToNextDay()
        {
            DateTime nextDay = _loginTime.Date.AddDays(1);
            return (long)(nextDay - _loginTime).TotalSeconds - (long)(Time.realtimeSinceStartup - _loginTimeUnity);
        }

        // Tính giây tới Thứ 2 tuần sau
        public long SecondsToNextWeek()
        {
            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)_loginTime.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7;
            
            DateTime nextMonday = _loginTime.Date.AddDays(daysUntilMonday);
            return (long)(nextMonday - _loginTime).TotalSeconds - (long)(Time.realtimeSinceStartup - _loginTimeUnity);
        }

        // Tính giây tới ngày 1 tháng sau (Chính xác tuyệt đối)
        public long SecondsToNextMonth()
        {
            DateTime nextMonth = new DateTime(_loginTime.Year, _loginTime.Month, 1).AddMonths(1);
            return (long)(nextMonth - _loginTime).TotalSeconds - (long)(Time.realtimeSinceStartup - _loginTimeUnity);
        }

        private bool CheckIsNewWeek(DateTime last, DateTime current)
        {
            Calendar cal = CultureInfo.InvariantCulture.Calendar;
            int lastWeek = cal.GetWeekOfYear(last, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            int currentWeek = cal.GetWeekOfYear(current, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            
            return currentWeek != lastWeek || (current - last).TotalDays >= 7;
        }

        #endregion
    }
}