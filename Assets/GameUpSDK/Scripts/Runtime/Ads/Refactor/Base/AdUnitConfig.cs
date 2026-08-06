using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameUp.SDK
{
    [Serializable]
    public class AdUnitConfig
    {
        [Tooltip("Bật nếu muốn sử dụng Waterfall 3 tầng (High -> Medium -> All). Tắt để dùng 1 ID tiêu chuẩn.")]
        public bool enableWaterfallFloor = false;

        [Tooltip("Bật nếu muốn cấu hình ID riêng cho từng vị trí (where). Tắt nếu muốn dùng chung ID mặc định.")]
        public bool useMultiAdUnitIds;

        [Header("Default IDs (Android)")]
        public string defaultIdAndroid_High;
        public string defaultIdAndroid_Medium;
        [FormerlySerializedAs("defaultIdAndroid")]
        public string defaultIdAndroid_All;

        [Header("Default IDs (iOS)")]
        public string defaultIdIOS_High;
        public string defaultIdIOS_Medium;
        [FormerlySerializedAs("defaultIdIOS")]
        public string defaultIdIOS_All;

        [Header("Default Banner Settings")]
        public BannerSize defaultBannerSize = BannerSize.Adaptive;
        public BannerFormatType defaultBannerFormat = BannerFormatType.StandardBanner;
        public CollapsibleBannerPlacement defaultCollapsible = CollapsibleBannerPlacement.None;

        [Header("Placements (Multi IDs)")]
        public List<AdPlacementIds> placementsAndroid = new List<AdPlacementIds>();
        public List<AdPlacementIds> placementsIOS = new List<AdPlacementIds>();

        // ---------------------------------------------------------------------
        // LEGACY (v1): dữ liệu phẳng theo từng floor, chỉ còn dùng để migrate
        // từ prefab cũ sang GameUpAdsConfig. Runtime KHÔNG đọc 2 list này.
        // ---------------------------------------------------------------------
        [HideInInspector] public List<AdUnitIdEntry> multiIdsAndroid = new List<AdUnitIdEntry>();
        [HideInInspector] public List<AdUnitIdEntry> multiIdsIOS = new List<AdUnitIdEntry>();

        public EcpmFloor[] GetActiveFloors()
        {
            return enableWaterfallFloor
                ? new[] { EcpmFloor.High, EcpmFloor.Medium, EcpmFloor.All }
                : new[] { EcpmFloor.All };
        }

        /// <summary>Danh sách placement của platform đang build (Android/iOS).</summary>
        public List<AdPlacementIds> GetPlacements() => GetPlacements(IsAndroidTarget());

        public List<AdPlacementIds> GetPlacements(bool android)
        {
            if (android) return placementsAndroid ??= new List<AdPlacementIds>();
            return placementsIOS ??= new List<AdPlacementIds>();
        }

        public AdPlacementIds FindPlacement(string where)
        {
            if (!useMultiAdUnitIds || string.IsNullOrWhiteSpace(where)) return null;

            var list = GetPlacements();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].Matches(where)) return list[i];
            }
            return null;
        }

        public AdUnitIdEntry GetEntry(AdUnitType type, string where)
        {
            return GetEntry(type, where, EcpmFloor.All);
        }

        public AdUnitIdEntry GetEntry(AdUnitType type, string where, EcpmFloor floor)
        {
            if (!enableWaterfallFloor) floor = EcpmFloor.All;

            var placement = FindPlacement(where);
            if (placement != null && placement.HasId(floor))
            {
                return new AdUnitIdEntry
                {
                    Id = placement.GetId(floor).Trim(),
                    AdType = type,
                    NameId = placement.where,
                    BannerSize = placement.bannerSize,
                    BannerFormat = placement.bannerFormat,
                    CollapsiblePlacement = placement.collapsible,
                    Floor = floor
                };
            }

            return new AdUnitIdEntry
            {
                Id = GetDefaultId(IsAndroidTarget(), floor),
                AdType = type,
                NameId = where,
                BannerSize = defaultBannerSize,
                BannerFormat = defaultBannerFormat,
                CollapsiblePlacement = defaultCollapsible,
                Floor = floor
            };
        }

        private string GetDefaultId(bool isAndroid, EcpmFloor floor)
        {
            if (isAndroid)
            {
                switch (floor)
                {
                    case EcpmFloor.High: return defaultIdAndroid_High;
                    case EcpmFloor.Medium: return defaultIdAndroid_Medium;
                    case EcpmFloor.All: default: return defaultIdAndroid_All;
                }
            }
            else
            {
                switch (floor)
                {
                    case EcpmFloor.High: return defaultIdIOS_High;
                    case EcpmFloor.Medium: return defaultIdIOS_Medium;
                    case EcpmFloor.All: default: return defaultIdIOS_All;
                }
            }
        }

        /// <summary>Tra ngược: từ Ad Unit ID thực tế ra tên placement (dùng khi callback chỉ trả unitId).</summary>
        public string WhereByKey(AdUnitType type, string key)
        {
            if (!useMultiAdUnitIds || string.IsNullOrWhiteSpace(key)) return "default";

            string trimmedKey = key.Trim();
            var list = GetPlacements();
            foreach (var placement in list)
            {
                if (placement == null) continue;
                foreach (var floor in AllFloors)
                {
                    var id = placement.GetId(floor);
                    if (!string.IsNullOrWhiteSpace(id) && string.Equals(id.Trim(), trimmedKey, StringComparison.OrdinalIgnoreCase))
                        return placement.where;
                }
            }
            return "default";
        }

        /// <summary>Tất cả placement có ID hợp lệ. Khi tắt Multi IDs chỉ có "default".</summary>
        public List<string> GetAllPlacements()
        {
            var placements = new List<string>();
            if (!useMultiAdUnitIds)
            {
                placements.Add("default");
                return placements;
            }

            foreach (var placement in GetPlacements())
            {
                if (placement == null || !placement.HasAnyId() || string.IsNullOrWhiteSpace(placement.where)) continue;

                string cleanName = placement.where.Trim();
                if (!placements.Contains(cleanName)) placements.Add(cleanName);
            }
            return placements;
        }

        public List<string> GetAllWhere() => GetAllPlacements();

        public string ResolveUnitId(AdUnitType type, string where, EcpmFloor floor = EcpmFloor.All)
        {
            return GetEntry(type, where, floor).Id;
        }

        // =====================================================================
        // MIGRATE v1 -> v2
        // =====================================================================

        public bool HasLegacyData => (multiIdsAndroid != null && multiIdsAndroid.Count > 0)
                                     || (multiIdsIOS != null && multiIdsIOS.Count > 0);

        /// <summary>Bản sao sâu (deep copy) đã gom dữ liệu legacy về dạng placement mới.</summary>
        public AdUnitConfig CloneMigrated()
        {
            var clone = JsonUtility.FromJson<AdUnitConfig>(JsonUtility.ToJson(this)) ?? new AdUnitConfig();
            clone.MigrateLegacyEntries();
            return clone;
        }

        /// <summary>Gom list phẳng (mỗi floor một dòng) thành placement gộp 3 floor. Trả về true nếu có đổi.</summary>
        public bool MigrateLegacyEntries()
        {
            bool changed = false;
            changed |= MigrateLegacyList(multiIdsAndroid, ref placementsAndroid);
            changed |= MigrateLegacyList(multiIdsIOS, ref placementsIOS);

            multiIdsAndroid = new List<AdUnitIdEntry>();
            multiIdsIOS = new List<AdUnitIdEntry>();
            return changed;
        }

        private static bool MigrateLegacyList(List<AdUnitIdEntry> legacy, ref List<AdPlacementIds> target)
        {
            if (legacy == null || legacy.Count == 0) return false;
            target ??= new List<AdPlacementIds>();

            bool changed = false;
            foreach (var entry in legacy)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.NameId)) continue;

                var placement = target.Find(p => p != null && p.Matches(entry.NameId));
                if (placement == null)
                {
                    placement = new AdPlacementIds { where = entry.NameId.Trim() };
                    target.Add(placement);
                    changed = true;
                }

                // Không ghi đè ID đã có ở bản mới (bản mới luôn được ưu tiên).
                if (!placement.HasId(entry.Floor) && !string.IsNullOrWhiteSpace(entry.Id))
                {
                    placement.SetId(entry.Floor, entry.Id.Trim());
                    changed = true;
                }

                if (entry.AdType == AdUnitType.Banner)
                {
                    placement.bannerFormat = entry.BannerFormat;
                    placement.bannerSize = entry.BannerSize;
                    placement.collapsible = entry.CollapsiblePlacement;
                }
            }
            return changed;
        }

        private static readonly EcpmFloor[] AllFloors = { EcpmFloor.High, EcpmFloor.Medium, EcpmFloor.All };

        private static bool IsAndroidTarget()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#elif (UNITY_IOS || UNITY_IPHONE) && !UNITY_EDITOR
            return false;
#elif UNITY_EDITOR
            return UnityEditor.EditorUserBuildSettings.activeBuildTarget != UnityEditor.BuildTarget.iOS;
#else
            return true;
#endif
        }
    }
}
