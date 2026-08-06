using System;
using GameUp.SDK;
using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    /// <summary>
    /// Tìm / tạo asset <see cref="GameUpSdkConfig"/> (AppsFlyer, AppMetrica, Remote Config defaults)
    /// và migrate dữ liệu cũ đang nằm trong prefab sang asset đó.
    /// </summary>
    public static class GameUpSdkConfigAsset
    {
        public static GameUpSdkConfig Find()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(GameUpSdkConfig));
            foreach (var guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameUpSdkConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) return asset;
            }
            return null;
        }

        public static GameUpSdkConfig GetOrCreate()
        {
            var existing = Find();
            if (existing != null) return existing;

            GameUpSetupPaths.EnsureFolderExists(GameUpSetupPaths.ConfigFolder);

            var asset = ScriptableObject.CreateInstance<GameUpSdkConfig>();
            AssetDatabase.CreateAsset(asset, GameUpSetupPaths.SdkConfigAssetPath);

            var report = MigrateFromPrefabs(asset, overwriteExisting: true);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameUpSdkConfig.ClearCache();

            Debug.Log($"[GameUp.SDK] Đã tạo {GameUpSetupPaths.SdkConfigAssetPath}." +
                      (string.IsNullOrEmpty(report) ? "" : "\n" + report));
            return asset;
        }

        [MenuItem("GameUp/SDK/Migrate SDK Config (Prefab → ScriptableObject)")]
        private static void MigrateMenu()
        {
            var asset = GetOrCreate();
            var report = MigrateFromPrefabs(asset, overwriteExisting: true);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            GameUpSdkConfig.ClearCache();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            EditorUtility.DisplayDialog("GameUp SDK",
                string.IsNullOrEmpty(report) ? "Không tìm thấy dữ liệu cũ trong prefab." : report, "OK");
        }

        public static string MigrateFromPrefabs(GameUpSdkConfig target, bool overwriteExisting)
        {
            if (target == null) return string.Empty;
            var lines = new System.Collections.Generic.List<string>();

            var appsFlyer = ReadAppsFlyerFromPrefab();
            if (appsFlyer != null && !string.IsNullOrWhiteSpace(appsFlyer.devKey)
                && (overwriteExisting || string.IsNullOrWhiteSpace(target.appsFlyer.devKey)))
            {
                target.appsFlyer = appsFlyer;
                lines.Add("• AppsFlyer: đã chuyển devKey / appID / debug.");
            }

            var appMetrica = GameUpSetupPaths.FindComponentInPrefabs<AppMetricaActivator>("AppmetricaObject.prefab");
            if (appMetrica != null)
            {
                var legacy = appMetrica.ExportLegacySettings();
                if (!string.IsNullOrWhiteSpace(legacy.apiKey)
                    && (overwriteExisting || string.IsNullOrWhiteSpace(target.appMetrica.apiKey)))
                {
                    target.appMetrica = legacy;
                    lines.Add("• AppMetrica: đã chuyển API key.");
                }
            }

            var rc = GameUpSetupPaths.FindComponentInPrefabs<FirebaseRemoteConfigUtils>("SDK.prefab");
            if (rc != null && (overwriteExisting || target.remoteConfig == null))
            {
                target.remoteConfig = rc.ExportLegacyDefaults();
                lines.Add("• Firebase Remote Config: đã chuyển bộ giá trị mặc định.");
            }

            return lines.Count == 0 ? string.Empty : "Migrate xong:\n" + string.Join("\n", lines);
        }

        public static bool HasPendingLegacyData(GameUpSdkConfig target)
        {
            if (target == null) return false;

            var appsFlyer = ReadAppsFlyerFromPrefab();
            if (appsFlyer != null && !string.IsNullOrWhiteSpace(appsFlyer.devKey)
                && string.IsNullOrWhiteSpace(target.appsFlyer.devKey)) return true;

            var appMetrica = GameUpSetupPaths.FindComponentInPrefabs<AppMetricaActivator>("AppmetricaObject.prefab");
            if (appMetrica != null && !string.IsNullOrWhiteSpace(appMetrica.ExportLegacySettings().apiKey)
                && string.IsNullOrWhiteSpace(target.appMetrica.apiKey)) return true;

            return false;
        }

        /// <summary>
        /// Đọc AppsFlyerObjectScript qua reflection — type nằm trong assembly của AppsFlyer SDK,
        /// có thể chưa được cài trong project.
        /// </summary>
        private static AppsFlyerSettings ReadAppsFlyerFromPrefab()
        {
            var type = Type.GetType("AppsFlyerObjectScript, AppsFlyer");
            if (type == null) return null;

            foreach (var path in GameUpSetupPaths.PrefabCandidates("AppsFlyerObject.prefab"))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var comp = go != null ? go.GetComponent(type) : null;
                if (comp == null) continue;

                var so = new SerializedObject(comp);
                return new AppsFlyerSettings
                {
                    devKey = so.FindProperty("devKey")?.stringValue ?? "",
                    appIdIOS = so.FindProperty("appID")?.stringValue ?? "",
                    isDebug = so.FindProperty("isDebug")?.boolValue ?? false,
                    getConversionData = so.FindProperty("getConversionData")?.boolValue ?? false
                };
            }
            return null;
        }
    }
}
