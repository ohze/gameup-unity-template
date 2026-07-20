using System;
using System.Reflection;
using GameUp.SDK;
using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    public static class GameUpSetupPaths
    {
        private static string _packageRoot;
        public static string PackageRoot
        {
            get
            {
                if (_packageRoot != null) return _packageRoot;
                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var pkgInfoType = Type.GetType("UnityEditor.PackageManager.PackageInfo, UnityEditor");
                    if (pkgInfoType != null)
                    {
                        var method = pkgInfoType.GetMethod("FindForAssembly", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(Assembly) }, null);
                        if (method != null)
                        {
                            var info = method.Invoke(null, new object[] { assembly });
                            var assetPathProp = pkgInfoType.GetProperty("assetPath");
                            var path = assetPathProp?.GetValue(info) as string;
                            if (!string.IsNullOrEmpty(path)) return _packageRoot = path;
                        }
                    }
                }
                catch { /* fallback */ }
                return _packageRoot = "Assets/GameUpSDK";
            }
        }

        public const string WritablePrefabsRoot = "Assets/SDK/Prefabs";

        public static string GetPackagePrefabDirectory() => (PackageRoot.Replace('\\', '/') + "/Prefab").Replace("//", "/");

        public static string GetPrefabDirectory()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WritablePrefabsRoot + "/SDK.prefab") != null)
                return WritablePrefabsRoot;
            return GetPackagePrefabDirectory();
        }

        public static string PathSDK => GetPrefabDirectory() + "/SDK.prefab";
        public static string PathAppsFlyer => GetPrefabDirectory() + "/AppsFlyerObject.prefab";
        public static string PathAppmetrica => GetPrefabDirectory() + "/AppmetricaObject.prefab";
        public static string PathIronSource => GetPrefabDirectory() + "/IronSourceAds.prefab";
        public static string PathMax => GetPrefabDirectory() + "/MaxAds.prefab";
        public static string PathAdMob => GetPrefabDirectory() + "/AdmobAds.prefab";

        public const string PathGoogleMobileAdsSettings = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
        public const string PathLevelPlayMediationSettings = "Assets/LevelPlay/Resources/LevelPlayMediationSettings.asset";
        public const string PathGameAnalyticsSettings = "Assets/Resources/GameAnalytics/Settings.asset";
        public const string PathFacebookSettings = "Assets/FacebookSDK/SDK/Resources/FacebookSettings.asset";
    }
}