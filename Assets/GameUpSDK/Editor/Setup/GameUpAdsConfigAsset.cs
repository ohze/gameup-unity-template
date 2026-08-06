using System.Collections.Generic;
using System.Linq;
using GameUp.SDK;
using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    /// <summary>
    /// Tìm / tạo asset <see cref="GameUpAdsConfig"/> của project và migrate dữ liệu cũ
    /// đang nằm trong prefab sang asset đó.
    /// </summary>
    public static class GameUpAdsConfigAsset
    {
        /// <summary>Asset hiện có trong project (bất kể nằm ở đâu), null nếu chưa có.</summary>
        public static GameUpAdsConfig Find()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(GameUpAdsConfig));
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameUpAdsConfig>(path);
                if (asset != null) return asset;
            }
            return null;
        }

        /// <summary>Lấy asset, tự tạo tại Assets/_MainProject/Resources/GameUpSDK nếu chưa có.</summary>
        public static GameUpAdsConfig GetOrCreate()
        {
            var existing = Find();
            if (existing != null)
            {
                WarnIfOutsideResources(existing);
                return existing;
            }

            GameUpSetupPaths.EnsureFolderExists(GameUpSetupPaths.AdsConfigFolder);

            var asset = ScriptableObject.CreateInstance<GameUpAdsConfig>();
            AssetDatabase.CreateAsset(asset, GameUpSetupPaths.AdsConfigAssetPath);

            // Lần đầu tạo: kéo hết dữ liệu cũ trong prefab sang để không mất ID đã điền.
            var report = MigrateFromPrefabs(asset, overwriteExisting: true);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameUpAdsConfig.ClearCache();

            Debug.Log($"[GameUp.SDK] Đã tạo {GameUpSetupPaths.AdsConfigAssetPath}." +
                      (string.IsNullOrEmpty(report) ? "" : "\n" + report));
            return asset;
        }

        private static void WarnIfOutsideResources(GameUpAdsConfig asset)
        {
            var path = AssetDatabase.GetAssetPath(asset).Replace('\\', '/');
            if (!path.Contains("/Resources/"))
            {
                Debug.LogWarning($"[GameUp.SDK] {path} không nằm trong thư mục Resources nên bản build sẽ không load được. " +
                                 $"Hãy di chuyển asset vào {GameUpSetupPaths.AdsConfigFolder}.");
            }
        }

        // =====================================================================
        // MIGRATE: prefab (v1) -> ScriptableObject (v2)
        // =====================================================================

        [MenuItem("GameUp/SDK/Migrate Ads Config (Prefab → ScriptableObject)")]
        private static void MigrateMenu()
        {
            var asset = GetOrCreate();
            var report = MigrateFromPrefabs(asset, overwriteExisting: true);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            GameUpAdsConfig.ClearCache();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            EditorUtility.DisplayDialog("GameUp SDK",
                string.IsNullOrEmpty(report) ? "Không tìm thấy dữ liệu cũ trong prefab." : report, "OK");
        }

        /// <summary>Đọc dữ liệu ads còn sót trong các prefab và ghi vào asset. Trả về báo cáo dạng text.</summary>
        public static string MigrateFromPrefabs(GameUpAdsConfig target, bool overwriteExisting)
        {
            if (target == null) return string.Empty;
            var lines = new List<string>();

            // Waterfall + native CTA rate: trước đây nằm trên component AdsManager của SDK.prefab.
            var adsManager = GameUpSetupPaths.FindComponentInPrefabs<AdsManager>("SDK.prefab");
            if (adsManager != null && overwriteExisting)
            {
                adsManager.ExportLegacyInto(target);
                lines.Add("• AdsManager: đã chuyển thứ tự mediation + native CTA rate.");
            }

            var admob = GameUpSetupPaths.FindComponentInPrefabs<AdmobNetwork>("AdmobAds.prefab");
            if (admob != null)
            {
                var legacy = admob.ExportLegacySettings();
                if (HasData(legacy.units) && (overwriteExisting || !HasData(target.admob.units)))
                {
                    // App ID lấy từ GoogleMobileAdsSettings, không nằm trong prefab.
                    legacy.appIdAndroid = target.admob.appIdAndroid;
                    legacy.appIdIOS = target.admob.appIdIOS;
                    target.admob = legacy;
                    lines.Add("• AdMob: đã chuyển " + Describe(legacy.units));
                }
            }

            var max = GameUpSetupPaths.FindComponentInPrefabs<MaxNetwork>("MaxAds.prefab");
            if (max != null)
            {
                var legacy = max.ExportLegacySettings();
                if (HasData(legacy.units) && (overwriteExisting || !HasData(target.max.units)))
                {
                    target.max = legacy;
                    lines.Add("• MAX: đã chuyển " + Describe(legacy.units));
                }
            }

            var ironSource = GameUpSetupPaths.FindComponentInPrefabs<IronSourceNetwork>("IronSourceAds.prefab");
            if (ironSource != null)
            {
                var legacy = ironSource.ExportLegacySettings();
                if (HasData(legacy.units) && (overwriteExisting || !HasData(target.ironSource.units)))
                {
                    target.ironSource = legacy;
                    lines.Add("• IronSource: đã chuyển " + Describe(legacy.units));
                }
            }

            SyncAdmobAppIdsFromGoogleSettings(target);
            return lines.Count == 0 ? string.Empty : "Migrate xong:\n" + string.Join("\n", lines);
        }

        /// <summary>Còn dữ liệu ads trong prefab mà asset chưa có → hiện nút migrate trong cửa sổ Setup.</summary>
        public static bool HasPendingLegacyData(GameUpAdsConfig target)
        {
            if (target == null) return false;

            var admob = GameUpSetupPaths.FindComponentInPrefabs<AdmobNetwork>("AdmobAds.prefab");
            if (admob != null && HasData(admob.ExportLegacySettings().units) && !HasData(target.admob.units)) return true;

            var max = GameUpSetupPaths.FindComponentInPrefabs<MaxNetwork>("MaxAds.prefab");
            if (max != null && HasData(max.ExportLegacySettings().units) && !HasData(target.max.units)) return true;

            var ironSource = GameUpSetupPaths.FindComponentInPrefabs<IronSourceNetwork>("IronSourceAds.prefab");
            if (ironSource != null && HasData(ironSource.ExportLegacySettings().units) && !HasData(target.ironSource.units)) return true;

            return false;
        }

        public static void SyncAdmobAppIdsFromGoogleSettings(GameUpAdsConfig target)
        {
            if (target == null) return;
            if (!string.IsNullOrEmpty(target.admob.appIdAndroid) && !string.IsNullOrEmpty(target.admob.appIdIOS)) return;

            var googleAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(GameUpSetupPaths.PathGoogleMobileAdsSettings);
            if (googleAsset == null) return;

            var so = new SerializedObject(googleAsset);
            if (string.IsNullOrEmpty(target.admob.appIdAndroid))
                target.admob.appIdAndroid = so.FindProperty("adMobAndroidAppId")?.stringValue ?? "";
            if (string.IsNullOrEmpty(target.admob.appIdIOS))
                target.admob.appIdIOS = so.FindProperty("adMobIOSAppId")?.stringValue ?? "";
        }

        /// <summary>Đẩy App ID trong asset sang GoogleMobileAdsSettings.asset (bắt buộc với Google Mobile Ads).</summary>
        public static void PushAdmobAppIdsToGoogleSettings(GameUpAdsConfig source)
        {
            if (source == null) return;
            var googleAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(GameUpSetupPaths.PathGoogleMobileAdsSettings);
            if (googleAsset == null) return;

            var so = new SerializedObject(googleAsset);
            var android = so.FindProperty("adMobAndroidAppId");
            var ios = so.FindProperty("adMobIOSAppId");
            if (android != null) android.stringValue = source.admob.appIdAndroid ?? "";
            if (ios != null) ios.stringValue = source.admob.appIdIOS ?? "";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(googleAsset);
            // Thiếu SaveAssets thì App ID chỉ nằm trong bộ nhớ: đóng Unity không qua save,
            // hoặc build ngay sau khi bấm Save, sẽ dùng App ID cũ trong GoogleMobileAdsSettings.asset.
            AssetDatabase.SaveAssets();
        }

        // --- helpers ---



        public static bool HasData(AdUnitConfigSet set)
        {
            if (set == null) return false;
            return set.All().Any(HasData);
        }

        private static bool HasData(AdUnitConfig config)
        {
            if (config == null) return false;
            if (!string.IsNullOrWhiteSpace(config.defaultIdAndroid_All) || !string.IsNullOrWhiteSpace(config.defaultIdIOS_All)
                || !string.IsNullOrWhiteSpace(config.defaultIdAndroid_High) || !string.IsNullOrWhiteSpace(config.defaultIdIOS_High)
                || !string.IsNullOrWhiteSpace(config.defaultIdAndroid_Medium) || !string.IsNullOrWhiteSpace(config.defaultIdIOS_Medium))
                return true;

            return CountIds(config) > 0 || config.HasLegacyData;
        }

        private static int CountIds(AdUnitConfig config)
        {
            int count = 0;
            foreach (var list in new[] { config.placementsAndroid, config.placementsIOS })
            {
                if (list == null) continue;
                count += list.Count(p => p != null && p.HasAnyId());
            }
            return count;
        }

        private static string Describe(AdUnitConfigSet set)
        {
            int placements = set.All().Where(c => c != null).Sum(CountIds);
            return $"{placements} placement có ID.";
        }
    }
}
