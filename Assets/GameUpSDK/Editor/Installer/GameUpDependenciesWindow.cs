using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using GameUp.SDK;
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

            /// <summary>
            /// Bắt buộc khi Primary Mediation = AdMob (mediation stack GameUp SDK + forward GDPR consent).
            /// </summary>
            public bool RequiredForAdMobRuntime;

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
            /// Thứ tự cài khuyến nghị (số nhỏ trước): Facebook → Firebase (EDM) → AdMob/LevelPlay → AppsFlyer → GameAnalytics.
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
                    "Bắt buộc. Facebook SDK cho Unity (login, sharing, v.v.). Cài qua installer sẽ tự xóa thư mục Examples sau khi import xong.",
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
                Description = "Khuyến nghị mạnh. Analytics, crash reporting, remote config. Kèm EDM4U (dùng chung cho AdMob/AppsFlyer/GA).",
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
                // Không liệt kê Assets/ExternalDependencyManager: EDM4U dùng chung với AdMob/AppsFlyer/GA,
                // chỉ dọn khi gỡ toàn bộ dependencies.
                RemoveAssetPaths = new[]
                {
                    "Assets/Firebase",
                    "Assets/Editor Default Resources/Firebase",
                    "Assets/GeneratedLocalRepo/Firebase",
                    "Assets/Plugins/iOS/Firebase",
                    "Assets/Plugins/tvOS/Firebase",
                },
                InstallPriority = 20,
            },
            new PackageDef
            {
                DisplayName = "Google Mobile Ads — AdMob",
                Description = "Cần khi Primary Mediation = AdMob, khi dùng AdMob standalone (Interstitial/Rewarded/AppOpen), hoặc khi muốn bắt paid event để log ad_impression.",
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
                RemoveAssetPaths = new[]
                {
                    "Assets/GoogleMobileAds",
                    "Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib",
                    "Assets/Plugins/Android/googlemobileads-unity.aar",
                    "Assets/Plugins/iOS/GADUAdNetworkExtras.h",
                },
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
                RemoveAssetPaths = new[] { "Assets/GameAnalytics", "Assets/Resources/GameAnalytics" },
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
                Description =
                    "Bắt buộc với Primary Mediation = AdMob (GameUp SDK). Adapter mediation IronSource / LevelPlay trong waterfall AdMob.",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.IronSource.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/IronSource",
                IsAdMobMediationAdapter = true,
                RequiredForAdMobRuntime = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "IronSourceUnityAdapter-4.5.1.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/ironsource/IronSourceUnityAdapter-4.5.1.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/ironsource",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 31,
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
                Description =
                    "Bắt buộc với Primary Mediation = AdMob (GameUp SDK). Adapter mediation Unity Ads trong waterfall AdMob.",
                Required = false,
                AssemblyName = "GoogleMobileAds.Mediation.UnityAds.Api",
                InstalledAssetPath = "Assets/GoogleMobileAds/Mediation/UnityAds",
                IsAdMobMediationAdapter = true,
                RequiredForAdMobRuntime = true,
                Method = InstallMethod.UnityPackage,
                BundledFileNames = new[] { "UnityAdsUnityAdapter-3.18.0.zip" },
                HostedUrls = new[]
                {
                    "https://dl.google.com/googleadmobadssdk/mediation/unity/unity/UnityAdsUnityAdapter-3.18.0.zip",
                },
                DownloadUrl = "https://developers.google.com/admob/unity/mediation/unity",
                DownloadLabel = "Chi tiết adapter →",
                InstallPriority = 31,
            },
        };


        // ─── State ────────────────────────────────────────────────────────────────

        private Vector2 _scroll;
        private bool _isBatchInstalling;

        /// <summary>Package sẽ cài trong lần batch hiện tại (null = toàn bộ s_packages — chỉ dùng nội bộ).</summary>
        private List<PackageDef> _batchScope;

        /// <summary>Bật khi batch bắt đầu từ &quot;Cài tất cả&quot; — gợi ý menu Ensure GameAnalytics asmdef khi xong.</summary>
        private bool _gameAnalyticsSetupHintAfterBatch;
        private bool _wasCompiling;
        private bool _wasBusy;

        /// <summary>
        /// File .unitypackage đang chờ Unity import xong (key = tên file không đuôi, đúng giá trị Unity
        /// trả về trong importPackageCompleted/Failed/Cancelled) → package sở hữu file đó.
        /// </summary>
        private readonly Dictionary<string, PackageDef> _pendingImports =
            new Dictionary<string, PackageDef>(StringComparer.OrdinalIgnoreCase);

        private double _pendingImportsStartedAt;
        private const double PendingImportTimeoutSeconds = 300;

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
        private WindowTab _activeTab = WindowTab.SetupDependencies;

        // ── UI state (layout 2 cột + các mục gấp/mở) ──
        private Vector2 _leftScroll;
        private bool _foldoutInstallOrder;
        private bool _foldoutTools;
        private bool _showOnlyMissing;

        private GUIStyle _cardStyle;
        private GUIStyle _cardTitleStyle;
        private GUIStyle _stepTitleStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _descStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _rowStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _foldoutTitleStyle;
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

        private static IEnumerable<PackageDef> GetRequiredAdMobRuntimeAdapters()
        {
            return GetAdMobMediationAdapters().Where(p => p.RequiredForAdMobRuntime);
        }

        [MenuItem("GameUp/SDK/Setup Dependencies")]
        public static void ShowWindow()
        {
            var win = GetWindow<GameUpDependenciesWindow>(true, "GameUp SDK — Setup Dependencies");
            win.minSize = new Vector2(620, 540);
            // Mở mặc định đủ rộng để dùng layout 2 cột (hướng dẫn | danh sách package).
            if (win.position.width < 900)
                win.position = new Rect(win.position.x, win.position.y, 1000, 660);
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
            AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
            AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
            AssetDatabase.importPackageFailed -= OnImportPackageFailed;
            AssetDatabase.importPackageFailed += OnImportPackageFailed;
            AssetDatabase.importPackageCancelled -= OnImportPackageCancelled;
            AssetDatabase.importPackageCancelled += OnImportPackageCancelled;

            // Sau domain reload/restore window, timing load assemblies có thể trễ hơn compilationFinished.
            // DelayCall 1 nhịp là đủ để tránh scan quá sớm.
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                // Cleanup sau import bị domain reload cắt ngang (vd Facebook Examples) — chạy bù ở đây.
                RunPendingPostImportCleanups();
                RefreshStatus();
                // Batch install bị domain reload cắt ngang giữa chừng thì chạy tiếp tại đây.
                TryResumePendingBatch();
            };
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdateRepaintWhenBusy;
            EditorApplication.update -= PollInstallQueue;
            EditorApplication.update -= PollParallelDownloads;
            AssemblyReloadEvents.afterAssemblyReload -= AfterAssemblyReloadRefresh;
            CompilationPipeline.compilationFinished -= OnCompilationFinishedRefresh;
            AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
            AssetDatabase.importPackageFailed -= OnImportPackageFailed;
            AssetDatabase.importPackageCancelled -= OnImportPackageCancelled;
            if (_parallelTasks != null)
            {
                foreach (var t in _parallelTasks)
                    t.Request?.Dispose();
                _parallelTasks = null;
            }

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

        private void OnImportPackageCompleted(string packageName)
        {
            FinishPendingImport(packageName, success: true, error: null);
            ScheduleRefreshAfterImport();
        }

        private void OnImportPackageFailed(string packageName, string error)
        {
            FinishPendingImport(packageName, success: false, error: error);
            ScheduleRefreshAfterImport();
        }

        private void OnImportPackageCancelled(string packageName)
        {
            FinishPendingImport(packageName, success: false, error: "Import bị hủy.");
            ScheduleRefreshAfterImport();
        }

        private void ScheduleRefreshAfterImport()
        {
            // ImportPackage hoàn thành có thể trigger refresh/compile; delayCall để tránh scan quá sớm.
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                RefreshStatus();
                TryResumePendingBatch();
            };
        }

        /// <summary>Làm mới UI khi đang compile hoặc đang cài để nút bật/tắt đúng lúc compile xong.</summary>
        private void EditorUpdateRepaintWhenBusy()
        {
            DropStalePendingImports();

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
            EnsureStyles();

            DrawToolbar();
            DrawBusyBar();

            if (_activeTab == WindowTab.SetupDependencies)
                DrawSetupDependenciesTab();
            else
                DrawAdMobMediationTab();

            DrawFooter();
        }

        /// <summary>Thanh trên cùng: chuyển tab + tiến độ tổng + làm mới.</summary>
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Toggle(_activeTab == WindowTab.SetupDependencies, "Dependencies", EditorStyles.toolbarButton, GUILayout.Width(120)))
                _activeTab = WindowTab.SetupDependencies;
            if (GUILayout.Toggle(_activeTab == WindowTab.AdMobMediation, "AdMob Mediation", EditorStyles.toolbarButton, GUILayout.Width(130)))
                _activeTab = WindowTab.AdMobMediation;

            GUILayout.FlexibleSpace();

            var planned = GetPackagesForSdkSetup(GetPrimaryMediationFromDefines());
            GUILayout.Label($"Bộ hiện tại: {planned.Count(p => p.IsInstalled)}/{planned.Count} đã cài", EditorStyles.miniLabel);
            GUILayout.Space(6);
            if (GUILayout.Button("↻ Làm mới", EditorStyles.toolbarButton, GUILayout.Width(80)))
                RequestManualRefresh();

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Dải trạng thái khi đang tải/cài — thay cho HelpBox rời rạc trước đây.</summary>
        private void DrawBusyBar()
        {
            if (!IsInstallOrDownloadBusy()) return;

            var rect = EditorGUILayout.BeginVertical(_rowStyle);
            EditorGUI.DrawRect(rect, Tint(BusyColor, 0.14f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), BusyColor);
            GUILayout.Label("⟳  Đang tải / cài dependency… Vui lòng chờ Unity import và compile xong.", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawSetupDependenciesTab()
        {
            // Đủ rộng thì chia 2 cột (hướng dẫn trái | danh sách package phải), hẹp thì xếp dọc.
            bool twoColumns = position.width >= 900f;

            if (!twoColumns)
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                DrawGuideColumn();
                EditorGUILayout.Space(8);
                DrawPackagePanelContent();
                EditorGUILayout.Space(8);
                DrawSetupDependenciesBulkRemoveSection();
                EditorGUILayout.EndScrollView();
                return;
            }

            float leftWidth = Mathf.Clamp(position.width * 0.42f, 340f, 480f);

            EditorGUILayout.BeginHorizontal();

            // ── Cột trái: các bước cần làm ──
            EditorGUILayout.BeginVertical(GUILayout.Width(leftWidth), GUILayout.ExpandHeight(true));
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUILayout.Width(leftWidth));
            DrawGuideColumn();
            EditorGUILayout.Space(6);
            DrawSetupDependenciesBulkRemoveSection();
            EditorGUILayout.Space(8);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            DrawVerticalSeparator();

            // ── Cột phải: danh sách package ──
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawPackagePanelHeader();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawPackageList(includeAdMobAdapters: false, allowPerPackageRemove: false);
            EditorGUILayout.Space(8);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Cột trái ở chế độ 1 cột: header + list gộp chung trong scroll ngoài.</summary>
        private void DrawPackagePanelContent()
        {
            DrawPackagePanelHeader();
            DrawPackageList(includeAdMobAdapters: false, allowPerPackageRemove: false);
        }

        private void DrawPackagePanelHeader()
        {
            EditorGUILayout.BeginHorizontal(_rowStyle);
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Danh sách package", _cardTitleStyle);
            GUILayout.Label("Cài/gỡ từng package. Sau mỗi lần cài nên chờ Unity compile xong rồi mới cài tiếp.", _mutedStyle);
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            _showOnlyMissing = GUILayout.Toggle(_showOnlyMissing, "Chỉ hiện mục chưa cài", EditorStyles.miniButton, GUILayout.Width(150), GUILayout.Height(20));
            EditorGUILayout.EndHorizontal();
            DrawSeparatorLine(SeparatorColor, 1f);
        }

        /// <summary>Cột trái: 2 bước làm theo thứ tự + phần tham khảo/công cụ gấp lại.</summary>
        private void DrawGuideColumn()
        {
            DrawStepMediation();
            DrawStepInstallAll();
            DrawInstallOrderCard();
            DrawToolsCard();
        }

        private static bool HasDefine(string define)
        {
            if (string.IsNullOrEmpty(define))
                return false;

            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
            // So khớp từng symbol, không dùng Contains: tránh define này khớp nhầm khi là chuỗi con của define khác.
            foreach (string symbol in symbols.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (symbol.Trim().Equals(define, StringComparison.Ordinal))
                    return true;
            }

            return false;
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

        // ─── Cột trái: các bước hướng dẫn ────────────────────────────────────────

        /// <summary>Bước 1 — chọn mediation chính, kèm giải thích bộ pack tương ứng.</summary>
        private void DrawStepMediation()
        {
            BeginCard();
            DrawStepTitle(1, "Chọn mediation chính");

            EditorGUI.BeginDisabledGroup(IsInteractionLocked());
            var current = GetPrimaryMediationFromDefines();
            var next = (MediationProvider)EditorGUILayout.EnumPopup("Primary Mediation", current);
            if (next != current)
            {
                SetPrimaryMediationDefines(next);
                RefreshStatus();
            }

            EditorGUI.EndDisabledGroup();

            var pm = GetPrimaryMediationFromDefines();
            string planDesc = pm switch
            {
                MediationProvider.Admob =>
                    "Facebook, Firebase, AppsFlyer, GameAnalytics, Google Mobile Ads + 2 adapter bắt buộc (Unity Ads, IronSource).",
                MediationProvider.Max =>
                    "Facebook, Firebase, AppsFlyer, GameAnalytics, AppLovin MAX.",
                _ => "Facebook, Firebase, AppsFlyer, GameAnalytics, IronSource LevelPlay.",
            };

            EditorGUILayout.Space(4);
            GUILayout.Label("Lựa chọn này quyết định bộ package sẽ được cài ở bước 2:", _mutedStyle);
            GUILayout.Label("• " + planDesc, _descStyle);
            GUILayout.Label(
                "Giá trị được lưu bằng Scripting Define Symbol (không tạo asset trong Assets/), nên đổi mediation sẽ khiến Unity compile lại.",
                _mutedStyle);

            EndCard();
        }

        /// <summary>Bước 2 — tiến độ bộ pack cốt lõi + nút cài tất cả + các cảnh báo liên quan.</summary>
        private void DrawStepInstallAll()
        {
            var pm = GetPrimaryMediationFromDefines();
            var planned = GetPackagesForSdkSetup(pm);
            var missingAuto = planned.Where(p => !p.IsInstalled && CanAutoInstall(p)).ToList();
            var missingManual = planned.Where(p => !p.IsInstalled && !CanAutoInstall(p)).ToList();
            int installed = planned.Count(p => p.IsInstalled);

            BeginCard();
            DrawStepTitle(2, "Cài dependency cốt lõi");

            // Thanh tiến độ: nhìn là biết còn thiếu bao nhiêu.
            var barRect = EditorGUILayout.GetControlRect(false, 18f);
            float progress = planned.Count > 0 ? (float)installed / planned.Count : 1f;
            EditorGUI.ProgressBar(barRect, progress, $"{installed}/{planned.Count} package đã cài");
            EditorGUILayout.Space(6);

            EditorGUI.BeginDisabledGroup(IsInteractionLocked() || missingAuto.Count == 0);
            if (GUILayout.Button(
                    missingAuto.Count > 0
                        ? $"⬇  Cài tất cả mục còn thiếu ({missingAuto.Count})"
                        : "✓  Đã đủ dependency cốt lõi",
                    GUILayout.Height(30)))
            {
                if (missingAuto.Count > 0)
                    StartBatchInstall(planned, showGameAnalyticsSetupHintWhenComplete: true);
            }

            EditorGUI.EndDisabledGroup();

            GUILayout.Label(
                "Installer tải và import lần lượt theo đúng thứ tự phụ thuộc. Cứ để Unity compile giữa các bước — đừng tắt Editor giữa chừng.",
                _mutedStyle);

            var pendingBatch = LoadPendingBatch();
            if (pendingBatch.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Đang có lượt \"Cài tất cả\" chạy dở. Sau mỗi lần Unity compile/reload, phần còn lại sẽ tự chạy tiếp.",
                    MessageType.Info);
                if (GUILayout.Button("Hủy lượt cài đang chờ"))
                {
                    ClearPendingBatch();
                    Debug.Log("[GameUpSDK] Đã hủy lượt cài đang chờ.");
                }
            }

            if (pm == MediationProvider.Admob)
            {
                var missingRequiredAdapters = GetRequiredAdMobRuntimeAdapters().Where(p => !p.IsInstalled).ToList();
                if (missingRequiredAdapters.Count > 0)
                {
                    EditorGUILayout.HelpBox(
                        "Thiếu adapter AdMob bắt buộc: " + string.Join(", ", missingRequiredAdapters.Select(p => p.DisplayName)) +
                        ".\nHai adapter này cần cho waterfall trên AdMob console và để forward GDPR consent. Bấm \"Cài tất cả\" ở trên là đủ.",
                        MessageType.Warning);
                }
            }

            if (missingManual.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Không tự cài được: " + string.Join(", ", missingManual.Select(p => p.DisplayName)) +
                    ".\nThiếu file trong Packages~ và không có URL tải — hãy tải thủ công rồi Assets → Import Package → Custom Package…",
                    MessageType.Warning);
            }

            EndCard();
        }

        /// <summary>Thứ tự cài khuyến nghị — gấp lại để không chiếm chỗ.</summary>
        private void DrawInstallOrderCard()
        {
            BeginCard();
            _foldoutInstallOrder = EditorGUILayout.Foldout(_foldoutInstallOrder, "Thứ tự cài khuyến nghị (khi cài lẻ từng pack)", true, _foldoutTitleStyle);
            if (_foldoutInstallOrder)
            {
                EditorGUILayout.Space(4);
                GUILayout.Label("1.  Facebook SDK", _descStyle);
                GUILayout.Label("2.  Firebase (kèm EDM4U) — chờ compile + Android Resolver xong", _descStyle);
                GUILayout.Label("3.  Google Mobile Ads / LevelPlay / MAX — đúng với Primary Mediation ở bước 1", _descStyle);
                GUILayout.Label("4.  AppsFlyer", _descStyle);
                GUILayout.Label("5.  GameAnalytics", _descStyle);
                EditorGUILayout.Space(4);
                GUILayout.Label(
                    "Với AdMob, installer tự thêm adapter Unity Ads + IronSource. Các adapter network khác (AppLovin, Meta, Pangle…) nằm ở tab \"AdMob Mediation\" và không nằm trong \"Cài tất cả\".",
                    _mutedStyle);
            }

            EndCard();
        }

        /// <summary>Nhóm công cụ phụ: sửa cache Unity UI, dọn Facebook Examples.</summary>
        private void DrawToolsCard()
        {
            BeginCard();
            _foldoutTools = EditorGUILayout.Foldout(_foldoutTools, "Công cụ & xử lý sự cố", true, _foldoutTitleStyle);
            if (_foldoutTools)
            {
                EditorGUILayout.Space(4);
                DrawUgUiPackageCacheTroubleshootFoldout();
                EditorGUILayout.Space(6);
                DrawSeparatorLine(SeparatorColor, 1f);
                EditorGUILayout.Space(6);
                DrawFacebookExamplesCleanupSection();
            }

            EndCard();
        }

        private void DrawUgUiPackageCacheTroubleshootFoldout()
        {
            GUILayout.Label("Lỗi compile trong com.unity.ugui", EditorStyles.boldLabel);
            GUILayout.Label(
                "Console báo lỗi ở Library/PackageCache/com.unity.ugui (GraphicRaycaster, Dropdown, ListPool…) " +
                "thường là do cache gói Unity lệch phiên bản Editor, không phải lỗi mã GameUp SDK. " +
                "Luôn mở project bằng đúng phiên bản trong ProjectSettings/ProjectVersion.txt.",
                _descStyle);

            if (GUILayout.Button("Xóa Package Cache + ScriptAssemblies", GUILayout.Height(24)))
                RepairUnityPackageCacheWithConfirmation();
        }

        private const string FacebookExamplesAssetPath = "Assets/FacebookSDK/Examples";

        private static bool FacebookSdkExamplesFolderExists()
        {
            return AssetDatabase.IsValidFolder(FacebookExamplesAssetPath);
        }

        private void DrawFacebookExamplesCleanupSection()
        {
            GUILayout.Label("Facebook SDK — thư mục Examples", EditorStyles.boldLabel);
            GUILayout.Label(
                "Examples không cần cho production và hay gây lỗi compile. Cài qua installer thì thư mục này đã bị xóa tự động; " +
                "chỉ dùng nút dưới khi bạn import Facebook SDK bằng tay.",
                _descStyle);

            EditorGUI.BeginDisabledGroup(IsInstallOrDownloadBusy() || !FacebookSdkExamplesFolderExists());
            if (GUILayout.Button("Xóa Assets/FacebookSDK/Examples", GUILayout.Height(24)))
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
                GUILayout.Label("Không thấy thư mục (đã xóa hoặc chưa import Facebook SDK).", _mutedStyle);
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

        /// <summary>Firebase + AppsFlyer + bộ mediation cốt lõi theo lựa chọn. AdMob gồm thêm 2 adapter bắt buộc (Unity Ads + IronSource).</summary>
        private static List<PackageDef> GetPackagesForSdkSetup(MediationProvider mediation)
        {
            var list = new List<PackageDef>();

            void AddByAssembly(string assemblyName)
            {
                var p = s_packages.FirstOrDefault(x => x.AssemblyName == assemblyName);
                if (p != null && !list.Contains(p))
                    list.Add(p);
            }

            void AddPackage(PackageDef pkg)
            {
                if (pkg != null && !list.Contains(pkg))
                    list.Add(pkg);
            }

            AddByAssembly("Facebook.Unity.Editor");
            AddByAssembly("Firebase.App");
            AddByAssembly("AppsFlyer");
            AddByAssembly("GameAnalyticsSDK");

            if (mediation == MediationProvider.Admob)
            {
                AddByAssembly("GoogleMobileAds");
                foreach (var adapter in GetRequiredAdMobRuntimeAdapters())
                    AddPackage(adapter);
            }
            else if (mediation == MediationProvider.Max)
            {
                AddByAssembly("MaxSdk.Scripts");
            }
            else
            {
                AddByAssembly("Unity.LevelPlay");
            }

            return OrderedInstallSequence(list).ToList();
        }

        private static MediationProvider GetPrimaryMediationFromDefines()
        {
            if (HasDefine(GUDefinetion.PrimaryMediationAdMob)) return MediationProvider.Admob;
            if (HasDefine(GUDefinetion.PrimaryMediationMax)) return MediationProvider.Max;
            return MediationProvider.IronSource;
        }

        private static void SetPrimaryMediationDefines(MediationProvider mediation)
        {
            SetDefine(GUDefinetion.PrimaryMediationAdMob, mediation == MediationProvider.Admob);
            SetDefine(GUDefinetion.PrimaryMediationLevelPlay, mediation == MediationProvider.IronSource);
            SetDefine(GUDefinetion.PrimaryMediationMax, mediation == MediationProvider.Max);
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
            var items = OrderedInstallSequence(s_packages)
                .Where(p => includeAdMobAdapters || !p.IsAdMobMediationAdapter)
                .Where(p => !_showOnlyMissing || !p.IsInstalled)
                .ToList();

            if (items.Count == 0)
            {
                EditorGUILayout.Space(8);
                GUILayout.Label("Tất cả package trong danh sách đã được cài.", _mutedStyle);
                return;
            }

            bool drewRequired = false, drewOptional = false;
            foreach (var pkg in items)
            {
                if (pkg.Required && !drewRequired)
                {
                    DrawSectionHeader("BẮT BUỘC", "Không có sẽ không build/chạy được SDK.");
                    drewRequired = true;
                }

                if (!pkg.Required && !drewOptional)
                {
                    DrawSectionHeader("TÙY CHỌN", "Chỉ cài khi game thực sự dùng đến dịch vụ đó.");
                    drewOptional = true;
                }

                DrawPackageRow(pkg, allowPerPackageRemove);
            }
        }

        private void DrawAdMobMediationTab()
        {
            var adapters = OrderedInstallSequence(GetAdMobMediationAdapters()).ToList();
            int installedAdapterCount = adapters.Count(p => p.IsInstalled);

            EditorGUILayout.BeginHorizontal(_rowStyle);
            EditorGUILayout.BeginVertical();
            GUILayout.Label($"AdMob Mediation Adapters — {installedAdapterCount}/{adapters.Count} đã cài", _cardTitleStyle);
            GUILayout.Label(
                "Chỉ cần khi Primary Mediation = AdMob. Installer tự tải .zip, giải nén và import .unitypackage.",
                _mutedStyle);
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            _showOnlyMissing = GUILayout.Toggle(_showOnlyMissing, "Chỉ hiện mục chưa cài", EditorStyles.miniButton, GUILayout.Width(150), GUILayout.Height(20));
            EditorGUILayout.EndHorizontal();
            DrawSeparatorLine(SeparatorColor, 1f);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (GetPrimaryMediationFromDefines() != MediationProvider.Admob)
            {
                EditorGUILayout.HelpBox(
                    "Primary Mediation hiện không phải AdMob nên các adapter này chưa được dùng tới. " +
                    "Vẫn cài trước được, nhưng nhớ đổi Primary Mediation = AdMob ở tab Dependencies.",
                    MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "Unity Ads và IronSource là bắt buộc với GameUp SDK — đã nằm sẵn trong nút \"Cài tất cả\" ở tab Dependencies. " +
                "Các adapter còn lại chỉ cài khi bạn bật network tương ứng trên AdMob console.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            var shown = adapters.Where(p => !_showOnlyMissing || !p.IsInstalled).ToList();
            if (shown.Count == 0)
                GUILayout.Label("Không còn adapter nào chưa cài.", _mutedStyle);

            bool drewRequired = false, drewOptional = false;
            foreach (var adapter in shown)
            {
                if (adapter.RequiredForAdMobRuntime && !drewRequired)
                {
                    DrawSectionHeader("BẮT BUỘC CHO GAMEUP SDK", "Cần cho waterfall AdMob và forward GDPR consent.");
                    drewRequired = true;
                }

                if (!adapter.RequiredForAdMobRuntime && !drewOptional)
                {
                    DrawSectionHeader("NETWORK TÙY CHỌN", "Cài đúng network bạn đã bật trên AdMob Mediation.");
                    drewOptional = true;
                }

                DrawPackageRow(adapter, allowPerPackageRemove: true);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSectionHeader(string title, string hint = null)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(title, _sectionHeaderStyle);
            if (!string.IsNullOrEmpty(hint))
            {
                GUILayout.Space(6);
                GUILayout.Label(hint, _mutedStyle);
            }

            EditorGUILayout.EndHorizontal();
            DrawSeparatorLine(SeparatorColor, 1f);
            EditorGUILayout.Space(2);
        }

        private static void DrawSeparatorLine(Color color, float thickness = 1f)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, thickness + 2f);
            rect.y += 1f;
            rect.height = thickness;
            EditorGUI.DrawRect(rect, color);
        }

        // ─── Style, màu và helper layout ─────────────────────────────────────────

        private static Color InstalledColor => EditorGUIUtility.isProSkin ? new Color(0.40f, 0.78f, 0.45f) : new Color(0.16f, 0.55f, 0.24f);
        private static Color MissingColor => EditorGUIUtility.isProSkin ? new Color(0.90f, 0.45f, 0.40f) : new Color(0.72f, 0.22f, 0.18f);
        private static Color BusyColor => EditorGUIUtility.isProSkin ? new Color(0.45f, 0.66f, 0.95f) : new Color(0.18f, 0.42f, 0.78f);
        private static Color SeparatorColor => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.09f) : new Color(0f, 0f, 0f, 0.12f);

        private static Color Tint(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private void EnsureStyles()
        {
            if (_rowStyle != null) return;

            _rowStyle = new GUIStyle { padding = new RectOffset(12, 10, 8, 8) };
            _cardStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 8, 8) };
            _cardTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            _stepTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            _descStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 11 };
            _mutedStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            _sectionHeaderStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            _badgeStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            _foldoutTitleStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
        }

        private void BeginCard()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(_cardStyle);
        }

        private static void EndCard()
        {
            EditorGUILayout.EndVertical();
        }

        /// <summary>Tiêu đề "① Tên bước" cho các card hướng dẫn bên trái.</summary>
        private void DrawStepTitle(int step, string title)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"BƯỚC {step}", _sectionHeaderStyle, GUILayout.Width(52));
            GUILayout.Label(title, _stepTitleStyle);
            EditorGUILayout.EndHorizontal();
            DrawSeparatorLine(SeparatorColor, 1f);
            EditorGUILayout.Space(4);
        }

        private static void DrawVerticalSeparator()
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, SeparatorColor);
        }

        private void DrawPackageRow(PackageDef pkg, bool allowPerPackageRemove)
        {
            bool isDownloading = _parallelTasks?.Any(t => t.Pkg == pkg && !t.IsDone) == true;
            bool isResolvingAdMobLatest = IsAdMobPackage(pkg) && _admobLatestReleaseRequest != null;
            bool isInstalling = pkg.IsInstalling
                                || (_isBatchInstalling && _installQueue.Contains(pkg))
                                || isDownloading
                                || isResolvingAdMobLatest;
            Color statusColor = pkg.IsInstalled ? InstalledColor : isInstalling ? BusyColor : MissingColor;
            string statusText = pkg.IsInstalled
                ? "ĐÃ CÀI"
                : isInstalling
                    ? "ĐANG XỬ LÝ"
                    : pkg.Required
                        ? "CHƯA CÀI · BẮT BUỘC"
                        : "CHƯA CÀI";

            const float ActionWidth = 190f;

            // Nền nhạt + vạch màu trạng thái bên trái để quét mắt nhanh theo cột.
            var rect = EditorGUILayout.BeginVertical(_rowStyle);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, Tint(statusColor, pkg.IsInstalled ? 0.07f : 0.12f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), statusColor);
            }

            EditorGUILayout.BeginHorizontal();

            // ── Cột thông tin ──
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(pkg.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            _badgeStyle.normal.textColor = statusColor;
            GUILayout.Label(statusText, _badgeStyle);
            GUILayout.Label(pkg.Description, _descStyle);
            if (!string.IsNullOrEmpty(pkg.InstallError))
                EditorGUILayout.HelpBox(pkg.InstallError, MessageType.Error);
            EditorGUILayout.EndVertical();

            // ── Cột hành động (cố định bề ngang cho thẳng hàng) ──
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical(GUILayout.Width(ActionWidth));

            if (isResolvingAdMobLatest)
            {
                float anim = Mathf.PingPong((float)EditorApplication.timeSinceStartup * 0.9f, 1f);
                GUILayout.Label("Đang kiểm tra release AdMob mới nhất…", _mutedStyle, GUILayout.Width(ActionWidth));
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Width(ActionWidth), GUILayout.Height(6)), anim, "");
            }
            else if (isDownloading)
            {
                var pkgTasks = _parallelTasks?.Where(t => t.Pkg == pkg).ToList();
                int total = pkgTasks?.Count ?? 0;
                int done = pkgTasks?.Count(t => t.IsDone) ?? 0;
                float prog = total > 0
                    ? pkgTasks.Average(t => t.IsDone ? 1f : t.Request?.downloadProgress ?? 0f)
                    : 0f;

                GUILayout.Label(total > 1 ? $"Đang tải… {done}/{total} file" : "Đang tải…", _mutedStyle, GUILayout.Width(ActionWidth));
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Width(ActionWidth), GUILayout.Height(6)), prog, $"{Mathf.RoundToInt(prog * 100f)}%");
            }
            else if (isInstalling)
            {
                GUILayout.Label("Đang import / compile…", _mutedStyle, GUILayout.Width(ActionWidth));
            }
            else if (!pkg.IsInstalled)
            {
                if (CanAutoInstall(pkg))
                {
                    EditorGUI.BeginDisabledGroup(IsInteractionLocked());
                    if (GUILayout.Button("⬇  Cài pack", GUILayout.Width(ActionWidth), GUILayout.Height(24)))
                        StartSinglePackageInstall(pkg);
                    EditorGUI.EndDisabledGroup();
                }
                else if (pkg.Method == InstallMethod.OpenUrl)
                {
                    if (GUILayout.Button("Mở trang tải", GUILayout.Width(ActionWidth), GUILayout.Height(24))
                        && !string.IsNullOrEmpty(pkg.DownloadUrl))
                        Application.OpenURL(pkg.DownloadUrl);
                }
                else
                {
                    GUILayout.Label("Cần tải thủ công (thiếu file trong Packages~ và URL).", _mutedStyle, GUILayout.Width(ActionWidth));
                }
            }

            if (IsAdMobPackage(pkg))
            {
                EditorGUI.BeginDisabledGroup(IsInstallOrDownloadBusy());
                if (GUILayout.Button("Cập nhật bản AdMob mới nhất", GUILayout.Width(ActionWidth), GUILayout.Height(22)))
                    StartAdMobLatestUpdate(pkg);
                EditorGUI.EndDisabledGroup();
            }

            if (allowPerPackageRemove && pkg.IsInstalled)
            {
                bool isAdapter = pkg.IsAdMobMediationAdapter;
                bool canRemove = isAdapter ? HasInstalledAssetPath(pkg) : CanRemovePackage(pkg);
                EditorGUI.BeginDisabledGroup(IsInstallOrDownloadBusy() || !canRemove);
                if (GUILayout.Button(isAdapter ? "Gỡ adapter" : "Gỡ package", GUILayout.Width(ActionWidth), GUILayout.Height(22)))
                {
                    if (isAdapter) ConfirmAndRemoveAdMobAdapter(pkg);
                    else ConfirmAndRemovePackage(pkg);
                }

                EditorGUI.EndDisabledGroup();
            }

            if (!string.IsNullOrEmpty(pkg.DownloadUrl) && !string.IsNullOrEmpty(pkg.DownloadLabel) && !pkg.IsInstalled && CanAutoInstall(pkg))
            {
                if (GUILayout.Button(pkg.DownloadLabel, EditorStyles.miniLabel, GUILayout.Width(ActionWidth)))
                    Application.OpenURL(pkg.DownloadUrl);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            DrawSeparatorLine(SeparatorColor, 1f);
        }

        /// <summary>Thanh dưới cùng: luôn hiện, cho biết bước tiếp theo là gì.</summary>
        private void DrawFooter()
        {
            DrawSeparatorLine(SeparatorColor, 1f);

            bool allRequiredDone = AreAllRequiredPackagesInstalled();
            var rect = EditorGUILayout.BeginHorizontal(_rowStyle);
            EditorGUI.DrawRect(rect, Tint(allRequiredDone ? InstalledColor : MissingColor, 0.10f));

            EditorGUILayout.BeginVertical();
            GUILayout.Label(
                allRequiredDone ? "✓  Đã đủ package bắt buộc" : "Chưa đủ package bắt buộc",
                EditorStyles.boldLabel);
            GUILayout.Label(
                allRequiredDone
                    ? "Bước cuối: mở cửa sổ cấu hình để nhập key cho từng network."
                    : "Cài nốt các mục đánh dấu BẮT BUỘC trong danh sách bên phải rồi quay lại đây.",
                _mutedStyle);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(IsInteractionLocked() || !allRequiredDone);
            if (GUILayout.Button("Mở cấu hình SDK  →", GUILayout.Width(200), GUILayout.Height(32)))
                RequestOpenSetup();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
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
            var removablePkgs = s_packages
                .Where(p => !p.IsAdMobMediationAdapter && CanRemovePackage(p))
                .ToList();

            BeginCard();
            GUILayout.Label("Gỡ toàn bộ dependencies", _cardTitleStyle);
            GUILayout.Label(
                "Xóa các SDK bên thứ ba (Facebook, Firebase, AdMob, …) và dọn luôn define tương ứng, để tránh trạng thái nửa vời khi cài lại. " +
                "GameUp Core và dependencies của Core (vd DOTween) được giữ nguyên.",
                _descStyle);

            EditorGUI.BeginDisabledGroup(IsInstallOrDownloadBusy() || removablePkgs.Count == 0);
            if (GUILayout.Button(
                    removablePkgs.Count > 0
                        ? $"Gỡ toàn bộ SDK dependencies ({removablePkgs.Count})"
                        : "Không còn dependency nào để gỡ",
                    GUILayout.Height(26)))
            {
                ConfirmAndRemoveSetupDependenciesBulk(removablePkgs);
            }

            EditorGUI.EndDisabledGroup();
            GUILayout.Label(
                "Sau khi gỡ, bước 2 sẽ lại báo \"còn N mục\" — đó là gợi ý cài lại, không phải file còn sót.",
                _mutedStyle);
            EndCard();
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

        private void StartBatchInstall(
            IReadOnlyList<PackageDef> scope,
            bool showGameAnalyticsSetupHintWhenComplete = false,
            bool isResume = false)
        {
            _gameAnalyticsSetupHintAfterBatch = showGameAnalyticsSetupHintWhenComplete;
            _batchScope = OrderedInstallSequence(
                    scope != null && scope.Count > 0
                        ? scope.Distinct()
                        : s_packages)
                .ToList();
            _isBatchInstalling = true;
            _installQueue.Clear();

            // Import .unitypackage sẽ kéo theo compile + domain reload → mọi state trong RAM mất sạch.
            // Ghi scope xuống SessionState để chạy tiếp sau reload (xem TryResumePendingBatch).
            SavePendingBatch(_batchScope, showGameAnalyticsSetupHintWhenComplete, isResume);

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

            // 2a) ScopedRegistry: sửa manifest.json rồi để UPM tự resolve.
            foreach (var pkg in InScope())
            {
                if (pkg.IsInstalled) continue;
                if (pkg.Method != InstallMethod.ScopedRegistry) continue;

                pkg.InstallError = null;
                AddScopedRegistryAndPackage(pkg);
            }

            // 2b) GitUrl: xếp hàng cho Client.Add (bất đồng bộ, chạy tuần tự).
            foreach (var pkg in InScope())
            {
                if (pkg.IsInstalled) continue;
                if (pkg.Method != InstallMethod.GitUrl) continue;

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
                _batchScope = null;
                RefreshStatus();

                // Chưa clear SessionState ở đây: import còn chạy nền và có thể kéo theo domain reload.
                // TryResumePendingBatch mới là nơi xác nhận đã cài đủ (hoặc chạy tiếp phần còn thiếu).
                EditorApplication.delayCall += () =>
                {
                    if (this == null) return;
                    TryResumePendingBatch();
                };
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

        // ─── Batch install bền vững qua domain reload ────────────────────────────

        private const string SessionKeyBatchScope = "GameUp.Installer.BatchScope";
        private const string SessionKeyBatchGaHint = "GameUp.Installer.BatchGaHint";
        private const string SessionKeyBatchRounds = "GameUp.Installer.BatchRounds";

        /// <summary>Số lần được phép chạy tiếp sau reload — chặn vòng lặp cài đi cài lại khi detect sai.</summary>
        private const int MaxBatchResumeRounds = 3;

        private static void SavePendingBatch(IEnumerable<PackageDef> scope, bool gaHint, bool isResume)
        {
            var indices = scope
                .Select(PackageIndexInCatalog)
                .Where(i => i >= 0 && i < s_packages.Length)
                .Distinct()
                .ToList();

            if (indices.Count == 0)
            {
                ClearPendingBatch();
                return;
            }

            SessionState.SetString(SessionKeyBatchScope, string.Join(",", indices));
            SessionState.SetBool(SessionKeyBatchGaHint, gaHint);
            if (!isResume)
                SessionState.SetInt(SessionKeyBatchRounds, 0);
        }

        private static List<PackageDef> LoadPendingBatch()
        {
            var result = new List<PackageDef>();
            string raw = SessionState.GetString(SessionKeyBatchScope, string.Empty);
            if (string.IsNullOrEmpty(raw))
                return result;

            foreach (string token in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(token, out int index) && index >= 0 && index < s_packages.Length)
                    result.Add(s_packages[index]);
            }

            return result;
        }

        private static void ClearPendingBatch()
        {
            SessionState.EraseString(SessionKeyBatchScope);
            SessionState.EraseBool(SessionKeyBatchGaHint);
            SessionState.EraseInt(SessionKeyBatchRounds);
        }

        // ─── Post-import cleanup bền vững qua domain reload ──────────────────────

        private const string SessionKeyPendingCleanup = "GameUp.Installer.PendingCleanup";

        /// <summary>
        /// Ghi nhận package cần dọn asset sau import (vd Facebook Examples). Nếu Unity reload trước khi
        /// importPackageCompleted kịp bắn, cleanup vẫn được chạy lại ở lần OnEnable kế tiếp.
        /// </summary>
        private static void MarkPendingPostImportCleanup(PackageDef pkg)
        {
            if (pkg?.DeleteAssetPathsAfterImport == null || pkg.DeleteAssetPathsAfterImport.Length == 0)
                return;

            int index = PackageIndexInCatalog(pkg);
            if (index < 0 || index >= s_packages.Length)
                return;

            var indices = LoadPendingCleanupIndices();
            if (indices.Add(index))
                SessionState.SetString(SessionKeyPendingCleanup, string.Join(",", indices));
        }

        private static void UnmarkPendingPostImportCleanup(PackageDef pkg)
        {
            int index = PackageIndexInCatalog(pkg);
            var indices = LoadPendingCleanupIndices();
            if (!indices.Remove(index))
                return;

            if (indices.Count == 0)
                SessionState.EraseString(SessionKeyPendingCleanup);
            else
                SessionState.SetString(SessionKeyPendingCleanup, string.Join(",", indices));
        }

        private static HashSet<int> LoadPendingCleanupIndices()
        {
            var result = new HashSet<int>();
            string raw = SessionState.GetString(SessionKeyPendingCleanup, string.Empty);
            if (string.IsNullOrEmpty(raw))
                return result;

            foreach (string token in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(token, out int index) && index >= 0 && index < s_packages.Length)
                    result.Add(index);
            }

            return result;
        }

        /// <summary>Chạy lại cleanup còn tồn đọng sau khi Unity reload (gọi trong OnEnable).</summary>
        private static void RunPendingPostImportCleanups()
        {
            var indices = LoadPendingCleanupIndices();
            if (indices.Count == 0)
                return;

            foreach (int index in indices)
                ApplyPostImportCleanup(s_packages[index]);

            SessionState.EraseString(SessionKeyPendingCleanup);
        }

        /// <summary>
        /// Chạy tiếp batch đang dở: gọi sau domain reload, sau import xong và sau khi một batch kết thúc.
        /// Không còn gì thiếu → dọn SessionState và báo hoàn tất.
        /// </summary>
        private void TryResumePendingBatch()
        {
            var pending = LoadPendingBatch();
            if (pending.Count == 0)
                return;

            // Còn việc đang chạy (download/import/compile) → để nhịp sau xử lý.
            if (IsInstallOrDownloadBusy() || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            bool gaHint = SessionState.GetBool(SessionKeyBatchGaHint, false);
            var missing = pending.Where(p => !p.IsInstalled && CanAutoInstall(p)).ToList();

            if (missing.Count == 0)
            {
                ClearPendingBatch();
                _gameAnalyticsSetupHintAfterBatch = false;
                if (gaHint)
                    NotifyGameAnalyticsAsmdefHint(fromMediationInstallAllBatch: true);
                Debug.Log("[GameUpSDK] Đã cài xong toàn bộ dependency trong lượt \"Cài tất cả\".");
                Repaint();
                return;
            }

            int rounds = SessionState.GetInt(SessionKeyBatchRounds, 0);
            if (rounds >= MaxBatchResumeRounds)
            {
                ClearPendingBatch();
                Debug.LogWarning(
                    "[GameUpSDK] Dừng cài tự động sau " + MaxBatchResumeRounds + " lượt. Vẫn chưa cài được: " +
                    string.Join(", ", missing.Select(p => p.DisplayName)) +
                    ". Hãy cài từng pack và xem lỗi trong Console.");
                ShowNotification(new GUIContent("Còn " + missing.Count + " package chưa cài được — xem Console."));
                Repaint();
                return;
            }

            SessionState.SetInt(SessionKeyBatchRounds, rounds + 1);
            Debug.Log($"[GameUpSDK] Tiếp tục cài {missing.Count} package còn lại sau khi Unity reload (lượt {rounds + 1}/{MaxBatchResumeRounds}).");
            StartBatchInstall(missing, gaHint, isResume: true);
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
        /// ImportPackage chạy BẤT ĐỒNG BỘ: package chỉ được coi là cài xong khi Unity bắn
        /// importPackageCompleted (xem <see cref="FinishPendingImport"/>).
        /// </summary>
        private void ImportUnityPackage(PackageDef pkg, List<string> filePaths)
        {
            // Không xóa InstallError ở đây: caller đã clear khi bắt đầu, và lỗi tải file trước đó
            // phải được giữ lại để package không bị đánh dấu "đã cài" một cách sai lệch.
            pkg.IsInstalling = true;
            Repaint();

            var errors = new List<string>();
            int dispatched = 0;

            foreach (string path in filePaths)
            {
                // Unity trả về tên file không đuôi trong importPackage* callback → dùng làm khóa chờ.
                string key = Path.GetFileNameWithoutExtension(path);
                try
                {
                    _pendingImports[key] = pkg;
                    _pendingImportsStartedAt = EditorApplication.timeSinceStartup;
                    MarkPendingPostImportCleanup(pkg);
                    AssetDatabase.ImportPackage(path, interactive: false);
                    dispatched++;
                    Debug.Log($"[GameUpSDK] Bắt đầu import: {Path.GetFileName(path)}");
                }
                catch (Exception ex)
                {
                    _pendingImports.Remove(key);
                    errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
                    Debug.LogError($"[GameUpSDK] Import {Path.GetFileName(path)} thất bại: {ex.Message}");
                }
            }

            if (errors.Count > 0)
                pkg.InstallError = "Một số file import thất bại:\n" + string.Join("\n", errors);

            // Không có file nào được dispatch → không có callback nào để chờ.
            if (dispatched == 0)
                pkg.IsInstalling = false;

            AssetDatabase.Refresh();
            Repaint();
        }

        /// <summary>Còn file .unitypackage nào của package này đang chờ Unity import xong?</summary>
        private bool HasPendingImport(PackageDef pkg)
        {
            if (pkg == null || _pendingImports.Count == 0)
                return false;

            foreach (var entry in _pendingImports)
            {
                if (ReferenceEquals(entry.Value, pkg))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Xử lý khi Unity báo import xong/thất bại/hủy một file .unitypackage.
        /// Đây mới là thời điểm asset thực sự nằm trên disk — cleanup (vd xóa Facebook Examples)
        /// phải chạy ở đây, không phải ngay sau khi gọi ImportPackage.
        /// </summary>
        private void FinishPendingImport(string packageName, bool success, string error)
        {
            if (string.IsNullOrEmpty(packageName))
                return;
            if (!_pendingImports.TryGetValue(packageName, out var pkg) || pkg == null)
                return;

            _pendingImports.Remove(packageName);

            if (success)
            {
                ApplyPostImportCleanup(pkg);
                // Một số file rơi xuống disk trễ hơn callback một nhịp → dọn lại lần nữa cho chắc.
                EditorApplication.delayCall += () =>
                {
                    ApplyPostImportCleanup(pkg);
                    UnmarkPendingPostImportCleanup(pkg);
                };
            }
            else
            {
                string detail = string.IsNullOrEmpty(error) ? "Import không hoàn tất." : error;
                pkg.InstallError = string.IsNullOrEmpty(pkg.InstallError)
                    ? $"{packageName}: {detail}"
                    : pkg.InstallError + $"\n{packageName}: {detail}";
            }

            if (HasPendingImport(pkg))
                return;

            pkg.IsInstalling = false;
            if (string.IsNullOrEmpty(pkg.InstallError))
            {
                pkg.IsInstalled = true;
                EnsureGameAnalyticsAsmdefAfterImport(pkg);
            }

            Repaint();
        }

        /// <summary>
        /// GA .unitypackage không kèm asmdef → pass compile đầu sẽ lỗi thiếu assembly GameAnalyticsSDK.
        /// Chỉ tạo được sau khi GameAnalytics.cs đã nằm trên disk.
        /// </summary>
        private void EnsureGameAnalyticsAsmdefAfterImport(PackageDef pkg)
        {
            if (!IsGameAnalyticsSdkPackage(pkg))
                return;

            if (GameUpDefineSymbolsAutoSync.TryEnsureGameAnalyticsRuntimeAsmdef(
                    out string asmdefMsg, out bool createdAsmdef))
            {
                if (createdAsmdef)
                    Debug.Log("[GameUp] " + asmdefMsg);
                return;
            }

            Debug.LogWarning("[GameUp] " + asmdefMsg);
            if (!_gameAnalyticsSetupHintAfterBatch)
                NotifyGameAnalyticsAsmdefHint(fromMediationInstallAllBatch: false);
        }

        /// <summary>
        /// Callback import có thể không bao giờ bắn (import bị Unity bỏ qua) → tránh kẹt cờ IsInstalling.
        /// </summary>
        private void DropStalePendingImports()
        {
            if (_pendingImports.Count == 0)
                return;
            if (EditorApplication.timeSinceStartup - _pendingImportsStartedAt < PendingImportTimeoutSeconds)
                return;

            var stalePackages = _pendingImports.Values.Distinct().ToList();
            _pendingImports.Clear();

            foreach (var pkg in stalePackages)
            {
                if (pkg == null) continue;
                pkg.IsInstalling = false;
                if (string.IsNullOrEmpty(pkg.InstallError))
                    pkg.InstallError = "Không nhận được thông báo import xong từ Unity. Bấm \"Làm mới\" để kiểm tra lại.";
            }

            Debug.LogWarning("[GameUpSDK] Quá thời gian chờ import: " +
                             string.Join(", ", stalePackages.Where(p => p != null).Select(p => p.DisplayName)));
            Repaint();
        }

        // ─── Parallel Download & Import ───────────────────────────────────────────

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
            op.completed += _ =>
            {
                // Window có thể đã bị đóng khi request về → không thao tác trên instance đã destroy.
                if (this == null)
                {
                    req.Dispose();
                    return;
                }

                ResolveAndInstallLatestAdMob(pkg, req);
            };
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
                if (pkg.HostedUrls == null || pkg.HostedUrls.Length == 0)
                {
                    // Không có gì để tải → đừng để cờ IsInstalling treo vĩnh viễn.
                    pkg.IsInstalling = false;
                    pkg.InstallError = "Không có URL tải cho package này.";
                    continue;
                }

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

            // Progress từng dòng được vẽ trực tiếp từ _parallelTasks trong DrawPackageRow.
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

        internal static bool IsDepsReadyDefined() => HasDefine(GUDefinetion.DepsReadyDefine);

        // ─── Status refresh ───────────────────────────────────────────────────────

        private void RefreshStatus(bool syncDefines = true)
        {
            if (syncDefines)
                EnsurePrimaryMediationDefines();

            foreach (var pkg in s_packages)
            {
                pkg.IsInstalled = IsPackageInstalled(pkg);

                // Còn file đang chờ Unity import xong thì giữ nguyên trạng thái "đang xử lý".
                if (!HasPendingImport(pkg))
                    pkg.IsInstalling = false;

                // Chỉ xóa lỗi khi package đã thực sự cài được — trước đây clear vô điều kiện nên
                // lỗi cài luôn biến mất trước khi người dùng kịp đọc (RefreshStatus chạy rất thường xuyên).
                if (pkg.IsInstalled)
                    pkg.InstallError = null;
            }

            if (!syncDefines)
            {
                Repaint();
                return;
            }

            // Auto set/clear Facebook define (Editor assembly = SDK đã import)
            bool facebookInstalled = IsPackageInstalled(FindPackageByAssembly("Facebook.Unity.Editor"));
            if (facebookInstalled && !HasDefine(FacebookDepsDefine))
                SetDefine(FacebookDepsDefine, true);
            else if (!facebookInstalled && HasDefine(FacebookDepsDefine))
                SetDefine(FacebookDepsDefine, false);

            // Auto set/clear LevelPlay define theo trạng thái package
            bool levelPlayInstalled = IsPackageInstalled(FindPackageByAssembly("Unity.LevelPlay"));
            if (levelPlayInstalled && !HasDefine(LevelPlayDepsDefine))
                SetDefine(LevelPlayDepsDefine, true);
            else if (!levelPlayInstalled && HasDefine(LevelPlayDepsDefine))
                SetDefine(LevelPlayDepsDefine, false);

            bool maxInstalled = IsPackageInstalled(FindPackageByAssembly("MaxSdk.Scripts"));
            if (maxInstalled && !HasDefine(MaxSdkDepsDefine))
                SetDefine(MaxSdkDepsDefine, true);
            else if (!maxInstalled && HasDefine(MaxSdkDepsDefine))
                SetDefine(MaxSdkDepsDefine, false);

            // Auto set/clear AdMob define theo AdMob core package.
            bool admobInstalled = IsAdMobCoreInstalled();
            if (admobInstalled && !HasDefine(AdMobDepsDefine))
                SetDefine(AdMobDepsDefine, true);
            else if (!admobInstalled && HasDefine(AdMobDepsDefine))
                SetDefine(AdMobDepsDefine, false);

            bool firebaseInstalled = IsPackageInstalled(FindPackageByAssembly("Firebase.App"));
            if (firebaseInstalled && !HasDefine(FirebaseDepsDefine))
                SetDefine(FirebaseDepsDefine, true);
            else if (!firebaseInstalled && HasDefine(FirebaseDepsDefine))
                SetDefine(FirebaseDepsDefine, false);

            bool appMetricaInstalled = IsPackageInstalled(FindPackageByAssembly("AppMetrica"));
            if (appMetricaInstalled && !HasDefine(AppmetricaDepsDefine))
                SetDefine(AppmetricaDepsDefine, true);
            else if (!appMetricaInstalled && HasDefine(AppmetricaDepsDefine))
                SetDefine(AppmetricaDepsDefine, false);

            bool appsFlyerInstalled = IsPackageInstalled(FindPackageByAssembly("AppsFlyer"));
            if (appsFlyerInstalled && !HasDefine(AppsFlyerDepsDefine))
                SetDefine(AppsFlyerDepsDefine, true);
            else if (!appsFlyerInstalled && HasDefine(AppsFlyerDepsDefine))
                SetDefine(AppsFlyerDepsDefine, false);

            bool gameAnalyticsInstalled = IsPackageInstalled(FindPackageByAssembly("GameAnalyticsSDK"));
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

            // Package import bằng .unitypackage: ưu tiên asset trên disk — assembly có thể còn trong AppDomain sau khi gỡ.
            var removablePaths = GetRemovableAssetPaths(pkg);
            if (removablePaths.Count > 0)
            {
                if (!removablePaths.Any(AssetPathExists))
                    return false;
            }

            bool byAssembly = !string.IsNullOrEmpty(pkg.AssemblyName) && IsAssemblyLoaded(pkg.AssemblyName);
            bool byAssetPath = HasInstalledAssetPath(pkg);
            bool byType = !string.IsNullOrEmpty(pkg.InstalledTypeFullName) &&
                          IsTypeInAnyLoadedAssembly(pkg.InstalledTypeFullName);

            // AdMob mediation adapters cần phản ánh trạng thái thư mục asset thực tế.
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

        private static PackageDef FindPackageByAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return null;

            return s_packages.FirstOrDefault(p =>
                !p.IsAdMobMediationAdapter &&
                string.Equals(p.AssemblyName, assemblyName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool AssetPathExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path))
                return true;

            return !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path));
        }

        private static bool CanRemovePackage(PackageDef pkg)
        {
            return GetRemovableAssetPaths(pkg).Any(AssetPathExists);
        }

        private void ConfirmAndRemovePackage(PackageDef pkg)
        {
            if (pkg == null)
                return;

            var existingPaths = FilterSdkRemovablePaths(
                    GetRemovableAssetPaths(pkg).Where(AssetPathExists))
                .OrderByDescending(p => p.Length)
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

            ClearPendingBatch();

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

            ClearPendingBatch();

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
                        .Where(AssetPathExists)
                        .Concat(GetSetupDependenciesResidualPaths().Where(AssetPathExists)))
                .OrderByDescending(p => p.Length)
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

            // Hủy batch đang chờ (nếu có) — nếu không, luồng resume sau reload sẽ cài lại thứ vừa gỡ.
            ClearPendingBatch();

            // Clear define trước để tránh conditionals/compile lệch trạng thái trong lúc gỡ.
            ClearDependencyDefinesAfterBulkRemove();

            TryDeleteAssets(existingPaths, out string error, out List<string> failedPaths);

            AssetDatabase.Refresh();
            RefreshStatus(syncDefines: true);

            if (failedPaths.Count == 0)
            {
                Debug.Log("[GameUpSDK] Đã gỡ toàn bộ SDK dependencies trong tab SetupDependencies (Core/DOTween giữ nguyên).");
                return;
            }

            string detail = error;
            detail += "\n\nPath chưa xóa được:\n• " + string.Join("\n• ", failedPaths);
            detail += "\n\nCác path khác có thể đã được gỡ. Bấm \"Làm mới trạng thái\" sau khi xóa thủ công phần còn sót.";

            EditorUtility.DisplayDialog(
                "GameUp SDK — Gỡ SDK dependencies (một phần)",
                detail,
                "OK");

            Debug.LogWarning(
                "[GameUpSDK] Gỡ SDK dependencies một phần. Còn sót: " + string.Join(", ", failedPaths));
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
        /// Residual do SDK third-party để lại (EDM4U, native plugins…).
        /// QUY TẮC: chỉ liệt kê path chắc chắn thuộc về dependency. Không bao giờ liệt kê thư mục dùng chung
        /// (Assets/Plugins, Assets/Plugins/Android, Assets/Editor, Assets/Resources, Assets/StreamingAssets)
        /// vì chúng chứa file build của game — vd mainTemplate.gradle, settingsTemplate.gradle.
        /// </summary>
        private static IEnumerable<string> GetSetupDependenciesResidualPaths()
        {
            return new[]
            {
                // EDM4U: dùng chung nhiều SDK nên chỉ dọn khi gỡ TOÀN BỘ dependencies.
                "Assets/ExternalDependencyManager",
                "Assets/Editor Default Resources/Firebase",
                "Assets/GeneratedLocalRepo/Firebase",
                "Assets/Plugins/iOS/Firebase",
                "Assets/tvOS/Firebase",
                "Assets/Plugins/tvOS/Firebase",
                "Assets/Plugins/iOS/GADUAdNetworkExtras.h",
                "Assets/Resources/GameAnalytics",

                // File .aar/androidlib cụ thể trong Assets/Plugins/Android — KHÔNG xóa cả thư mục.
                "Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib",
                "Assets/Plugins/Android/googlemobileads-unity.aar",
            };
        }

        /// <summary>
        /// Thư mục dùng chung của project — cấm xóa chính nó (vẫn cho phép xóa file/thư mục con cụ thể).
        /// Chặn cứng ở tầng xóa để một entry sai trong danh sách path không thể thổi bay cấu hình build của game.
        /// </summary>
        private static readonly string[] s_neverDeleteExactPaths =
        {
            "Assets",
            "Assets/Plugins",
            "Assets/Plugins/Android",
            "Assets/Plugins/iOS",
            "Assets/Plugins/tvOS",
            "Assets/Editor",
            "Assets/Editor Default Resources",
            "Assets/Resources",
            "Assets/StreamingAssets",
            "Assets/Scenes",
            "Assets/Scripts",
        };

        private static bool IsSharedProjectFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string normalized = path.Replace('\\', '/').TrimEnd('/');
            foreach (string shared in s_neverDeleteExactPaths)
            {
                if (normalized.Equals(shared, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsCoreProtectedAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (IsSharedProjectFolder(path))
                return true;

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
            return TryDeleteAssets(assetPaths, out error, out _);
        }

        private static bool TryDeleteAssets(List<string> assetPaths, out string error, out List<string> failedPaths)
        {
            error = null;
            failedPaths = new List<string>();
            if (assetPaths == null || assetPaths.Count == 0)
            {
                error = "Không có asset path để xóa.";
                return false;
            }

            foreach (string path in FilterSdkRemovablePaths(assetPaths).OrderByDescending(p => p.Length))
            {
                if (!TryDeleteSingleAssetPath(path, out string pathError))
                {
                    failedPaths.Add(path);
                    if (string.IsNullOrEmpty(error))
                        error = pathError;
                }
            }

            return failedPaths.Count == 0;
        }

        private static bool TryDeleteSingleAssetPath(string assetPath, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(assetPath) || IsCoreProtectedAssetPath(assetPath))
                return true;

            assetPath = assetPath.Replace('\\', '/');
            if (!AssetPathExists(assetPath))
                return true;

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { assetPath });
                foreach (string childPath in guids
                             .Select(AssetDatabase.GUIDToAssetPath)
                             .Where(p => !string.Equals(p, assetPath, StringComparison.OrdinalIgnoreCase))
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderByDescending(p => p.Length))
                {
                    if (!TryDeleteSingleAssetPath(childPath, out error))
                        return false;
                }
            }

            if (AssetDatabase.DeleteAsset(assetPath))
                return true;

            return TryDeleteAssetPathViaFileSystem(assetPath, out error);
        }

        private static bool TryDeleteAssetPathViaFileSystem(string assetPath, out string error)
        {
            error = null;
            try
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
                string metaPath = fullPath + ".meta";

                if (Directory.Exists(fullPath))
                    Directory.Delete(fullPath, recursive: true);
                else if (File.Exists(fullPath))
                    File.Delete(fullPath);
                else if (!AssetPathExists(assetPath))
                    return true;

                if (File.Exists(metaPath))
                    File.Delete(metaPath);

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (!AssetPathExists(assetPath))
                    return true;
            }
            catch (Exception ex)
            {
                error = $"Không xóa được {assetPath}: {ex.Message}";
                return false;
            }

            error = "AssetDatabase.DeleteAsset trả về false: " + assetPath;
            return false;
        }

        /// <summary>UPM có .asmdef GameAnalyticsSDK; .unitypackage chuẩn GA nằm trong Assembly-CSharp.</summary>
        internal static bool IsGameAnalyticsSdkPresent()
        {
            var pkg = FindPackageByAssembly("GameAnalyticsSDK");
            return pkg != null && IsPackageInstalled(pkg);
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