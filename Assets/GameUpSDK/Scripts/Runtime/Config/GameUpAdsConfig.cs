using System;
using System.Collections.Generic;
using GameUp.Core;
using UnityEngine;

namespace GameUp.SDK
{
    /// <summary>Bộ 5 config cho 1 mediation network.</summary>
    [Serializable]
    public class AdUnitConfigSet
    {
        public AdUnitConfig banner = new AdUnitConfig();
        public AdUnitConfig interstitial = new AdUnitConfig();
        public AdUnitConfig rewarded = new AdUnitConfig();
        public AdUnitConfig appOpen = new AdUnitConfig();
        public AdUnitConfig nativeAd = new AdUnitConfig();

        public AdUnitConfig Get(AdUnitType type)
        {
            switch (type)
            {
                case AdUnitType.Banner: return banner;
                case AdUnitType.Interstitial: return interstitial;
                case AdUnitType.RewardedVideo: return rewarded;
                case AdUnitType.AppOpen: return appOpen;
                case AdUnitType.NativeAd: return nativeAd;
                default: return null;
            }
        }

        public IEnumerable<AdUnitConfig> All()
        {
            yield return banner;
            yield return interstitial;
            yield return rewarded;
            yield return appOpen;
            yield return nativeAd;
        }

        public bool MigrateLegacyEntries()
        {
            bool changed = false;
            foreach (var config in All())
            {
                if (config != null) changed |= config.MigrateLegacyEntries();
            }
            return changed;
        }
    }

    [Serializable]
    public class AdmobAdsSettings
    {
        [Tooltip("App ID AdMob (Android). Được đồng bộ sang GoogleMobileAdsSettings.asset khi bấm Save.")]
        public string appIdAndroid;

        [Tooltip("App ID AdMob (iOS). Được đồng bộ sang GoogleMobileAdsSettings.asset khi bấm Save.")]
        public string appIdIOS;

        [Tooltip("Danh sách device test (GAID / IDFA) để nhận test ads.")]
        public List<string> testDevices = new List<string>();

        [Tooltip("Mở Ad Inspector ngay sau khi init (chỉ dùng khi debug).")]
        public bool showMediationInspector;

        [Header("UMP — chỉ có tác dụng ở development build")]
        [Tooltip("Ép UMP coi thiết bị đang ở EEA để test consent form. Bị bỏ qua ở bản release.")]
        public bool umpDebugForceEea;

        [Tooltip("Hashed device ID cho UMP (UMP in ra trong log lần chạy đầu). KHÁC với testDevices ở trên — cái đó dùng cho ad request.")]
        public List<string> umpTestDeviceHashedIds = new List<string>();

        public AdUnitConfigSet units = new AdUnitConfigSet();
    }

    [Serializable]
    public class MaxAdsSettings
    {
        [Tooltip("SDK Key của AppLovin MAX.")]
        public string sdkKey;

        [Tooltip("Mở Mediation Debugger ngay sau khi init (chỉ dùng khi debug).")]
        public bool showMediationDebugger;

        public AdUnitConfigSet units = new AdUnitConfigSet();
    }

    [Serializable]
    public class IronSourceAdsSettings
    {
        [Tooltip("App Key của IronSource LevelPlay.")]
        public string appKey;

        public AdUnitConfigSet units = new AdUnitConfigSet();
    }

    /// <summary>
    /// Nguồn sự thật duy nhất cho toàn bộ ID & thông số quảng cáo của dự án.
    /// Asset nằm trong project (Resources), KHÔNG nằm trong package, nên project nào
    /// cài SDK qua UPM cũng chỉ cần mở GameUp/SDK/Setup, điền ID rồi Save là chạy.
    /// </summary>
    [CreateAssetMenu(fileName = "GameUpAdsConfig", menuName = "GameUp/Ads Config", order = 0)]
    public class GameUpAdsConfig : ScriptableObject
    {
        public const string ResourceFolder = "GameUpSDK";
        public const string AssetName = "GameUpAdsConfig";

        /// <summary>Đường dẫn dùng cho Resources.Load (không có đuôi .asset).</summary>
        public const string ResourcePath = ResourceFolder + "/" + AssetName;

        [Header("Waterfall")]
        [Tooltip("Thứ tự ưu tiên mạng quảng cáo. Index 0 là mạng chính, lỗi thì rớt xuống mạng kế tiếp.")]
        public List<MediationProvider> mediationPriority = new List<MediationProvider>
            { MediationProvider.Admob, MediationProvider.Max, MediationProvider.IronSource };

        [Tooltip("Tỉ lệ (%) biến toàn bộ vùng Native Ad thành CTA. Remote Config native_cta_click_rate ghi đè giá trị này.")]
        [Range(0, 100)] public int nativeCtaClickRate = 30;

        [Header("AdMob")] public AdmobAdsSettings admob = new AdmobAdsSettings();
        [Header("AppLovin MAX")] public MaxAdsSettings max = new MaxAdsSettings();
        [Header("IronSource LevelPlay")] public IronSourceAdsSettings ironSource = new IronSourceAdsSettings();

        private static GameUpAdsConfig _instance;
        private static bool _lookedUp;

        /// <summary>Asset cấu hình của project. Null nếu chưa chạy Setup lần nào.</summary>
        public static GameUpAdsConfig Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (_lookedUp) return null;

                _lookedUp = true;
                _instance = Resources.Load<GameUpAdsConfig>(ResourcePath);

#if UNITY_EDITOR
                if (_instance == null) _instance = GameUpConfigLookup.FindInProject<GameUpAdsConfig>();
#endif
                if (_instance == null)
                {
                    GULogger.Error("GameUp",
                        $"Không tìm thấy {AssetName}. Mở menu GameUp/SDK/Setup rồi bấm 'Save Configuration' để tạo asset tại Resources/{ResourcePath}.asset");
                }
                return _instance;
            }
        }

        /// <summary>Ưu tiên asset override gắn trực tiếp trên network, nếu không có thì lấy asset chung.</summary>
        public static GameUpAdsConfig Resolve(GameUpAdsConfig overrideAsset)
        {
            return overrideAsset != null ? overrideAsset : Instance;
        }

        /// <summary>Xoá cache (editor gọi sau khi tạo/di chuyển asset).</summary>
        public static void ClearCache()
        {
            _instance = null;
            _lookedUp = false;
        }

        public bool MigrateLegacyEntries()
        {
            bool changed = false;
            changed |= admob.units.MigrateLegacyEntries();
            changed |= max.units.MigrateLegacyEntries();
            changed |= ironSource.units.MigrateLegacyEntries();
            return changed;
        }
    }
}
