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

        public const string WritablePrefabsRoot = "Assets/_MainProject/Prefabs/SDK";

        /// <summary>Nơi chứa asset cấu hình của project. Phải nằm trong Resources để build đọc được.</summary>
        public const string WritableResourcesRoot = "Assets/_MainProject/Resources";

        public static string ConfigFolder => WritableResourcesRoot + "/" + GameUpAdsConfig.ResourceFolder;
        public static string AdsConfigFolder => ConfigFolder;
        public static string AdsConfigAssetPath => ConfigFolder + "/" + GameUpAdsConfig.AssetName + ".asset";
        public static string SdkConfigAssetPath => ConfigFolder + "/" + GameUpSdkConfig.AssetName + ".asset";

        /// <summary>Đường dẫn prefab theo thứ tự ưu tiên: bản clone trong Assets trước, bản trong package sau.</summary>
        public static System.Collections.Generic.IEnumerable<string> PrefabCandidates(string fileName)
        {
            yield return WritablePrefabsRoot + "/" + fileName;
            yield return GetPackagePrefabDirectory() + "/" + fileName;
        }

        /// <summary>Component đầu tiên tìm thấy trong các prefab ứng viên (ưu tiên bản clone).</summary>
        public static T FindComponentInPrefabs<T>(string fileName) where T : Component
        {
            foreach (var path in PrefabCandidates(fileName))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var comp = go != null ? go.GetComponentInChildren<T>(true) : null;
                if (comp != null) return comp;
            }
            return null;
        }

        public static string GetPackagePrefabDirectory() => (PackageRoot.Replace('\\', '/') + "/Prefab").Replace("//", "/");

        /// <summary>Tạo folder đệ quy theo từng cấp (AssetDatabase.CreateFolder chỉ tạo được 1 cấp/lần).</summary>
        public static void EnsureFolderExists(string folderPath)
        {
            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

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