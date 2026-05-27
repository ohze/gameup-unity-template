using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace GameUp.SDK.Installer
{
    /// <summary>
    /// Cửa sổ hướng dẫn cài đặt tất cả package phụ thuộc của GameUp SDK.
    /// Tự động xuất hiện khi SDK được cài lần đầu tiên qua Git URL Package.
    /// </summary>
    public class GameUpDependenciesWindow : EditorWindow
    {
        private enum WindowTab
        {
            SetupDependencies = 0,
            AdMobMediation = 1,
        }

        // ─── Định nghĩa các package phụ thuộc ────────────────────────────────────

        private enum InstallMethod
        {
            /// <summary>Cài qua Unity Package Manager bằng Git URL</summary>
            GitUrl,

            /// <summary>Cài qua scoped registry trong manifest.json</summary>
            ScopedRegistry,

            /// <summary>Import .unitypackage đã được bundle trong thư mục Packages~</summary>
            UnityPackage,

            /// <summary>Chỉ mở trang web — cài thủ công</summary>
            OpenUrl,
        }

        private class PackageDef
        {
            public string DisplayName;
            public string Description;
            public bool Required;

            /// <summary>Tên assembly để detect xem package đã cài chưa</summary>
            public string AssemblyName;

            /// <summary>
            /// Nếu set: coi là đã cài khi tìm thấy type này trong bất kỳ assembly đã load (vd GameAnalytics .unitypackage → Assembly-CSharp).
            /// Vẫn kết hợp với <see cref="AssemblyName"/> nếu có assembly UPM riêng.
            /// </summary>
            public string InstalledTypeFullName;

            /// <summary>
            /// Nếu set: coi là đã cài khi asset/folder này tồn tại trong project
            /// (hữu ích cho các adapter import bằng .unitypackage/.zip).
            /// </summary>
            public string InstalledAssetPath;

            /// <summary>
            /// Danh sách asset path có thể xóa để gỡ package khỏi project.
            /// Dùng cho nút "Gỡ package" trong installer.
            /// </summary>
            public string[] RemoveAssetPaths;

            /// <summary>Đánh dấu package là adapter thuộc AdMob Mediation.</summary>
            public bool IsAdMobMediationAdapter;

            public InstallMethod Method;

            // Git URL (dùng khi Method == GitUrl)
            public string GitUrl;

            // Scoped registry (dùng khi Method == ScopedRegistry)
            public string RegistryName;
            public string RegistryUrl;
            public string[] RegistryScopes;
            public string PackageId;

            /// <summary>
            /// Danh sách file .unitypackage trong thư mục Packages~.
            /// Hỗ trợ subfolder: vd "Firebase/FirebaseAnalytics.unitypackage".
            /// Tất cả file sẽ được import theo thứ tự.
            /// </summary>
            public string[] BundledFileNames;

            /// <summary>
            /// URL để tải từng file tương ứng với BundledFileNames.
            /// Dùng khi file không có trong Packages~ (vd: cài từ .unitypackage).
            /// Index phải khớp 1-1 với BundledFileNames.
            /// </summary>
            public string[] HostedUrls;

            // URL trang tải thủ công (fallback cuối khi cả local lẫn hosted URL đều thất bại)
            public string DownloadUrl;
            public string DownloadLabel;

            /// <summary>
            /// Đường dẫn asset (vd Assets/FacebookSDK/Examples) sẽ xóa ngay sau khi import .unitypackage
            /// (bỏ sample/examples gây lỗi compile hoặc không cần trong production).
            /// </summary>
            public string[] DeleteAssetPathsAfterImport;

            /// <summary>
            /// Thứ tự cài khuyến nghị (số nhỏ trước): Facebook → Firebase (EDM) → AdMob/LevelPlay → adapters AdMob → AppsFlyer → GameAnalytics.
            /// Batch install, import sau download và danh sách UI đều sort theo trường này.
            /// </summary>
            public int InstallPriority;

            // ── Runtime state ──
            public bool IsInstalled;
            public bool IsInstalling;
            public string InstallError;
        }

        // ─── Thay đổi URL ở đây khi cập nhật phiên bản SDK ─────────────────────────
        // Đặt file vào Assets/GameUpSDK/Packages~/ để dùng local (Git URL install).
        // Nếu không có file local, installer tự download từ HostedUrls (unitypackage install).

        private static readonly PackageDef[] s_packages =
        {
            new PackageDef
            {
                DisplayName = "Facebook Unity SDK 18.0.0",
                Description =
                    "Bắt buộc. Facebook SDK cho Unity (login, sharing, v.v.). Khi cài qua installer, thư mục Examples sẽ tự bỏ để tránh lỗi.",
                Required = true,
                // Facebook.Unity.dll thường tắt trên Editor; assembly Editor luôn load khi đã import SDK.
                AssemblyName = "Facebook.Unity.Editor",
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "Facebook/facebook-unity-sdk-18.0.0.unitypackage" },
                HostedUrls = new[]
                {
                    "https://github.com/DuyOhze119/sdk-gameup/releases/download/deps/facebook-unity-sdk-18.0.0.unitypackage",
                },
                DownloadUrl = "https://developers.facebook.com/docs/unity/downloads/",
                DownloadLabel = "Tải Facebook Unity SDK →",
                DeleteAssetPathsAfterImport = new[] { "Assets/FacebookSDK/Examples" },
                RemoveAssetPaths = new[] { "Assets/FacebookSDK" },
                InstallPriority = 10,
            },
            new PackageDef
            {
                DisplayName = "Firebase SDK  (Analytics + Crashlytics + Remote Config)",
                Description = "Bắt buộc. Analytics, crash reporting, remote configuration. Bao gồm EDM4U.",
                Required = false,
                AssemblyName = "Firebase.App",
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[]
                {
                    "Firebase/FirebaseAnalytics.unitypackage",
                    "Firebase/FirebaseCrashlytics.unitypackage",
                    "Firebase/FirebaseRemoteConfig.unitypackage",
                },
                HostedUrls = new[]
                {
                    "https://github.com/DuyOhze119/sdk-gameup/releases/download/deps/FirebaseAnalytics.unitypackage",
                    "https://github.com/DuyOhze119/sdk-gameup/releases/download/deps/FirebaseCrashlytics.unitypackage",
                    "https://github.com/DuyOhze119/sdk-gameup/releases/download/deps/FirebaseRemoteConfig.unitypackage",
                },
                DownloadUrl = "https://firebase.google.com/docs/unity/setup",
                DownloadLabel = "Tải Firebase Unity SDK →",
                RemoveAssetPaths = new[] { "Assets/Firebase" },
                InstallPriority = 20,
            },
            new PackageDef
            {
                DisplayName = "Google Mobile Ads — AdMob",
                Description = "Bắt buộc nếu dùng AdMob standalone (Interstitial/Rewarded/AppOpen) hoặc muốn bắt paid event để log ad_impression.",
                Required = false,
                AssemblyName = "GoogleMobileAds",
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "GoogleMobileAds-v10.7.0.unitypackage" },
                HostedUrls = new[]
                {
                    "https://github.com/DuyOhze119/sdk-gameup/releases/download/deps/GoogleMobileAds-v10.7.0.unitypackage",
                },
                DownloadUrl = "https://github.com/googlesamples/unity-admob-sdk/releases",
                DownloadLabel = "Tải AdMob Plugin →",
                RemoveAssetPaths = new[] { "Assets/GoogleMobileAds" },
                InstallPriority = 30,
            },
            new PackageDef
            {
                DisplayName = "IronSource LevelPlay SDK",
                Description = "Tùy chọn. Cần nếu bạn chọn Primary Mediation = LevelPlay trong AdsManager.",
                Required = false,
                AssemblyName = "Unity.LevelPlay",
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "UnityLevelPlay_v9.2.0.unitypackage" },
                HostedUrls = new[]
                {
                    "https://github.com/DuyOhze119/sdk-gameup/releases/download/deps/UnityLevelPlay_v9.2.0.unitypackage",
                },
                DownloadUrl = "https://developers.is.com/ironsource-mobile/unity/unity-plugin/",
                DownloadLabel = "Tải IronSource SDK →",
                RemoveAssetPaths = new[] { "Assets/LevelPlay" },
                InstallPriority = 30,
            },
            new PackageDef
            {
                DisplayName = "AppLovin MAX Unity Plugin 8.6.3",
                Description =
                    "Tùy chọn. Bắt buộc khi Primary Mediation = Max. Android/iOS SDK 13.6.2. Define: MAXSDK_DEPENDENCIES_INSTALLED.",
                Required = false,
                AssemblyName = "MaxSdk.Scripts",
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "AppLovin-MAX-Unity-Plugin-8.6.3-Android-13.6.2-iOS-13.6.2.unitypackage" },
                HostedUrls = new[]
                {
                    "https://github.com/AppLovin/AppLovin-MAX-Unity-Plugin/releases/download/release_8_6_3/AppLovin-MAX-Unity-Plugin-8.6.3-Android-13.6.2-iOS-13.6.2.unitypackage",
                },
                DownloadUrl = "https://github.com/AppLovin/AppLovin-MAX-Unity-Plugin/releases",
                DownloadLabel = "AppLovin MAX releases (GitHub) →",
                RemoveAssetPaths = new[] { "Assets/MaxSdk" },
                InstallPriority = 33,
            },
            new PackageDef
            {
                // Firebase gồm 3 file riêng trong subfolder Firebase/
                // EDM4U (Google.VersionHandler) được bundle kèm trong FirebaseAnalytics
                DisplayName      = "AppsFlyer Attribution SDK",
                Description      = "Tùy chọn. Mobile measurement & attribution.",
                Required         = false,
                AssemblyName     = "AppsFlyer",
                Method           = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "appsflyer-unity-plugin-6.17.81.unitypackage" },
                HostedUrls       = new[]
                {
                    "https://github.com/DuyOhze119/sdk-gameup/releases/download/deps/appsflyer-unity-plugin-6.17.81.unitypackage",
                },
                DownloadUrl      = "https://github.com/AppsFlyerSDK/appsflyer-unity-plugin/releases",
                DownloadLabel    = "Tải AppsFlyer SDK →",
                RemoveAssetPaths = new[] { "Assets/AppsFlyer" },
                InstallPriority = 45,
            },
             new PackageDef
            {
                DisplayName = "GameAnalytics SDK",
                Description = "Tùy chọn. Analytics sản phẩm (funnels, progression). GameUpAnalytics gửi design event (tiền tố gameup:) khi bật define GAMEANALYTICS_DEPENDENCIES_INSTALLED. Cần GameObject GameAnalytics + keys trong scene (Window → GameAnalytics).",
                Required = false,
                AssemblyName = "GameAnalyticsSDK",
                InstalledTypeFullName = "GameAnalyticsSDK.GameAnalytics",
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "GameAnalytics.unitypackage" },
                HostedUrls = new[]
                {
                    "https://github.com/ohze/gameup-unity-template/releases/download/deps/GameAnalytics.unitypackage",
                },
                DownloadUrl = "https://github.com/ohze/gameup-unity-template/releases/download/deps/GameAnalytics.unitypackage",
                DownloadLabel = "GameAnalytics Unity SDK →",
                RemoveAssetPaths = new[] { "Assets/GameAnalytics" },
                InstallPriority = 46,
            },

            new PackageDef
            {
                DisplayName = "Appmetrica SDK",
                Description = "Tùy chọn. Analytics sản phẩm thay thế Firebase",
                Required = false,
                AssemblyName = "AppMetrica",
                InstalledTypeFullName = "AppMetrica",
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "Appmetrica.unitypackage" },
                HostedUrls = new[]
                {
                    "https://github.com/DuyOhze119/sdk-gameup/releases/download/deps/Appmetrica.unitypackage",
                },
                DownloadUrl = "https://github.com/DuyOhze119/sdk-gameup/releases/download/deps/Appmetrica.unitypackage",
                DownloadLabel = "Appmetrica SDK →",
                RemoveAssetPaths = new[] { "Assets/Appmetrica" },
                InstallPriority = 60,
            },

            new PackageDef
            {
                DisplayName = "AdMob Adapter — AppLovin",
                Description = "Adapter mediation cho AppLovin (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.AppLovin.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/AppLovin",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "AppLovinUnityAdapter-8.7.2.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/applovin/AppLovinUnityAdapter-8.7.2.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/applovin",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — Chartboost",
                Description = "Adapter mediation cho Chartboost (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.Chartboost.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/Chartboost",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "ChartboostUnityAdapter-4.11.3.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/chartboost/ChartboostUnityAdapter-4.11.3.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/chartboost",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — DT Exchange",
                Description = "Adapter mediation cho DT Exchange (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.DTExchange.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/DTExchange",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "DTExchangeUnityAdapter-3.5.7.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/dtexchange/DTExchangeUnityAdapter-3.5.7.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/dt-exchange",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — i-mobile",
                Description = "Adapter mediation cho i-mobile (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.iMobile.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/iMobile",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "imobileUnityAdapter-1.3.11.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/imobile/imobileUnityAdapter-1.3.11.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/imobile",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — InMobi",
                Description = "Adapter mediation cho InMobi (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.InMobi.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/InMobi",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "InMobiUnityAdapter-5.1.0.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/inmobi/InMobiUnityAdapter-5.1.0.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/inmobi",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — IronSource Ads",
                Description = "Adapter mediation cho IronSource Ads (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.IronSource.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/IronSource",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "IronSourceUnityAdapter-4.5.0.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/ironsource/IronSourceUnityAdapter-4.5.0.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/ironsource",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — Liftoff Monetize",
                Description = "Adapter mediation cho Liftoff Monetize (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.LiftoffMonetize.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/LiftoffMonetize",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "LiftoffMonetizeUnityAdapter-5.7.2.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/liftoffmonetize/LiftoffMonetizeUnityAdapter-5.7.2.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/liftoff-monetize",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — LINE Ads Network",
                Description = "Adapter mediation cho LINE Ads Network (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.Line.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/Line",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "LineUnityAdapter-2.1.0.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/line/LineUnityAdapter-2.1.0.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/line",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — maio",
                Description = "Adapter mediation cho maio (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.Maio.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/Maio",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "MaioUnityAdapter-3.1.6.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/maio/MaioUnityAdapter-3.1.6.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/maio",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — Meta Audience Network",
                Description = "Adapter mediation cho Meta Audience Network (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.Meta.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/Meta",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "MetaAudienceNetworkUnityAdapter-3.18.4.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/meta/MetaAudienceNetworkUnityAdapter-3.18.4.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/meta",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — Mintegral",
                Description = "Adapter mediation cho Mintegral (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.Mintegral.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/Mintegral",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "MintegralUnityAdapter-2.2.0.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/mintegral/MintegralUnityAdapter-2.2.0.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/mintegral",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — Moloco",
                Description = "Adapter mediation cho Moloco (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.Moloco.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/Moloco",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "MolocoUnityAdapter-3.5.1.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/moloco/MolocoUnityAdapter-3.5.1.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/moloco",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — myTarget",
                Description = "Adapter mediation cho myTarget (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.MyTarget.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/MyTarget",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "myTargetUnityAdapter-3.35.0.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/mytarget/myTargetUnityAdapter-3.35.0.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/mytarget",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — Pangle",
                Description = "Adapter mediation cho Pangle (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.Pangle.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/Pangle",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "PangleUnityAdapter-5.9.2.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/pangle/PangleUnityAdapter-5.9.2.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/pangle",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — PubMatic OpenWrap",
                Description = "Adapter mediation cho PubMatic OpenWrap (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.PubMatic.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/PubMatic",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "PubMaticUnityAdapter-2.0.1.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/pubmatic/PubMaticUnityAdapter-2.0.1.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/pubmatic",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
            new PackageDef
            {
                DisplayName = "AdMob Adapter — Unity Ads",
                Description = "Adapter mediation cho Unity Ads (Unity).",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.UnityAds.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/UnityAds",
                IsAdMobMediationAdapter = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "UnityAdsUnityAdapter-3.17.0.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/unity/UnityAdsUnityAdapter-3.17.0.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/unity",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 35,
            },
        };


        // ─── State ────────────────────────────────────────────────────────────────

        private Vector2 _scroll;
        private bool _isRefreshing;
        private bool _isBatchInstalling;

        /// <summary>Package sẽ cài trong lần batch hiện tại (null = toàn bộ s_packages — chỉ dùng nội bộ).</summary>
        private List<PackageDef> _batchScope;

        /// <summary>Bật khi batch bắt đầu từ &quot;Cài tất cả&quot; — gợi ý menu Ensure GameAnalytics asmdef khi xong.</summary>
        private bool _gameAnalyticsSetupHintAfterBatch;
        private bool _wasCompiling;
        private bool _wasBusy;

        // Queue PackageManager (GitUrl / ScopedRegistry)
        private readonly Queue<PackageDef> _installQueue = new Queue<PackageDef>();
        private AddRequest _currentAddRequest;
        private PackageDef _currentInstallingPackage;

        // ── Parallel download state ──
        private class DownloadTask
        {
            public PackageDef Pkg;
            public string FileName;
            public string TempPath;
            public UnityWebRequest Request;
            public bool IsDone;
            public bool HasError;
            public string ErrorMessage;
        }

        private List<DownloadTask> _parallelTasks;
        private Action _parallelDoneCallback;
        private float _downloadProgress;
        private string _downloadStatus;

        // Kept for backward compat with OnDisable / PollDownloadQueue references — sẽ không dùng nữa
        private UnityWebRequest _activeDownload;
        private PackageDef _downloadingPkg;
        private bool _foldoutUgUiPackageCacheHelp;
        private bool _foldoutAdMobMediationAdapters = true;
        private WindowTab _activeTab = WindowTab.SetupDependencies;
        private const string LevelPlayDepsDefine = GUDefinetion.LevelPlayDepsInstalled;
        private const string MaxSdkDepsDefine = GUDefinetion.MaxDepsInstalled;
        private const string AdMobDepsDefine = GUDefinetion.AdMobDepsInstalled;
        private const string FirebaseDepsDefine = GUDefinetion.FirebaseDepsInstalled;
        private const string AppsFlyerDepsDefine = GUDefinetion.AppsFlyerDepsInstalled;
        private const string GameAnalyticsDepsDefine = GUDefinetion.GameAnalyticsDepsInstalled;
        private const string FacebookDepsDefine = GUDefinetion.FacebookDepsInstalled;
        private const string AppmetricaDepsDefine = GUDefinetion.AppMetricaDepsInstalled;
        private const string AdMobReleaseApiUrl = "https://api.github.com/repos/googleads/googleads-mobile-unity/releases/latest";
        private const string AdMobUnityPackagePrefix = "GoogleMobileAds-v";
        private const string AdMobUnityPackageSuffix = ".unitypackage";
        private UnityWebRequest _admobLatestReleaseRequest;
        private string[] _admobUpdateOriginalHostedUrls;
        private string[] _admobUpdateOriginalBundledFileNames;

        // ─── Static helpers ───────────────────────────────────────────────────────

        private static int PackageIndexInCatalog(PackageDef pkg)
        {
            for (int i = 0; i < s_packages.Length; i++)
            {
                if (ReferenceEquals(s_packages[i], pkg))
                    return i;
            }

            return int.MaxValue;
        }

        private static bool IsGameAnalyticsSdkPackage(PackageDef pkg) =>
            pkg != null && string.Equals(pkg.AssemblyName, "GameAnalyticsSDK", StringComparison.OrdinalIgnoreCase);

        private static IEnumerable<PackageDef> OrderedInstallSequence(IEnumerable<PackageDef> items)
        {
            return items.OrderBy(p => p.InstallPriority).ThenBy(PackageIndexInCatalog);
        }

        private static IEnumerable<PackageDef> GetAdMobMediationAdapters()
        {
            return s_packages.Where(p => p.IsAdMobMediationAdapter);
        }

        [MenuItem("GameUp/SDK/Setup Dependencies")]
        public static void ShowWindow()
        {
            var win = GetWindow<GameUpDependenciesWindow>(true, "GameUp SDK — Setup Dependencies");
            win.minSize = new Vector2(560, 520);
            // Đặt kích thước ban đầu nếu window chưa được mở
            if (win.position.width < 560)
                win.position = new Rect(win.position.x, win.position.y, 620, 580);
            win.RefreshStatus();
        }

        /// <summary>
        /// Xóa <c>Library/PackageCache</c> và <c>Library/ScriptAssemblies</c> để Unity tải lại
        /// <c>com.unity.ugui</c> khớp với bản Editor (tránh lỗi GraphicRaycaster / Dropdown / ListPool).
        /// </summary>
        [MenuItem("GameUp/SDK/Troubleshooting/Fix Unity UI package cache (com.unity.ugui errors)…", false, 50)]
        public static void MenuRepairUnityPackageCache()
        {
            RepairUnityPackageCacheWithConfirmation();
        }

        /// <summary>
        /// Hiển thị hộp thoại xác nhận rồi xóa cache; dùng chung cho menu và nút trong cửa sổ Setup Dependencies.
        /// </summary>
        public static void RepairUnityPackageCacheWithConfirmation()
        {
            if (!EditorUtility.DisplayDialog(
                    "GameUp SDK — Sửa cache gói Unity UI",
                    "Sẽ xóa thư mục:\n• Library/PackageCache\n• Library/ScriptAssemblies\n\n" +
                    "Unity sẽ tải lại các gói (gồm com.unity.ugui) cho khớp với phiên bản Editor hiện tại. " +
                    "Dùng khi Console báo lỗi compile trong PackageCache (vd. GraphicRaycaster, Dropdown, ListPool).\n\n" +
                    "Đóng các ứng dụng khác đang giữ file trong Library (hiếm). Tiếp tục?",
                    "Xóa cache",
                    "Hủy"))
                return;

            if (!TryDeleteUnityLibraryPackageCaches(out string err))
            {
                EditorUtility.DisplayDialog("GameUp SDK — Không xóa được cache", err, "OK");
                return;
            }

            EditorUtility.DisplayDialog(
                "GameUp SDK — Đã xóa cache",
                "Đã xóa PackageCache và ScriptAssemblies. Unity sẽ resolve package và compile lại.\n\n" +
                "Nếu vẫn lỗi: đóng Unity hoàn toàn, xóa cả thư mục Library, mở lại project bằng đúng Unity trong ProjectSettings/ProjectVersion.txt.",
                "OK");

            AssetDatabase.Refresh();
            Client.Resolve();
            CompilationPipeline.RequestScriptCompilation();
        }

        /// <summary>Xóa PackageCache + ScriptAssemblies; trả false và message nếu thất bại.</summary>
        internal static bool TryDeleteUnityLibraryPackageCaches(out string errorMessage)
        {
            errorMessage = null;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string packageCache = Path.Combine(projectRoot, "Library", "PackageCache");
                string scriptAssemblies = Path.Combine(projectRoot, "Library", "ScriptAssemblies");

                if (Directory.Exists(packageCache))
                    FileUtil.DeleteFileOrDirectory(packageCache);
                if (Directory.Exists(scriptAssemblies))
                    FileUtil.DeleteFileOrDirectory(scriptAssemblies);

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra nhanh (đồng bộ) xem tất cả package bắt buộc đã cài chưa.
        /// Dùng bởi GameUpPackageInstaller để quyết định có mở window không.
        /// </summary>
        public static bool AreAllRequiredPackagesInstalled()
        {
            return s_packages
                .Where(p => p.Required)
                .All(IsPackageInstalled);
        }

        // ─── Lifecycle ────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            RefreshStatus();
            _wasCompiling = EditorApplication.isCompiling;
            _wasBusy = IsInstallOrDownloadBusy();
            EditorApplication.update += EditorUpdateRepaintWhenBusy;

            // Khi đổi Scripting Define Symbols, Unity sẽ trigger compile + domain reload.
            // Rely vào _wasCompiling đôi khi miss edge (window bị recreate sau reload),
            // nên subscribe thêm các events này để luôn refresh UI/state sau khi compile/reload xong.
            AssemblyReloadEvents.afterAssemblyReload -= AfterAssemblyReloadRefresh;
            AssemblyReloadEvents.afterAssemblyReload += AfterAssemblyReloadRefresh;
            CompilationPipeline.compilationFinished -= OnCompilationFinishedRefresh;
            CompilationPipeline.compilationFinished += OnCompilationFinishedRefresh;
            AssetDatabase.importPackageCompleted -= OnImportPackageRefresh;
            AssetDatabase.importPackageCompleted += OnImportPackageRefresh;
            AssetDatabase.importPackageFailed -= OnImportPackageFailedRefresh;
            AssetDatabase.importPackageFailed += OnImportPackageFailedRefresh;
            AssetDatabase.importPackageCancelled -= OnImportPackageRefresh;
            AssetDatabase.importPackageCancelled += OnImportPackageRefresh;

            // Sau domain reload/restore window, timing load assemblies có thể trễ hơn compilationFinished.
            // DelayCall 1 nhịp là đủ để tránh scan quá sớm.
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                RefreshStatus();
            };
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdateRepaintWhenBusy;
            EditorApplication.update -= PollInstallQueue;
            EditorApplication.update -= PollParallelDownloads;
            AssemblyReloadEvents.afterAssemblyReload -= AfterAssemblyReloadRefresh;
            CompilationPipeline.compilationFinished -= OnCompilationFinishedRefresh;
            AssetDatabase.importPackageCompleted -= OnImportPackageRefresh;
            AssetDatabase.importPackageFailed -= OnImportPackageFailedRefresh;
            AssetDatabase.importPackageCancelled -= OnImportPackageRefresh;
            if (_parallelTasks != null)
            {
                foreach (var t in _parallelTasks)
                    t.Request?.Dispose();
                _parallelTasks = null;
            }

            _activeDownload?.Dispose();
            _activeDownload = null;
            _admobLatestReleaseRequest?.Dispose();
            _admobLatestReleaseRequest = null;
        }

        private void AfterAssemblyReloadRefresh()
        {
            // DelayCall để đảm bảo assemblies đã available đầy đủ trước khi scan IsAssemblyLoaded.
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                RefreshStatus();
            };
        }

        private void OnCompilationFinishedRefresh(object _)
        {
            // compilationFinished có thể bắn khi window vừa được recreate,
            // nên chỉ cần schedule refresh + repaint an toàn.
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                RefreshStatus();
            };
        }

        private void OnImportPackageRefresh(string _)
        {
            // ImportPackage hoàn thành có thể trigger refresh/compile; delayCall để tránh scan quá sớm.
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                RefreshStatus();
            };
        }

        private void OnImportPackageFailedRefresh(string _, string __)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                RefreshStatus();
            };
        }

        /// <summary>Làm mới UI khi đang compile hoặc đang cài để nút bật/tắt đúng lúc compile xong.</summary>
        private void EditorUpdateRepaintWhenBusy()
        {
            bool compiling = EditorApplication.isCompiling;
            bool busy = IsInstallOrDownloadBusy();

            // Compile vừa kết thúc → assemblies đã reload, refresh trạng thái package một lần.
            if (_wasCompiling && !compiling)
                RefreshStatus();

            // Khi các job cài/tải vừa kết thúc, refresh lại ngay để bật lại nút gỡ/cài.
            if (_wasBusy && !busy)
                RefreshStatus();

            bool stateChanged = (_wasCompiling != compiling) || (_wasBusy != busy);
            _wasCompiling = compiling;
            _wasBusy = busy;

            if (compiling || busy || stateChanged)
                Repaint();
        }

        private bool IsInstallOrDownloadBusy()
        {
            bool hasQueueItems = _installQueue.Count > 0;
            bool hasRunningAddRequest = _currentAddRequest != null && !_currentAddRequest.IsCompleted;
            bool hasResolvingAdMobLatest = _admobLatestReleaseRequest != null && !_admobLatestReleaseRequest.isDone;
            bool hasRunningParallelDownloads = _parallelTasks?.Any(t => !t.IsDone && t.Request != null && !t.Request.isDone) == true;
            bool hasInstallingPackage = s_packages.Any(p => p.IsInstalling);

            // Tự phục hồi cờ batch nếu không còn tác vụ nào thực sự chạy.
            if (_isBatchInstalling
                && !hasQueueItems
                && !hasRunningAddRequest
                && !hasResolvingAdMobLatest
                && !hasRunningParallelDownloads
                && !hasInstallingPackage)
            {
                _isBatchInstalling = false;
                _batchScope = null;
            }

            return _isBatchInstalling
                   || hasQueueItems
                   || hasRunningAddRequest
                   || hasResolvingAdMobLatest
                   || hasRunningParallelDownloads
                   || hasInstallingPackage;
        }

        /// <summary>Khóa mọi thao tác: đang compile hoặc đang cài/tải package.</summary>
        private bool IsInteractionLocked()
        {
            // Theo yêu cầu: luôn mở UI để tránh trường hợp trạng thái nút không kịp cập nhật sau compile/import.
            return false;
        }

        // ─── GUI ─────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (IsInstallOrDownloadBusy())
            {
                EditorGUILayout.HelpBox("Đang xử lý cài/tải dependency…", MessageType.Info);
                EditorGUILayout.Space(4);
            }

            DrawHeader();
            DrawTabToolbar();

            if (_activeTab == WindowTab.SetupDependencies)
            {
                DrawSetupDependenciesTab();
            }
            else
            {
                DrawAdMobMediationTab();
            }
        }

        private void DrawTabToolbar()
        {
            EditorGUILayout.Space(2);
            _activeTab = (WindowTab)GUILayout.Toolbar(
                (int)_activeTab,
                new[] { "Setup Dependencies", "AdMob Mediation" });
            EditorGUILayout.Space(6);
        }

        private void DrawSetupDependenciesTab()
        {
            float totalWidth = Mathf.Max(720f, position.width - 20f);
            float leftWidth = Mathf.Clamp(totalWidth * 0.42f, 320f, 520f);
            float rightWidth = Mathf.Max(300f, totalWidth - leftWidth - 10f);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(leftWidth), GUILayout.ExpandHeight(true));
            DrawMediationInfo();
            DrawUgUiPackageCacheTroubleshootFoldout();
            DrawFacebookExamplesCleanupSection();
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical("box", GUILayout.Width(rightWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Package Installer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Danh sách package liên quan và thao tác cài/gỡ.", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawPackageList(includeAdMobAdapters: false, allowPerPackageRemove: false);
            EditorGUILayout.EndScrollView();

            DrawSetupDependenciesBulkRemoveSection();
            DrawFooter();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private static bool HasDefine(string define)
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            return symbols.Contains(define);
        }

        private static void SetDefine(string define, bool enabled)
        {
            foreach (var group in s_buildTargetGroups)
            {
                try
                {
                    string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                    var list = new List<string>(
                        current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

                    bool changed = false;

                    if (enabled && !list.Contains(define))
                    {
                        list.Add(define);
                        changed = true;
                    }
                    else if (!enabled && list.Remove(define))
                    {
                        changed = true;
                    }

                    if (changed)
                    {
                        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", list));
                    }
                }
                catch { }
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField(
                "GameUp SDK — Setup Dependencies",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 });

            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "Có thể cài nhanh bằng \"Cài tất cả\" trong khung Mediation (chỉ các pack cần thiết theo Primary Mediation), hoặc cài từng bước bằng \"Cài pack\" trên từng dòng — nên chờ Unity compile (và EDM/Android Resolver nếu bật) giữa các bước khi cài lẻ.\n" +
                "AdMob Mediation adapters cài thủ công trong tab \"AdMob Mediation\" (không nằm trong \"Cài tất cả\").\n" +
                "Khi đang compile hoặc đang cài/tải, nút Mediation và \"Cài pack\" đều bị khóa.",
                MessageType.Info);

            EditorGUILayout.Space(6);
        }

        private void DrawMediationInfo()
        {
            EditorGUILayout.Space(6);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Mediation Settings", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(IsInteractionLocked());
            var current = GetPrimaryMediationFromDefines();
            var next = (AdsManager.PrimaryMediation)EditorGUILayout.EnumPopup("Primary Mediation", current);
            if (next != current)
            {
                SetPrimaryMediationDefines(next);
                RefreshStatus();
            }

            EditorGUI.EndDisabledGroup();

            var pm = GetPrimaryMediationFromDefines();
            var planned = GetPackagesForSdkSetup(pm);
            var missingAuto = planned.Where(p => !p.IsInstalled && CanAutoInstall(p)).ToList();
            var missingManual = planned.Where(p => !p.IsInstalled && !CanAutoInstall(p)).ToList();

            string planDesc = pm switch
            {
                AdsManager.PrimaryMediation.AdMob =>
                    "Facebook, Firebase, AppsFlyer, GameAnalytics, Google Mobile Ads.",
                AdsManager.PrimaryMediation.Max =>
                    "Facebook, Firebase, AppsFlyer, GameAnalytics, AppLovin MAX.",
                _ => "Facebook, Firebase, AppsFlyer, GameAnalytics, IronSource LevelPlay.",
            };

            EditorGUILayout.HelpBox(
                "Primary Mediation chọn bộ pack quảng cáo cốt lõi (AdMob, LevelPlay hay MAX). " +
                "Dùng nút bên dưới để cài một lần mọi mục còn thiếu trong bộ đó (đúng thứ tự), hoặc \"Cài pack\" từng dòng trong danh sách.\n" +
                "Bộ theo mediation hiện tại: " + planDesc,
                MessageType.Info);

            EditorGUILayout.HelpBox(
                "Thứ tự nên cài: (1) Facebook → (2) Firebase (kèm EDM) — chờ compile/resolve xong — → " +
                "(3) Google Mobile Ads, LevelPlay hoặc MAX (trùng với Primary Mediation) → " +
                "(4) AppsFlyer → (5) GameAnalytics. " +
                "AdMob Mediation adapters (nếu cần) cài riêng trong tab \"AdMob Mediation\" — không khuyến nghị cài hàng loạt.",
                MessageType.None);

            if (missingManual.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Có package không cài tự động được (thiếu file trong Packages~ và không có URL tải). Cần tải/import thủ công theo mô tả từng dòng.",
                    MessageType.Warning);
            }

            if (missingAuto.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Còn {missingAuto.Count} mục có thể cài tự động — bấm \"Cài tất cả\" hoặc \"Cài pack\" lần lượt từ trên xuống trong danh sách.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Theo Primary Mediation, không còn mục nào thiếu mà installer tự cài được (hoặc đã đủ).",
                    MessageType.None);
            }

            EditorGUI.BeginDisabledGroup(IsInteractionLocked() || missingAuto.Count == 0);
            if (GUILayout.Button(
                    missingAuto.Count > 0
                        ? $"⬇ Cài tất cả còn thiếu ({missingAuto.Count}) — theo thứ tự khuyến nghị"
                        : "✓ Đã đủ package (tự động) cho Primary Mediation",
                    GUILayout.Height(28)))
            {
                if (missingAuto.Count > 0)
                    StartBatchInstall(planned, showGameAnalyticsSetupHintWhenComplete: true);
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.HelpBox(
                "AdMob Mediation adapters nằm trong tab \"AdMob Mediation\" — tự chọn và cài từng adapter theo network bạn dùng.",
                MessageType.None);

            EditorGUILayout.HelpBox(
                "Primary Mediation lưu bằng Scripting Define Symbols (`" + GUDefinetion.PrimaryMediationLevelPlay + "` / `" + GUDefinetion.PrimaryMediationAdMob + "`) — phù hợp khi GameUp SDK cài dạng UPM package (không tạo asset trong Assets/).",
                MessageType.Info
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawUgUiPackageCacheTroubleshootFoldout()
        {
            EditorGUILayout.Space(4);
            _foldoutUgUiPackageCacheHelp = EditorGUILayout.Foldout(
                _foldoutUgUiPackageCacheHelp,
                "Gỡ lỗi: lỗi compile trong com.unity.ugui (PackageCache)",
                true);

            if (!_foldoutUgUiPackageCacheHelp)
                return;

            EditorGUILayout.HelpBox(
                "Nếu Console báo lỗi trong Library/PackageCache/com.unity.ugui (vd. GraphicRaycaster, Dropdown, ListPool): " +
                "đó thường do cache gói Unity lệch với bản Editor — không phải do mã GameUp SDK. " +
                "Mở project luôn bằng đúng phiên bản Unity trong ProjectSettings/ProjectVersion.txt.",
                MessageType.Warning);

            if (GUILayout.Button("Xóa Package Cache + ScriptAssemblies (tải lại gói Unity UI)", GUILayout.Height(26)))
                RepairUnityPackageCacheWithConfirmation();
        }

        private const string FacebookExamplesAssetPath = "Assets/FacebookSDK/Examples";

        private static bool FacebookSdkExamplesFolderExists()
        {
            return AssetDatabase.IsValidFolder(FacebookExamplesAssetPath);
        }

        private void DrawFacebookExamplesCleanupSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Facebook SDK — Examples", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Thư mục Examples thường không cần cho production và có thể gây lỗi compile. " +
                "Khi cài Facebook qua installer, Examples đã được xóa tự động; nếu bạn import tay hoặc còn sót, dùng nút bên dưới.",
                MessageType.None);

            EditorGUI.BeginDisabledGroup(IsInstallOrDownloadBusy() || !FacebookSdkExamplesFolderExists());
            if (GUILayout.Button("Xóa thủ công: Assets/FacebookSDK/Examples", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog(
                        "GameUp SDK — Xóa Facebook Examples",
                        "Xóa toàn bộ thư mục Assets/FacebookSDK/Examples?\n\n" +
                        "SDK Facebook chính (ngoài Examples) không bị gỡ. Có thể hoàn tác qua Git/VCS nếu cần.",
                        "Xóa",
                        "Hủy"))
                {
                    TryDeleteFacebookExamplesFolder();
                    Repaint();
                }
            }

            EditorGUI.EndDisabledGroup();

            if (!FacebookSdkExamplesFolderExists())
            {
                EditorGUILayout.LabelField(
                    "Không thấy thư mục (đã xóa hoặc chưa import Facebook SDK).",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>Xóa <c>Assets/FacebookSDK/Examples</c> qua AssetDatabase (nút thủ công trong installer).</summary>
        internal static void TryDeleteFacebookExamplesFolder()
        {
            if (!FacebookSdkExamplesFolderExists())
            {
                EditorUtility.DisplayDialog(
                    "GameUp SDK",
                    "Không có thư mục " + FacebookExamplesAssetPath + ".",
                    "OK");
                return;
            }

            if (!AssetDatabase.DeleteAsset(FacebookExamplesAssetPath))
            {
                Debug.LogWarning("[GameUpSDK] Không xóa được: " + FacebookExamplesAssetPath);
                EditorUtility.DisplayDialog(
                    "GameUp SDK",
                    "Xóa thất bại. Kiểm tra Console hoặc đóng file đang mở trong thư mục đó.",
                    "OK");
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log("[GameUpSDK] Đã xóa " + FacebookExamplesAssetPath);
        }

        /// <summary>Firebase + AppsFlyer + bộ mediation cốt lõi theo lựa chọn (AdMob: GMA; LevelPlay: LevelPlay; Max: MAX). Không gồm AdMob Mediation adapters.</summary>
        private static List<PackageDef> GetPackagesForSdkSetup(AdsManager.PrimaryMediation mediation)
        {
            var list = new List<PackageDef>();

            void AddByAssembly(string assemblyName)
            {
                var p = s_packages.FirstOrDefault(x => x.AssemblyName == assemblyName);
                if (p != null && !list.Contains(p))
                    list.Add(p);
            }

            AddByAssembly("Facebook.Unity.Editor");
            AddByAssembly("Firebase.App");
            AddByAssembly("AppsFlyer");
            AddByAssembly("GameAnalyticsSDK");

            if (mediation == AdsManager.PrimaryMediation.AdMob)
            {
                AddByAssembly("GoogleMobileAds");
            }
            else if (mediation == AdsManager.PrimaryMediation.Max)
            {
                AddByAssembly("MaxSdk.Scripts");
            }
            else
            {
                AddByAssembly("Unity.LevelPlay");
            }

            return OrderedInstallSequence(list).ToList();
        }

        private static AdsManager.PrimaryMediation GetPrimaryMediationFromDefines()
        {
            if (HasDefine(GUDefinetion.PrimaryMediationAdMob)) return AdsManager.PrimaryMediation.AdMob;
            if (HasDefine(GUDefinetion.PrimaryMediationMax)) return AdsManager.PrimaryMediation.Max;
            return AdsManager.PrimaryMediation.LevelPlay;
        }

        private static void SetPrimaryMediationDefines(AdsManager.PrimaryMediation mediation)
        {
            SetDefine(GUDefinetion.PrimaryMediationAdMob, mediation == AdsManager.PrimaryMediation.AdMob);
            SetDefine(GUDefinetion.PrimaryMediationLevelPlay, mediation == AdsManager.PrimaryMediation.LevelPlay);
            SetDefine(GUDefinetion.PrimaryMediationMax, mediation == AdsManager.PrimaryMediation.Max);
        }

        /// <summary>Đảm bảo có đúng một define mediation (mặc định LevelPlay nếu chưa có).</summary>
        private static void EnsurePrimaryMediationDefines()
        {
            bool lp = HasDefine(GUDefinetion.PrimaryMediationLevelPlay);
            bool admob = HasDefine(GUDefinetion.PrimaryMediationAdMob);
            bool max = HasDefine(GUDefinetion.PrimaryMediationMax);
            int active = (lp ? 1 : 0) + (admob ? 1 : 0) + (max ? 1 : 0);
            if (active == 0)
            {
                SetDefine(GUDefinetion.PrimaryMediationLevelPlay, true);
                return;
            }

            if (active <= 1)
                return;

            if (admob)
            {
                SetDefine(GUDefinetion.PrimaryMediationLevelPlay, false);
                SetDefine(GUDefinetion.PrimaryMediationMax, false);
            }
            else if (max)
            {
                SetDefine(GUDefinetion.PrimaryMediationLevelPlay, false);
                SetDefine(GUDefinetion.PrimaryMediationAdMob, false);
            }
            else
            {
                SetDefine(GUDefinetion.PrimaryMediationAdMob, false);
                SetDefine(GUDefinetion.PrimaryMediationMax, false);
            }
        }

        private void DrawPackageList(bool includeAdMobAdapters, bool allowPerPackageRemove = true)
        {
            bool drewRequired = false, drewOptional = false;
            bool? lastRequired = null;

            foreach (var pkg in OrderedInstallSequence(s_packages))
            {
                if (!includeAdMobAdapters && pkg.IsAdMobMediationAdapter)
                    continue;

                if (lastRequired.HasValue && lastRequired.Value != pkg.Required)
                {
                    // Đường phân tách rõ giữa nhóm bắt buộc và tùy chọn.
                    EditorGUILayout.Space(3);
                    DrawSeparatorLine(new Color(1f, 1f, 1f, 0.32f), 1.2f);
                    EditorGUILayout.Space(3);
                }

                // Section headers
                if (pkg.Required && !drewRequired)
                {
                    DrawSectionHeader("BẮT BUỘC");
                    drewRequired = true;
                }

                if (!pkg.Required && !drewOptional)
                {
                    EditorGUILayout.Space(8);
                    DrawSectionHeader("TÙY CHỌN");
                    drewOptional = true;
                }

                DrawPackageRow(pkg, allowPerPackageRemove);
                lastRequired = pkg.Required;
            }
        }

        private void DrawAdMobMediationTab()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("AdMob Mediation Adapters", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Danh sách adapter AdMob theo Google. Chỉ cài adapter cho network bạn thực sự dùng — bấm \"Cài pack\" trên từng dòng. " +
                "Installer tự tải .zip, giải nén và import .unitypackage.",
                MessageType.Info);

            var pm = GetPrimaryMediationFromDefines();
            if (pm != AdsManager.PrimaryMediation.AdMob)
            {
                EditorGUILayout.HelpBox(
                    "Primary Mediation hiện không phải AdMob. Bạn vẫn có thể cài adapter trước, nhưng nên chuyển Primary Mediation = AdMob nếu muốn dùng bộ này.",
                    MessageType.Warning);
            }

            var adapters = OrderedInstallSequence(GetAdMobMediationAdapters()).ToList();
            var installedAdapterCount = adapters.Count(p => p.IsInstalled);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _foldoutAdMobMediationAdapters = EditorGUILayout.Foldout(
                _foldoutAdMobMediationAdapters,
                $"Danh sách adapter ({installedAdapterCount}/{adapters.Count} đã cài)",
                true);

            if (_foldoutAdMobMediationAdapters)
            {
                foreach (var adapter in adapters)
                    DrawPackageRow(adapter, allowPerPackageRemove: true);
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawSectionHeader(string title)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(4, 0, 6, 2),
            };
            EditorGUILayout.LabelField(title, style);
            DrawSeparatorLine(new Color(1f, 1f, 1f, 0.2f), 1f);
        }

        private static void DrawSeparatorLine(Color color, float thickness = 1f)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, thickness + 2f);
            rect.y += 1f;
            rect.height = thickness;
            EditorGUI.DrawRect(rect, color);
        }

        private void DrawPackageRow(PackageDef pkg, bool allowPerPackageRemove)
        {
            bool isDownloading = _parallelTasks?.Any(t => t.Pkg == pkg && !t.IsDone) == true;
            bool isResolvingAdMobLatest = IsAdMobPackage(pkg) && _admobLatestReleaseRequest != null;
            bool isInstalling = pkg.IsInstalling
                                || (_isBatchInstalling && _installQueue.Contains(pkg))
                                || isDownloading
                                || isResolvingAdMobLatest;
            Color boxColor = pkg.IsInstalled ? new Color(0.18f, 0.45f, 0.18f, 0.3f)
                : isInstalling ? new Color(0.3f, 0.3f, 0.6f, 0.3f)
                : new Color(0.45f, 0.18f, 0.18f, 0.3f);

            // Row background
            var rect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height + 2), boxColor);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);

            // Status icon
            string icon = pkg.IsInstalled ? "✓" : isInstalling ? "⟳" : "✗";
            var iconStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = pkg.IsInstalled ? Color.green : isInstalling ? Color.yellow : Color.red },
                fixedWidth = 24,
            };
            GUILayout.Label(icon, iconStyle, GUILayout.Width(24));

            // Name + description
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(pkg.DisplayName, EditorStyles.boldLabel);
            var descStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            EditorGUILayout.LabelField(pkg.Description, descStyle);
            if (!string.IsNullOrEmpty(pkg.InstallError))
                EditorGUILayout.HelpBox(pkg.InstallError, MessageType.Error);
            EditorGUILayout.EndVertical();

            // Trạng thái / Cài pack (từng dòng, không cài gom)
            GUILayout.Space(4);
            EditorGUILayout.BeginVertical(GUILayout.Width(190));
            if (pkg.IsInstalled)
            {
                var greenStyle = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = Color.green }, fontStyle = FontStyle.Bold };
                GUILayout.Label("Đã cài", greenStyle, GUILayout.Width(64));
            }
            else if (isResolvingAdMobLatest)
            {
                float anim = Mathf.PingPong((float)EditorApplication.timeSinceStartup * 0.9f, 1f);
                EditorGUILayout.BeginVertical(GUILayout.Width(190));
                EditorGUILayout.LabelField("Đang kiểm tra release AdMob mới nhất...", EditorStyles.miniLabel, GUILayout.Width(190));
                var barRect = EditorGUILayout.GetControlRect(GUILayout.Width(190), GUILayout.Height(6));
                EditorGUI.ProgressBar(barRect, anim, "");
                EditorGUILayout.EndVertical();
            }
            else if (isDownloading)
            {
                var pkgTasks = _parallelTasks?.Where(t => t.Pkg == pkg).ToList();
                int total = pkgTasks?.Count ?? 0;
                int done = pkgTasks?.Count(t => t.IsDone) ?? 0;
                float prog = total > 0
                    ? pkgTasks.Average(t => t.IsDone ? 1f : t.Request?.downloadProgress ?? 0f)
                    : 0f;

                EditorGUILayout.BeginVertical(GUILayout.Width(190));
                EditorGUILayout.LabelField(
                    total > 1 ? $"Đang tải... {done}/{total} files" : "Đang tải...",
                    EditorStyles.miniLabel, GUILayout.Width(190));
                var barRect = EditorGUILayout.GetControlRect(GUILayout.Width(190), GUILayout.Height(6));
                EditorGUI.ProgressBar(barRect, prog, "");
                EditorGUILayout.EndVertical();
            }
            else if (isInstalling)
            {
                EditorGUILayout.LabelField("Đang cài...", GUILayout.Width(100));
            }
            else
            {
                bool canAuto = CanAutoInstall(pkg);
                if (canAuto)
                {
                    EditorGUI.BeginDisabledGroup(IsInteractionLocked());
                    if (GUILayout.Button("Cài pack", GUILayout.Width(88), GUILayout.Height(24)))
                        StartSinglePackageInstall(pkg);
                    EditorGUI.EndDisabledGroup();
                }
                else if (pkg.Method == InstallMethod.OpenUrl)
                {
                    if (GUILayout.Button("Mở trang tải", GUILayout.Width(100), GUILayout.Height(24))
                        && !string.IsNullOrEmpty(pkg.DownloadUrl))
                        Application.OpenURL(pkg.DownloadUrl);
                }
                else
                {
                    var manualStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = true,
                        normal = { textColor = new Color(0.55f, 0.55f, 0.55f) },
                    };
                    GUILayout.Label("Cần file Packages~/URL", manualStyle, GUILayout.Width(118));
                }
            }

            if (IsAdMobPackage(pkg))
            {
                EditorGUI.BeginDisabledGroup(IsInstallOrDownloadBusy());
                if (GUILayout.Button("Update AdMob mới nhất", GUILayout.Width(190), GUILayout.Height(22)))
                    StartAdMobLatestUpdate(pkg);
                EditorGUI.EndDisabledGroup();
            }

            if (allowPerPackageRemove && pkg.IsAdMobMediationAdapter && pkg.IsInstalled)
            {
                bool canRemove = HasInstalledAssetPath(pkg);
                EditorGUI.BeginDisabledGroup(IsInstallOrDownloadBusy() || !canRemove);
                if (GUILayout.Button("Gỡ adapter", GUILayout.Width(190), GUILayout.Height(22)))
                    ConfirmAndRemoveAdMobAdapter(pkg);
                EditorGUI.EndDisabledGroup();
            }
            else if (allowPerPackageRemove && !pkg.IsAdMobMediationAdapter && pkg.IsInstalled)
            {
                bool canRemove = CanRemovePackage(pkg);
                EditorGUI.BeginDisabledGroup(IsInstallOrDownloadBusy() || !canRemove);
                if (GUILayout.Button("Gỡ package", GUILayout.Width(190), GUILayout.Height(22)))
                    ConfirmAndRemovePackage(pkg);
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();

            DrawSeparatorLine(new Color(1f, 1f, 1f, 0.14f), 1f);
            EditorGUILayout.Space(3);
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            // Nút refresh thủ công luôn active.
            // Nếu đang compile thì delay để refresh sau khi compile/reload xong.
            if (GUILayout.Button("↻  Làm mới trạng thái", GUILayout.Height(30)))
                RequestManualRefresh();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Manual install hint
            bool hasManualUninstalled = s_packages.Any(p => !p.IsInstalled && p.Method == InstallMethod.OpenUrl);
            if (hasManualUninstalled)
            {
                EditorGUILayout.HelpBox(
                    "Một số package chỉ cài được thủ công: tải .unitypackage từ trang nhà cung cấp, " +
                    "rồi Assets → Import Package → Custom Package…, sau đó \"Làm mới trạng thái\".",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(4);

            // Continue button
            bool allRequiredDone = AreAllRequiredPackagesInstalled();
            if (allRequiredDone)
            {
                EditorGUILayout.HelpBox(
                    "Tất cả package bắt buộc đã được cài đặt! " +
                    "Nhấn bên dưới để mở cửa sổ cấu hình SDK.",
                    MessageType.None);

                // Khi đang compile/cài, không cho bấm (không trigger delay-call) để đúng luồng UX.
                EditorGUI.BeginDisabledGroup(IsInteractionLocked());
                if (GUILayout.Button("→  Mở cấu hình SDK (GameUp SDK Setup)", GUILayout.Height(36)))
                    RequestOpenSetup();
                EditorGUI.EndDisabledGroup();
            }
        }

        /// <summary>
        /// Asset path/prefix thuộc GameUp Core hoặc dependencies Core — không xóa khi gỡ SDK.
        /// (vd DOTween nằm dưới Assets/Plugins/Demigiant.)
        /// </summary>
        private static readonly string[] s_coreProtectedAssetPathPrefixes =
        {
            "Assets/GameUpCore",
            "Assets/_MainProject",
            "Assets/Plugins/Demigiant",
        };

        private void DrawSetupDependenciesBulkRemoveSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Gỡ SDK dependencies", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Chỉ gỡ dependencies bên thứ ba của GameUp SDK (Facebook, Firebase, AdMob, …). " +
                "GameUp Core và dependencies Core (vd DOTween) được giữ nguyên. " +
                "Tab này hỗ trợ gỡ toàn bộ SDK dependencies một lần để tránh define/package bị lệch.",
                MessageType.Warning);

            var removablePkgs = s_packages
                .Where(p => !p.IsAdMobMediationAdapter && CanRemovePackage(p))
                .ToList();

            EditorGUI.BeginDisabledGroup(IsInstallOrDownloadBusy() || removablePkgs.Count == 0);
            if (GUILayout.Button(
                    removablePkgs.Count > 0
                        ? $"🗑 Gỡ toàn bộ SDK dependencies ({removablePkgs.Count})"
                        : "✓ Không còn SDK dependencies để gỡ",
                    GUILayout.Height(28)))
            {
                ConfirmAndRemoveSetupDependenciesBulk(removablePkgs);
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

        private void RequestManualRefresh()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += () =>
                {
                    if (this == null) return;
                    RefreshStatus();
                };
                Repaint();
                return;
            }

            RefreshStatus();
        }

        private void RequestOpenSetup()
        {
            if (IsInteractionLocked())
            {
                EditorApplication.delayCall += () =>
                {
                    if (this == null) return;
                    RequestOpenSetup();
                };
                Repaint();
                return;
            }

            GameUpPackageInstaller.MarkSetupComplete();
            Close();
            EditorApplication.ExecuteMenuItem("GameUp SDK/Setup");
        }

        // ─── Install logic ────────────────────────────────────────────────────────

        private void StartInstall(PackageDef pkg)
        {
            if (pkg.IsInstalling) return;
            pkg.IsInstalling = true;
            pkg.InstallError = null;
            Repaint();

            switch (pkg.Method)
            {
                case InstallMethod.GitUrl:
                    EnqueueGitInstall(pkg);
                    break;

                case InstallMethod.ScopedRegistry:
                    AddScopedRegistryAndPackage(pkg);
                    break;
            }
        }

        private void StartBatchInstall(IReadOnlyList<PackageDef> scope, bool showGameAnalyticsSetupHintWhenComplete = false)
        {
            _gameAnalyticsSetupHintAfterBatch = showGameAnalyticsSetupHintWhenComplete;
            _batchScope = OrderedInstallSequence(
                    scope != null && scope.Count > 0
                        ? scope.Distinct()
                        : s_packages)
                .ToList();
            _isBatchInstalling = true;
            _installQueue.Clear();

            IEnumerable<PackageDef> InScope() => _batchScope;

            // 1) Import các UnityPackage đã có file local (đồng bộ, nhanh)
            foreach (var pkg in InScope())
            {
                if (pkg.IsInstalled) continue;
                if (pkg.Method != InstallMethod.UnityPackage) continue;

                var localPaths = GetBundledPackagePaths(pkg.BundledFileNames);
                if (localPaths == null) continue;
                localPaths = ResolveImportableUnityPackages(localPaths, out var localResolveErrors);

                pkg.InstallError = null;
                if (localResolveErrors.Count > 0)
                    pkg.InstallError = "Xử lý file local thất bại:\n" + string.Join("\n", localResolveErrors);

                if (localPaths.Count > 0)
                    ImportUnityPackage(pkg, localPaths);
            }

            // 2) Cài GitUrl / ScopedRegistry (bất đồng bộ)
            foreach (var pkg in InScope())
            {
                if (pkg.IsInstalled) continue;
                if (pkg.Method != InstallMethod.GitUrl && pkg.Method != InstallMethod.ScopedRegistry) continue;

                pkg.InstallError = null;
                _installQueue.Enqueue(pkg);
            }

            // 3) Download song song; import sau khi tải xong theo InstallPriority (tránh import AdMob trước Firebase).
            var downloadPkgs = OrderedInstallSequence(
                    InScope().Where(p => !p.IsInstalled
                                         && p.Method == InstallMethod.UnityPackage
                                         && GetBundledPackagePaths(p.BundledFileNames) == null
                                         && p.HostedUrls?.Length > 0))
                .ToList();

            void FinishBatch()
            {
                _isBatchInstalling = false;
                bool hintGa = _gameAnalyticsSetupHintAfterBatch;
                _gameAnalyticsSetupHintAfterBatch = false;
                _batchScope = null;
                RefreshStatus();
                if (hintGa)
                    NotifyGameAnalyticsAsmdefHint(fromMediationInstallAllBatch: true);
            }

            if (_installQueue.Count > 0)
            {
                // GitUrl chạy trước (bất đồng bộ), download song song sau khi xong
                ProcessNextInQueueThen(() =>
                {
                    if (downloadPkgs.Count > 0)
                        StartParallelDownloadAndImport(downloadPkgs, onAllDone: FinishBatch);
                    else
                        FinishBatch();
                });
            }
            else if (downloadPkgs.Count > 0)
            {
                // Chỉ có download → chạy ngay song song
                StartParallelDownloadAndImport(downloadPkgs, onAllDone: FinishBatch);
            }
            else
            {
                FinishBatch();
            }
        }

        /// <summary>
        /// Nhắc menu Ensure GameAnalytics runtime asmdef (<see cref="GameUpDefineSymbolsAutoSync"/>).
        /// </summary>
        /// <param name="fromMediationInstallAllBatch">true khi vừa xong &quot;Cài tất cả&quot; Mediation; false khi vừa import xong GA (Cài pack).</param>
        private static void NotifyGameAnalyticsAsmdefHint(bool fromMediationInstallAllBatch)
        {
            EditorApplication.delayCall += () =>
            {
                const string menuItem = "GameUp SDK → Ensure GameAnalytics runtime asmdef";
                string intro = fromMediationInstallAllBatch
                    ? "Cài package (bộ Mediation) đã xong. "
                    : "Game Analytics SDK vừa import xong. ";
                Debug.Log(
                    "[GameUp] " + intro + "Để tạo/đảm bảo asmdef GA, chọn menu: " + menuItem + ".");

                foreach (var w in Resources.FindObjectsOfTypeAll<GameUpDependenciesWindow>())
                {
                    if (w == null)
                        continue;
                    w.ShowNotification(
                        new GUIContent("Game Analytics (asmdef): menu " + menuItem));
                }
            };
        }

        /// <summary>
        /// Cài một package — dùng chung <see cref="StartBatchInstall"/> với scope một phần tử.
        /// </summary>
        private void StartSinglePackageInstall(PackageDef pkg)
        {
            if (pkg == null || pkg.IsInstalled || !CanAutoInstall(pkg))
                return;
            if (IsInstallOrDownloadBusy())
                return;

            StartBatchInstall(new List<PackageDef> { pkg });
        }

        private void EnqueueGitInstall(PackageDef pkg)
        {
            _installQueue.Clear();
            _installQueue.Enqueue(pkg);
            ProcessNextInQueue();
        }

        private Action _onQueueDone;

        private void ProcessNextInQueueThen(Action onDone)
        {
            _onQueueDone = onDone;
            ProcessNextInQueue();
        }

        private void ProcessNextInQueue()
        {
            if (_installQueue.Count == 0)
            {
                _currentInstallingPackage = null;
                _currentAddRequest = null;
                EditorApplication.update -= PollInstallQueue;

                var cb = _onQueueDone;
                _onQueueDone = null;
                if (cb != null) cb();
                else
                {
                    _isBatchInstalling = false;
                    _batchScope = null;
                    RefreshStatus();
                }

                return;
            }

            var pkg = _installQueue.Peek();
            _currentInstallingPackage = pkg;
            pkg.IsInstalling = true;
            Repaint();

            _currentAddRequest = Client.Add(pkg.GitUrl);
            EditorApplication.update += PollInstallQueue;
        }

        private void PollInstallQueue()
        {
            if (_currentAddRequest == null || !_currentAddRequest.IsCompleted) return;

            EditorApplication.update -= PollInstallQueue;

            var pkg = _currentInstallingPackage;
            if (pkg != null)
            {
                pkg.IsInstalling = false;

                if (_currentAddRequest.Status == StatusCode.Success)
                {
                    pkg.IsInstalled = true;
                    pkg.InstallError = null;
                }
                else
                {
                    pkg.InstallError = _currentAddRequest.Error?.message ?? "Cài thất bại.";
                }
            }

            _installQueue.Dequeue();
            _currentAddRequest = null;
            _currentInstallingPackage = null;

            ProcessNextInQueue();
            Repaint();
        }

        // ─── UnityPackage install ─────────────────────────────────────────────────

        /// <summary>
        /// Trả về danh sách đường dẫn tuyệt đối cho các file .unitypackage trong Packages~.
        /// Chỉ trả về file thực sự tồn tại. Trả về null nếu KHÔNG CÓ file nào.
        /// </summary>
        private static List<string> GetBundledPackagePaths(string[] fileNames)
        {
            if (fileNames == null || fileNames.Length == 0) return null;

            var found = new List<string>();
            foreach (string name in fileNames)
            {
                string normalized = name.Replace('/', Path.DirectorySeparatorChar);

                // 1) Packages~ (khi SDK cài dạng UPM package hoặc assets-based fallback)
                string packagesFolder = GetPackagesFolder();
                if (!string.IsNullOrEmpty(packagesFolder))
                {
                    string full = Path.Combine(packagesFolder, normalized);
                    if (File.Exists(full))
                    {
                        found.Add(full);
                        continue;
                    }
                }
            }

            return found.Count > 0 ? found : null;
        }

        // Backward compat helper dùng nội bộ để check có ít nhất 1 file
        private static string GetBundledPackagePath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            string folder = GetPackagesFolder();
            if (string.IsNullOrEmpty(folder)) return null;
            string full = Path.Combine(folder, fileName.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(full) ? full : null;
        }

        /// <summary>
        /// Tìm thư mục Packages~ của package này.
        /// Hỗ trợ cả cài via UPM Git URL (resolvedPath) và .unitypackage (Assets/GameUpSDK).
        /// </summary>
        private static string GetPackagesFolder()
        {
            // Thử tìm qua PackageInfo khi cài via UPM
            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                Type pkgInfoType = Type.GetType("UnityEditor.PackageManager.PackageInfo, UnityEditor");
                if (pkgInfoType != null)
                {
                    MethodInfo findMethod = pkgInfoType.GetMethod(
                        "FindForAssembly",
                        BindingFlags.Static | BindingFlags.Public,
                        null, new[] { typeof(System.Reflection.Assembly) }, null);

                    object info = findMethod?.Invoke(null, new object[] { asm });
                    if (info != null)
                    {
                        string resolved = pkgInfoType.GetProperty("resolvedPath")
                            ?.GetValue(info) as string;
                        if (!string.IsNullOrEmpty(resolved))
                            return Path.Combine(resolved, "Packages~");
                    }
                }
            }
            catch
            {
            }

            // Fallback: cài via .unitypackage → scripts nằm ở Assets/GameUpSDK
            return Path.Combine(Application.dataPath, "GameUpSDK", "Packages~");
        }

        /// <summary>Xóa asset/thư mục sau import .unitypackage (vd bỏ Facebook SDK Examples).</summary>
        private static void ApplyPostImportCleanup(PackageDef pkg)
        {
            if (pkg?.DeleteAssetPathsAfterImport == null || pkg.DeleteAssetPathsAfterImport.Length == 0)
                return;

            foreach (string assetPath in pkg.DeleteAssetPathsAfterImport)
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                    continue;

                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) == null
                    && !AssetDatabase.IsValidFolder(assetPath))
                    continue;

                if (!AssetDatabase.DeleteAsset(assetPath))
                    Debug.LogWarning($"[GameUpSDK] Không xóa được sau import: {assetPath}");
            }
        }

        /// <summary>
        /// Import tất cả file .unitypackage của một package.
        /// interactive=false để không hiện dialog xác nhận cho từng file.
        /// </summary>
        private void ImportUnityPackage(PackageDef pkg, List<string> filePaths)
        {
            pkg.IsInstalling = true;
            pkg.InstallError = null;
            Repaint();

            var errors = new List<string>();
            foreach (string path in filePaths)
            {
                try
                {
                    AssetDatabase.ImportPackage(path, interactive: false);
                    Debug.Log($"[GameUpSDK] Imported: {Path.GetFileName(path)}");
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
                    Debug.LogError($"[GameUpSDK] Import {Path.GetFileName(path)} thất bại: {ex.Message}");
                }
            }

            ApplyPostImportCleanup(pkg);

            // Ép Unity re-scan assets/dll sau khi import để giảm độ trễ load assemblies.
            // (ImportPackage chạy async; refresh thêm nhịp sau giúp state ổn định nhanh hơn.)
            AssetDatabase.Refresh();
            EditorApplication.delayCall += AssetDatabase.Refresh;

            pkg.IsInstalling = false;
            if (errors.Count == 0)
            {
                pkg.IsInstalled = true;
                pkg.InstallError = null;

                // GA .unitypackage không kèm asmdef → pass compile đầu sẽ lỗi thiếu assembly GameAnalyticsSDK.
                // Tạo asmdef ngay sau import (cần đã có GameAnalytics.cs trên disk — không thể tạo trước khi import).
                if (IsGameAnalyticsSdkPackage(pkg))
                {
                    if (GameUpDefineSymbolsAutoSync.TryEnsureGameAnalyticsRuntimeAsmdef(
                            out string asmdefMsg, out bool createdAsmdef))
                    {
                        if (createdAsmdef)
                            Debug.Log("[GameUp] " + asmdefMsg);
                    }
                    else
                    {
                        Debug.LogWarning("[GameUp] " + asmdefMsg);
                        if (!_gameAnalyticsSetupHintAfterBatch)
                            NotifyGameAnalyticsAsmdefHint(fromMediationInstallAllBatch: false);
                    }
                }
            }
            else
            {
                pkg.InstallError = "Một số file import thất bại:\n" + string.Join("\n", errors);
            }

            Repaint();
        }

        // ─── Parallel Download & Import ───────────────────────────────────────────

        /// <summary>Bắt đầu download song song + import một package đơn lẻ.</summary>
        private void StartDownloadAndImport(PackageDef pkg)
        {
            StartParallelDownloadAndImport(new List<PackageDef> { pkg }, onAllDone: null);
        }

        private static bool IsAdMobPackage(PackageDef pkg)
        {
            return pkg != null
                   && string.Equals(pkg.AssemblyName, "GoogleMobileAds", StringComparison.OrdinalIgnoreCase);
        }

        private void StartAdMobLatestUpdate(PackageDef pkg)
        {
            if (pkg == null || IsInstallOrDownloadBusy())
                return;

            _admobLatestReleaseRequest?.Dispose();
            _admobLatestReleaseRequest = null;

            pkg.IsInstalling = true;
            pkg.InstallError = null;
            Repaint();

            var req = UnityWebRequest.Get(AdMobReleaseApiUrl);
            req.SetRequestHeader("Accept", "application/vnd.github+json");
            req.SetRequestHeader("User-Agent", "GameUpSDK-Installer");
            _admobLatestReleaseRequest = req;

            var op = req.SendWebRequest();
            op.completed += _ => ResolveAndInstallLatestAdMob(pkg, req);
        }

        private void ResolveAndInstallLatestAdMob(PackageDef pkg, UnityWebRequest request)
        {
            _admobLatestReleaseRequest = null;

            if (request.result != UnityWebRequest.Result.Success)
            {
                pkg.IsInstalling = false;
                pkg.InstallError = "Không lấy được bản AdMob mới nhất: " + request.error;
                request.Dispose();
                Repaint();
                return;
            }

            string body = request.downloadHandler?.text;
            request.Dispose();

            if (!TryParseLatestAdMobAsset(body, out string fileName, out string downloadUrl, out string releaseTag, out string error))
            {
                pkg.IsInstalling = false;
                pkg.InstallError = error;
                Repaint();
                return;
            }

            _admobUpdateOriginalHostedUrls = pkg.HostedUrls;
            _admobUpdateOriginalBundledFileNames = pkg.BundledFileNames;
            pkg.HostedUrls = new[] { downloadUrl };
            pkg.BundledFileNames = new[] { fileName };

            StartParallelDownloadAndImport(new List<PackageDef> { pkg }, onAllDone: () =>
            {
                pkg.HostedUrls = _admobUpdateOriginalHostedUrls;
                pkg.BundledFileNames = _admobUpdateOriginalBundledFileNames;
                _admobUpdateOriginalHostedUrls = null;
                _admobUpdateOriginalBundledFileNames = null;

                Debug.Log($"[GameUpSDK] AdMob latest update completed from release {releaseTag}: {fileName}");
                RefreshStatus();
            });
        }

        private static bool TryParseLatestAdMobAsset(
            string releaseJson,
            out string fileName,
            out string downloadUrl,
            out string releaseTag,
            out string error)
        {
            fileName = null;
            downloadUrl = null;
            releaseTag = null;
            error = null;

            if (string.IsNullOrWhiteSpace(releaseJson))
            {
                error = "Không nhận được dữ liệu release từ GitHub.";
                return false;
            }

            Dictionary<string, object> root;
            try
            {
                root = SimpleJsonHelper.ParseObject(releaseJson);
            }
            catch (Exception ex)
            {
                error = "Parse release AdMob thất bại: " + ex.Message;
                return false;
            }

            if (root == null)
            {
                error = "Dữ liệu release AdMob không hợp lệ.";
                return false;
            }

            releaseTag = root.TryGetValue("tag_name", out var tagObj) ? tagObj?.ToString() : "unknown";
            if (!root.TryGetValue("assets", out var assetsObj) || !(assetsObj is List<object> assets))
            {
                error = "Release AdMob không có danh sách assets.";
                return false;
            }

            foreach (var assetObj in assets)
            {
                if (!(assetObj is Dictionary<string, object> asset))
                    continue;

                string name = asset.TryGetValue("name", out var nameObj) ? nameObj?.ToString() : null;
                string url = asset.TryGetValue("browser_download_url", out var urlObj) ? urlObj?.ToString() : null;
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
                    continue;
                if (!name.StartsWith(AdMobUnityPackagePrefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!name.EndsWith(AdMobUnityPackageSuffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                fileName = name;
                downloadUrl = url;
                return true;
            }

            error = "Không tìm thấy file GoogleMobileAds-v*.unitypackage trong release mới nhất.";
            return false;
        }

        /// <summary>
        /// Tải tất cả file của tất cả packages cùng lúc (parallel).
        /// Khi toàn bộ download xong → import từng package theo nhóm → gọi onAllDone.
        /// </summary>
        private void StartParallelDownloadAndImport(List<PackageDef> pkgs, Action onAllDone)
        {
            if (_parallelTasks != null)
            {
                // Đang có download chạy, dừng lại
                foreach (var old in _parallelTasks) old.Request?.Dispose();
                EditorApplication.update -= PollParallelDownloads;
            }

            _parallelTasks = new List<DownloadTask>();
            _parallelDoneCallback = onAllDone;

            foreach (var pkg in pkgs)
            {
                if (pkg.HostedUrls == null || pkg.HostedUrls.Length == 0) continue;

                pkg.IsInstalling = true;
                pkg.InstallError = null;

                for (int i = 0; i < pkg.HostedUrls.Length; i++)
                {
                    string url = pkg.HostedUrls[i];
                    string fileName = pkg.BundledFileNames != null && i < pkg.BundledFileNames.Length
                        ? Path.GetFileName(pkg.BundledFileNames[i])
                        : $"{i}.unitypackage";
                    string tempPath = Path.Combine(Application.temporaryCachePath, fileName);

                    var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
                    req.downloadHandler = new DownloadHandlerFile(tempPath) { removeFileOnAbort = true };
                    req.SendWebRequest();

                    _parallelTasks.Add(new DownloadTask
                    {
                        Pkg = pkg,
                        FileName = fileName,
                        TempPath = tempPath,
                        Request = req,
                    });
                }
            }

            if (_parallelTasks.Count == 0)
            {
                _parallelTasks = null;
                onAllDone?.Invoke();
                return;
            }

            EditorApplication.update += PollParallelDownloads;
            Repaint();
        }

        private void PollParallelDownloads()
        {
            if (_parallelTasks == null) return;

            bool anyRunning = false;
            foreach (var task in _parallelTasks)
            {
                if (task.IsDone) continue;
                if (!task.Request.isDone)
                {
                    anyRunning = true;
                    continue;
                }

                // Request hoàn thành
                task.IsDone = true;
                if (task.Request.result != UnityWebRequest.Result.Success)
                {
                    task.HasError = true;
                    task.ErrorMessage = task.Request.error;
                }

                task.Request.Dispose();
                task.Request = null;
            }

            // Cập nhật overall progress
            float totalProgress = _parallelTasks.Sum(t =>
                t.IsDone ? 1f : t.Request?.downloadProgress ?? 0f);
            _downloadProgress = totalProgress / _parallelTasks.Count;
            int doneCount = _parallelTasks.Count(t => t.IsDone);
            _downloadStatus = $"Đang tải: {doneCount}/{_parallelTasks.Count} files";
            Repaint();

            if (anyRunning) return;

            // ─── Tất cả done → import theo nhóm package ───────────────────────
            EditorApplication.update -= PollParallelDownloads;

            // Group tasks by package
            var byPkg = _parallelTasks
                .GroupBy(t => t.Pkg)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (PackageDef pkg in OrderedInstallSequence(byPkg.Keys))
            {
                List<DownloadTask> tasks = byPkg[pkg];
                var downloadedPaths = tasks.Where(t => !t.HasError).Select(t => t.TempPath).ToList();
                var errorMsgs = tasks.Where(t => t.HasError)
                    .Select(t => $"{t.FileName}: {t.ErrorMessage}").ToList();
                var successPaths = ResolveImportableUnityPackages(downloadedPaths, out var unzipErrors);
                errorMsgs.AddRange(unzipErrors);

                pkg.IsInstalling = false;
                if (errorMsgs.Count > 0)
                    pkg.InstallError = "Xử lý/tải file thất bại:\n" + string.Join("\n", errorMsgs);

                if (successPaths.Count > 0)
                    ImportUnityPackage(pkg, successPaths);
            }

            _parallelTasks = null;
            _downloadProgress = 0;
            _downloadStatus = null;

            var cb = _parallelDoneCallback;
            _parallelDoneCallback = null;
            cb?.Invoke();
        }

        /// <summary>
        /// Chuyển danh sách file đã tải thành danh sách .unitypackage có thể import.
        /// Nếu gặp .zip sẽ tự giải nén và lấy toàn bộ file .unitypackage bên trong.
        /// </summary>
        private static List<string> ResolveImportableUnityPackages(IEnumerable<string> downloadedPaths, out List<string> errors)
        {
            var importable = new List<string>();
            errors = new List<string>();

            if (downloadedPaths == null)
                return importable;

            foreach (string path in downloadedPaths)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    errors.Add("File tải về không tồn tại: " + path);
                    continue;
                }

                try
                {
                    if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        string extractRoot = Path.Combine(
                            Application.temporaryCachePath,
                            "GameUpAdMobAdapterZip",
                            Path.GetFileNameWithoutExtension(path) + "_" + Guid.NewGuid().ToString("N"));

                        Directory.CreateDirectory(extractRoot);
                        ZipFile.ExtractToDirectory(path, extractRoot);

                        var unityPackages = Directory
                            .GetFiles(extractRoot, "*.unitypackage", SearchOption.AllDirectories)
                            .ToList();

                        if (unityPackages.Count == 0)
                        {
                            errors.Add($"{Path.GetFileName(path)}: không tìm thấy file .unitypackage trong .zip.");
                            continue;
                        }

                        importable.AddRange(unityPackages);
                        continue;
                    }

                    importable.Add(path);
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(path)}: lỗi xử lý file tải về ({ex.Message}).");
                }
            }

            return importable;
        }

        private void AddScopedRegistryAndPackage(PackageDef pkg)
        {
            // Đọc manifest.json, thêm scoped registry + dependency, ghi lại
            string manifestPath = System.IO.Path.Combine(
                Application.dataPath, "..", "Packages", "manifest.json");

            try
            {
                string json = System.IO.File.ReadAllText(manifestPath);
                var manifest = SimpleJsonHelper.ParseObject(json);

                // Thêm scoped registry nếu chưa có
                if (!string.IsNullOrEmpty(pkg.RegistryUrl))
                {
                    if (!manifest.ContainsKey("scopedRegistries"))
                        manifest["scopedRegistries"] = new List<object>();

                    var registries = (List<object>)manifest["scopedRegistries"];
                    bool found = registries.OfType<Dictionary<string, object>>()
                        .Any(r => r.TryGetValue("url", out var u) && u?.ToString() == pkg.RegistryUrl);

                    if (!found)
                    {
                        registries.Add(new Dictionary<string, object>
                        {
                            ["name"] = pkg.RegistryName,
                            ["url"] = pkg.RegistryUrl,
                            ["scopes"] = pkg.RegistryScopes?.ToList<object>() ?? new List<object>(),
                        });
                    }
                }

                // Thêm dependency
                if (!manifest.ContainsKey("dependencies"))
                    manifest["dependencies"] = new Dictionary<string, object>();

                var deps = (Dictionary<string, object>)manifest["dependencies"];
                if (!deps.ContainsKey(pkg.PackageId))
                    deps[pkg.PackageId] = "latest";

                System.IO.File.WriteAllText(manifestPath, SimpleJsonHelper.Serialize(manifest));
                AssetDatabase.Refresh();

                pkg.IsInstalling = false;
                pkg.IsInstalled = true;
            }
            catch (Exception ex)
            {
                pkg.IsInstalling = false;
                pkg.InstallError = "Lỗi khi sửa manifest.json: " + ex.Message;
            }

            Repaint();
        }

        // ─── Scripting Define Symbol management ──────────────────────────────────

        private static readonly BuildTargetGroup[] s_buildTargetGroups =
        {
            BuildTargetGroup.Android,
            BuildTargetGroup.iOS,
            BuildTargetGroup.Standalone,
        };

        /// <summary>
        /// Thêm hoặc xóa define GAMEUP_SDK_DEPS_READY khỏi Player Settings.
        /// Khi define này tồn tại, GameUp.SDK.Runtime và GameUp.SDK.Editor sẽ được compile.
        /// </summary>
        internal static void SetDepsReadyDefine(bool enabled)
        {
            foreach (var group in s_buildTargetGroups)
            {
                try
                {
                    string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
                    var list = new List<string>(
                        current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

                    bool changed = false;
                    if (enabled && !list.Contains(GUDefinetion.DepsReadyDefine))
                    {
                        list.Add(GUDefinetion.DepsReadyDefine);
                        changed = true;
                    }
                    else if (!enabled && list.Remove(GUDefinetion.DepsReadyDefine))
                    {
                        changed = true;
                    }

                    if (changed)
                        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", list));
                }
                catch
                {
                    /* group không tồn tại trong project này, bỏ qua */
                }
            }
        }

        internal static bool IsDepsReadyDefined()
        {
            string current = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            return current.Contains(GUDefinetion.DepsReadyDefine);
        }

        // ─── Status refresh ───────────────────────────────────────────────────────

        private void RefreshStatus(bool syncDefines = true)
        {
            if (syncDefines)
                EnsurePrimaryMediationDefines();

            foreach (var pkg in s_packages)
            {
                pkg.IsInstalled = IsPackageInstalled(pkg);
                pkg.IsInstalling = false;
                pkg.InstallError = null;
            }

            if (!syncDefines)
            {
                Repaint();
                return;
            }

            // Auto set/clear Facebook define (Editor assembly = SDK đã import)
            bool facebookInstalled = IsAssemblyLoaded("Facebook.Unity.Editor");
            if (facebookInstalled && !HasDefine(FacebookDepsDefine))
                SetDefine(FacebookDepsDefine, true);
            else if (!facebookInstalled && HasDefine(FacebookDepsDefine))
                SetDefine(FacebookDepsDefine, false);

            // Auto set/clear LevelPlay define theo trạng thái package
            bool levelPlayInstalled = IsAssemblyLoaded("Unity.LevelPlay");
            if (levelPlayInstalled && !HasDefine(LevelPlayDepsDefine))
                SetDefine(LevelPlayDepsDefine, true);
            else if (!levelPlayInstalled && HasDefine(LevelPlayDepsDefine))
                SetDefine(LevelPlayDepsDefine, false);

            // Auto set/clear LevelPlay define theo trạng thái package
            bool maxInstalled = IsAssemblyLoaded("MaxSdk.Scripts");
            if (maxInstalled && !HasDefine(MaxSdkDepsDefine))
                SetDefine(MaxSdkDepsDefine, true);
            else if (!maxInstalled && HasDefine(MaxSdkDepsDefine))
                SetDefine(MaxSdkDepsDefine, false);

            // Auto set/clear AdMob define theo AdMob core package.
            // Adapter mediation không ảnh hưởng define này.
            bool admobInstalled = IsAdMobCoreInstalled();
            if (admobInstalled && !HasDefine(AdMobDepsDefine))
                SetDefine(AdMobDepsDefine, true);
            else if (!admobInstalled && HasDefine(AdMobDepsDefine))
                SetDefine(AdMobDepsDefine, false);

            // Auto set/clear Firebase define theo trạng thái package
            bool firebaseInstalled = IsAssemblyLoaded("Firebase.App");
            if (firebaseInstalled && !HasDefine(FirebaseDepsDefine))
                SetDefine(FirebaseDepsDefine, true);
            else if (!firebaseInstalled && HasDefine(FirebaseDepsDefine))
                SetDefine(FirebaseDepsDefine, false);

            bool appMetricaInstalled = IsAssemblyLoaded("AppMetrica");
            if (appMetricaInstalled && !HasDefine(AppmetricaDepsDefine))
                SetDefine(AppmetricaDepsDefine, true);
            else if (!appMetricaInstalled && HasDefine(AppmetricaDepsDefine))
                SetDefine(AppmetricaDepsDefine, false);

            // Auto set/clear AppsFlyer define theo trạng thái package
            bool appsFlyerInstalled = IsAssemblyLoaded("AppsFlyer");
            if (appsFlyerInstalled && !HasDefine(AppsFlyerDepsDefine))
                SetDefine(AppsFlyerDepsDefine, true);
            else if (!appsFlyerInstalled && HasDefine(AppsFlyerDepsDefine))
                SetDefine(AppsFlyerDepsDefine, false);

            // GameAnalytics: UPM (assembly GameAnalyticsSDK) hoặc .unitypackage cổ điển (type trong Assembly-CSharp)
            bool gameAnalyticsInstalled = IsGameAnalyticsSdkPresent();
            if (gameAnalyticsInstalled && !HasDefine(GameAnalyticsDepsDefine))
                SetDefine(GameAnalyticsDepsDefine, true);
            else if (!gameAnalyticsInstalled && HasDefine(GameAnalyticsDepsDefine))
                SetDefine(GameAnalyticsDepsDefine, false);

            // Tự động set/clear define khi trạng thái thay đổi
            // GAMEUP_SDK_DEPS_READY chỉ còn ý nghĩa "SDK enabled" (backward compat).
            // Bật khi có (Firebase hoặc AppsFlyer hoặc GameAnalytics) AND (AdMob hoặc LevelPlay).
            // Không dùng define này để include SDK bên thứ 3 nữa.
            bool hasAnalytics = firebaseInstalled || appsFlyerInstalled || gameAnalyticsInstalled || appMetricaInstalled;
            bool hasMediation = admobInstalled || levelPlayInstalled || maxInstalled;
            bool sdkEnabled = hasAnalytics && hasMediation;
            if (sdkEnabled && !IsDepsReadyDefined())
                SetDepsReadyDefine(true);
            else if (!sdkEnabled && IsDepsReadyDefined())
                SetDepsReadyDefine(false);

            Repaint();
        }

        private static bool CanAutoInstall(PackageDef p)
        {
            if (p.Method == InstallMethod.GitUrl || p.Method == InstallMethod.ScopedRegistry)
                return true;
            if (p.Method == InstallMethod.UnityPackage)
                return GetBundledPackagePaths(p.BundledFileNames) != null
                       || (p.HostedUrls?.Length > 0);
            return false;
        }

        private static bool IsAssemblyLoaded(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName)) return false;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(asm.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsPackageInstalled(PackageDef pkg)
        {
            if (pkg == null)
                return false;

            bool byAssembly = !string.IsNullOrEmpty(pkg.AssemblyName) && IsAssemblyLoaded(pkg.AssemblyName);
            bool byAssetPath = HasInstalledAssetPath(pkg);
            bool byType = !string.IsNullOrEmpty(pkg.InstalledTypeFullName) &&
                          IsTypeInAnyLoadedAssembly(pkg.InstalledTypeFullName);

            // AdMob mediation adapters cần phản ánh trạng thái thư mục asset thực tế.
            // Assembly có thể còn loaded trong AppDomain một lúc sau khi xóa asset.
            if (pkg.IsAdMobMediationAdapter)
                return byAssetPath;

            if (!string.IsNullOrEmpty(pkg.InstalledTypeFullName))
                return byAssembly || byType || byAssetPath;
            if (!string.IsNullOrEmpty(pkg.InstalledAssetPath))
                return byAssembly || byAssetPath;
            return byAssembly;
        }

        /// <summary>
        /// AdMob define chỉ phản ánh package AdMob core, không phụ thuộc các mediation adapter.
        /// </summary>
        private static bool IsAdMobCoreInstalled()
        {
            var core = s_packages.FirstOrDefault(p =>
                !p.IsAdMobMediationAdapter &&
                string.Equals(p.AssemblyName, "GoogleMobileAds", StringComparison.OrdinalIgnoreCase));

            if (core != null)
                return IsPackageInstalled(core);

            // Fallback an toàn nếu package definition bị thay đổi.
            return IsAssemblyLoaded("GoogleMobileAds");
        }

        private static bool HasInstalledAssetPath(PackageDef pkg)
        {
            if (pkg == null || string.IsNullOrEmpty(pkg.InstalledAssetPath))
                return false;

            string path = pkg.InstalledAssetPath.Replace('\\', '/');

            // Với path thư mục (đa số adapter), chỉ coi là "đã cài" khi folder thực sự tồn tại.
            if (!Path.HasExtension(path))
                return AssetDatabase.IsValidFolder(path);

            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                return false;

            return AssetDatabase.LoadMainAssetAtPath(path) != null;
        }

        private static List<string> GetRemovableAssetPaths(PackageDef pkg)
        {
            var paths = new List<string>();
            if (pkg == null)
                return paths;

            if (pkg.RemoveAssetPaths != null)
            {
                foreach (string p in pkg.RemoveAssetPaths)
                {
                    if (!string.IsNullOrEmpty(p) && !paths.Contains(p))
                        paths.Add(p);
                }
            }

            if (!string.IsNullOrEmpty(pkg.InstalledAssetPath) && !paths.Contains(pkg.InstalledAssetPath))
                paths.Add(pkg.InstalledAssetPath);

            return paths;
        }

        private static bool CanRemovePackage(PackageDef pkg)
        {
            return GetRemovableAssetPaths(pkg)
                .Any(p => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(p)));
        }

        private void ConfirmAndRemovePackage(PackageDef pkg)
        {
            if (pkg == null)
                return;

            var existingPaths = FilterSdkRemovablePaths(
                    GetRemovableAssetPaths(pkg)
                        .Where(p => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(p))))
                .ToList();

            if (existingPaths.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "GameUp SDK — Gỡ package",
                    "Không tìm thấy asset path để xóa cho package này.",
                    "OK");
                return;
            }

            string preview = string.Join("\n• ", existingPaths);
            if (!EditorUtility.DisplayDialog(
                    "GameUp SDK — Gỡ package",
                    $"Bạn có chắc muốn gỡ \"{pkg.DisplayName}\"?\n\n" +
                    $"Sẽ xóa các path:\n• {preview}\n\n" +
                    "Lưu ý: thao tác này không tự thêm lại package vào project.",
                    "Gỡ package",
                    "Hủy"))
                return;

            if (!TryDeleteAssets(existingPaths, out string error))
            {
                EditorUtility.DisplayDialog(
                    "GameUp SDK — Gỡ package thất bại",
                    error,
                    "OK");
                return;
            }

            AssetDatabase.Refresh();
            RefreshStatus();
            Debug.Log("[GameUpSDK] Đã gỡ package: " + pkg.DisplayName);
        }

        private void ConfirmAndRemoveAdMobAdapter(PackageDef pkg)
        {
            if (pkg == null || !pkg.IsAdMobMediationAdapter)
                return;

            if (!HasInstalledAssetPath(pkg))
            {
                EditorUtility.DisplayDialog(
                    "GameUp SDK — Gỡ adapter",
                    "Không tìm thấy thư mục adapter để xóa: " + pkg.InstalledAssetPath,
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "GameUp SDK — Gỡ AdMob adapter",
                    $"Bạn có chắc muốn gỡ \"{pkg.DisplayName}\"?\n\n" +
                    $"Sẽ xóa: {pkg.InstalledAssetPath}\n\n" +
                    "Lưu ý: có thể cần chạy resolver/compile lại sau khi gỡ.",
                    "Gỡ adapter",
                    "Hủy"))
                return;

            if (!TryDeleteInstalledAssetPath(pkg, out string error))
            {
                EditorUtility.DisplayDialog(
                    "GameUp SDK — Gỡ adapter thất bại",
                    error,
                    "OK");
                return;
            }

            AssetDatabase.Refresh();
            RefreshStatus();
            Debug.Log("[GameUpSDK] Đã gỡ adapter: " + pkg.DisplayName);
        }

        private void ConfirmAndRemoveSetupDependenciesBulk(List<PackageDef> packages)
        {
            if (packages == null || packages.Count == 0)
                return;

            var existingPaths = FilterSdkRemovablePaths(
                    packages
                        .SelectMany(GetRemovableAssetPaths)
                        .Where(p => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(p)))
                        .Concat(GetSetupDependenciesResidualPaths()
                            .Where(p => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(p))))
                )
                .ToList();

            if (existingPaths.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "GameUp SDK — Gỡ SDK dependencies",
                    "Không tìm thấy SDK dependency path để gỡ.",
                    "OK");
                return;
            }

            string preview = string.Join("\n• ", existingPaths.Take(12));
            if (existingPaths.Count > 12)
                preview += $"\n• ... và {existingPaths.Count - 12} path khác";

            if (!EditorUtility.DisplayDialog(
                    "GameUp SDK — Gỡ SDK dependencies",
                    "Bạn có chắc muốn gỡ toàn bộ SDK dependencies trong tab SetupDependencies?\n\n" +
                    $"Sẽ xóa {existingPaths.Count} path:\n• {preview}\n\n" +
                    "GameUp Core và DOTween không bị gỡ.\n" +
                    "Sẽ clear define symbols SDK trước khi gỡ pack.",
                    "Gỡ SDK dependencies",
                    "Hủy"))
                return;

            // Clear define trước để tránh conditionals/compile lệch trạng thái trong lúc gỡ.
            ClearDependencyDefinesAfterBulkRemove();

            if (!TryDeleteAssets(existingPaths, out string error))
            {
                EditorUtility.DisplayDialog(
                    "GameUp SDK — Gỡ dependencies thất bại",
                    error,
                    "OK");
                return;
            }

            AssetDatabase.Refresh();
            // Tránh auto-sync define ngay vì assemblies có thể còn loaded trong AppDomain tạm thời.
            RefreshStatus(syncDefines: false);
            Debug.Log("[GameUpSDK] Đã gỡ toàn bộ SDK dependencies trong tab SetupDependencies (Core/DOTween giữ nguyên).");
        }

        private static void ClearDependencyDefinesAfterBulkRemove()
        {
            SetDefine(LevelPlayDepsDefine, false);
            SetDefine(AdMobDepsDefine, false);
            SetDefine(MaxSdkDepsDefine, false);
            SetDefine(FirebaseDepsDefine, false);
            SetDefine(AppmetricaDepsDefine, false);
            SetDefine(AppsFlyerDepsDefine, false);
            SetDefine(GameAnalyticsDepsDefine, false);
            SetDefine(FacebookDepsDefine, false);
            SetDepsReadyDefine(false);

            // Reset mediation về mặc định an toàn sau khi gỡ toàn bộ dependencies.
            SetDefine(GUDefinetion.PrimaryMediationAdMob, false);
            SetDefine(GUDefinetion.PrimaryMediationMax, false);
            SetDefine(GUDefinetion.PrimaryMediationLevelPlay, true);
        }

        /// <summary>
        /// Residual do SDK third-party để lại (Firebase EDM, native plugins…).
        /// Không liệt kê Assets/Plugins, Assets/Editor, Assets/StreamingAssets — có thể chứa Core (DOTween) hoặc nội dung game.
        /// </summary>
        private static IEnumerable<string> GetSetupDependenciesResidualPaths()
        {
            return new[]
            {
                "Assets/Editor Default Resources/Firebase",
                "Assets/ExternalDependencyManager",
                "Assets/GeneratedLocalRepo/Firebase",
                "Assets/Plugins/Android",
                "Assets/Plugins/iOS/Firebase",
                "Assets/Plugins/tvOS/Firebase",
                "Assets/Plugins/iOS/GADUAdNetworkExtras.h",
                "Assets/Resources/GameAnalytics",
                "Assets/SDK",
            };
        }

        private static bool IsCoreProtectedAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            foreach (string prefix in s_coreProtectedAssetPathPrefixes)
            {
                if (path.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> FilterSdkRemovablePaths(IEnumerable<string> paths)
        {
            if (paths == null)
                yield break;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path) || IsCoreProtectedAssetPath(path) || !seen.Add(path))
                    continue;

                yield return path;
            }
        }

        private static bool TryDeleteInstalledAssetPath(PackageDef pkg, out string error)
        {
            error = null;
            try
            {
                if (pkg == null || string.IsNullOrEmpty(pkg.InstalledAssetPath))
                {
                    error = "Adapter không có InstalledAssetPath để xóa.";
                    return false;
                }

                if (!HasInstalledAssetPath(pkg))
                {
                    error = "Không tìm thấy asset path: " + pkg.InstalledAssetPath;
                    return false;
                }

                return TryDeleteAssets(new List<string> { pkg.InstalledAssetPath }, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryDeleteAssets(List<string> assetPaths, out string error)
        {
            error = null;
            if (assetPaths == null || assetPaths.Count == 0)
            {
                error = "Không có asset path để xóa.";
                return false;
            }

            foreach (string path in FilterSdkRemovablePaths(assetPaths))
            {
                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                    continue;

                if (!AssetDatabase.DeleteAsset(path))
                {
                    error = "AssetDatabase.DeleteAsset trả về false: " + path;
                    return false;
                }
            }

            return true;
        }

        /// <summary>UPM có .asmdef GameAnalyticsSDK; .unitypackage chuẩn GA nằm trong Assembly-CSharp.</summary>
        internal static bool IsGameAnalyticsSdkPresent()
        {
            return IsAssemblyLoaded("GameAnalyticsSDK") ||
                   IsTypeInAnyLoadedAssembly("GameAnalyticsSDK.GameAnalytics");
        }

        private static bool IsTypeInAnyLoadedAssembly(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return false;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetType(fullTypeName, throwOnError: false, ignoreCase: false) != null)
                        return true;
                }
                catch
                {
                    /* một số dynamic assembly */
                }
            }

            return false;
        }
    }

    // ─── Minimal JSON helper (không dùng Newtonsoft/JsonUtility để giữ assembly sạch) ───

    internal static class SimpleJsonHelper
    {
        public static Dictionary<string, object> ParseObject(string json)
        {
            // Dùng Unity built-in JsonUtility không hỗ trợ Dictionary,
            // nên parse thủ công phần dependencies/scopedRegistries cần thiết.
            // Thực tế: dùng regex-free approach với index tracking.
            json = json.Trim();
            int idx = 0;
            return (Dictionary<string, object>)ParseValue(json, ref idx);
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) return null;

            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't')
            {
                i += 4;
                return true;
            }

            if (c == 'f')
            {
                i += 5;
                return false;
            }

            if (c == 'n')
            {
                i += 4;
                return null;
            }

            return ParseNumber(s, ref i);
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var dict = new Dictionary<string, object>();
            i++; // skip '{'
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}')
            {
                i++;
                return dict;
            }

            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                i++; // skip ':'
                object val = ParseValue(s, ref i);
                dict[key] = val;
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }

                if (i < s.Length && s[i] == '}')
                {
                    i++;
                    break;
                }
            }

            return dict;
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // skip '['
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']')
            {
                i++;
                return list;
            }

            while (i < s.Length)
            {
                list.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                    continue;
                }

                if (i < s.Length && s[i] == ']')
                {
                    i++;
                    break;
                }
            }

            return list;
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // skip opening '"'
            var sb = new System.Text.StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c == '\\' && i < s.Length)
                {
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(esc); break;
                    }
                }
                else sb.Append(c);
            }

            return sb.ToString();
        }

        private static object ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '.' || s[i] == 'e' || s[i] == 'E' ||
                                    s[i] == '+'))
                i++;
            string num = s.Substring(start, i - start);
            if (int.TryParse(num, out int iv)) return iv;
            if (double.TryParse(num, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double dv)) return dv;
            return num;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        public static string Serialize(object obj, int indent = 0)
        {
            string pad = new string(' ', indent * 2);
            string pad1 = new string(' ', (indent + 1) * 2);

            switch (obj)
            {
                case null: return "null";
                case bool b: return b ? "true" : "false";
                case int iv: return iv.ToString();
                case long lv: return lv.ToString();
                case double dv:
                    return dv.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case string sv:
                    return "\"" + sv.Replace("\\", "\\\\").Replace("\"", "\\\"")
                        .Replace("\n", "\\n").Replace("\r", "\\r")
                        .Replace("\t", "\\t") + "\"";

                case Dictionary<string, object> dict:
                    {
                        if (dict.Count == 0) return "{}";
                        var lines = dict.Select(
                            kv => pad1 + "\"" + kv.Key + "\": " + Serialize(kv.Value, indent + 1));
                        return "{\n" + string.Join(",\n", lines) + "\n" + pad + "}";
                    }

                case List<object> list:
                    {
                        if (list.Count == 0) return "[]";
                        var lines = list.Select(item => pad1 + Serialize(item, indent + 1));
                        return "[\n" + string.Join(",\n", lines) + "\n" + pad + "]";
                    }

                default:
                    return "\"" + obj.ToString() + "\"";
            }
        }
    }
}