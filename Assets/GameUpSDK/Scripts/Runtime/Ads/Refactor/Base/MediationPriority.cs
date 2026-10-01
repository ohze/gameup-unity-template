using System.Collections.Generic;

namespace GameUp.SDK
{
    /// <summary>
    /// Thứ tự waterfall giữa các mạng quảng cáo đã cài SDK. Dùng chung cho runtime (<see cref="AdsManager"/>),
    /// tab Setup và installer để cả ba luôn thấy cùng một thứ tự.
    /// </summary>
    public static class MediationPriority
    {
        /// <summary>Mạng có SDK trong project (theo define *_DEPENDENCIES_INSTALLED), thứ tự mặc định AdMob → MAX → LevelPlay.</summary>
        public static List<MediationProvider> GetInstalledProviders()
        {
            var list = new List<MediationProvider>(3);
#if ADMOB_DEPENDENCIES_INSTALLED
            list.Add(MediationProvider.Admob);
#endif
#if MAXSDK_DEPENDENCIES_INSTALLED
            list.Add(MediationProvider.Max);
#endif
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            list.Add(MediationProvider.IronSource);
#endif
            return list;
        }

        /// <summary>
        /// Giữ thứ tự đã lưu cho các mạng còn cài, bỏ None / trùng / mạng đã gỡ SDK, và nối mạng mới cài
        /// (chưa có trong danh sách) vào cuối — nên cài thêm MAX/LevelPlay là được dùng ngay làm mạng dự phòng,
        /// không cần mở tab Setup để lưu lại.
        /// </summary>
        public static List<MediationProvider> Resolve(IEnumerable<MediationProvider> saved)
        {
            var installed = GetInstalledProviders();
            var ordered = new List<MediationProvider>(installed.Count);

            if (saved != null)
            {
                foreach (var provider in saved)
                {
                    if (installed.Contains(provider) && !ordered.Contains(provider)) ordered.Add(provider);
                }
            }

            foreach (var provider in installed)
            {
                if (!ordered.Contains(provider)) ordered.Add(provider);
            }

            return ordered;
        }
    }
}
