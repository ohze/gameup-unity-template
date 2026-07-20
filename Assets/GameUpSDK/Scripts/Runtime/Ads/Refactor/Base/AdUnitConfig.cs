using System;
using System.Collections.Generic;
using System.Linq;
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

        [Header("Multi IDs")] 
        public List<AdUnitIdEntry> multiIdsAndroid = new List<AdUnitIdEntry>();
        public List<AdUnitIdEntry> multiIdsIOS = new List<AdUnitIdEntry>();

        public EcpmFloor[] GetActiveFloors()
        {
            return enableWaterfallFloor 
                ? new[] { EcpmFloor.High, EcpmFloor.Medium, EcpmFloor.All } 
                : new[] { EcpmFloor.All };
        }

        public AdUnitIdEntry GetEntry(AdUnitType type, string where)
        {
            return GetEntry(type, where, EcpmFloor.All);
        }

        public AdUnitIdEntry GetEntry(AdUnitType type, string where, EcpmFloor floor)
        {
            if (!enableWaterfallFloor) floor = EcpmFloor.All;

            bool isAndroid = GetRuntimeAdPlatform() == RuntimeAdPlatform.Android;
            var multiIds = isAndroid ? multiIdsAndroid : multiIdsIOS;

            if (useMultiAdUnitIds && !string.IsNullOrWhiteSpace(where))
            {
                foreach (var entry in multiIds)
                {
                    if (entry != null && entry.AdType == type && entry.IsValid() &&
                        string.Equals(entry.NameId?.Trim(), where.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        entry.Floor == floor) 
                    {
                        return entry;
                    }
                }
            }

            return new AdUnitIdEntry
            {
                Id = GetDefaultId(isAndroid, floor),
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

        public string WhereByKey(AdUnitType type, string key)
        {
            bool isAndroid = GetRuntimeAdPlatform() == RuntimeAdPlatform.Android;
            var multiIds = isAndroid ? multiIdsAndroid : multiIdsIOS;

            if (useMultiAdUnitIds && !string.IsNullOrWhiteSpace(key))
            {
                foreach (var entry in multiIds)
                {
                    if (entry != null && entry.AdType == type && entry.IsValid() &&
                        string.Equals(entry.Id?.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.NameId;
                    }
                }
            }
            return "default";
        }

        public List<string> GetAllPlacements()
        {
            var placements = new List<string>();
            if (!useMultiAdUnitIds)
            {
                placements.Add("default");
                return placements;
            }

            bool isAndroid = GetRuntimeAdPlatform() == RuntimeAdPlatform.Android;
            var multiIds = isAndroid ? multiIdsAndroid : multiIdsIOS;

            foreach (var entry in multiIds)
            {
                if (entry != null && entry.IsValid() && !string.IsNullOrWhiteSpace(entry.NameId))
                {
                    string cleanName = entry.NameId.Trim();
                    if (!placements.Contains(cleanName)) placements.Add(cleanName);
                }
            }
            return placements;
        }

        public List<string> GetAllWhere()
        {
            bool isAndroid = GetRuntimeAdPlatform() == RuntimeAdPlatform.Android;
            var multiIds = isAndroid ? multiIdsAndroid : multiIdsIOS;
            if (useMultiAdUnitIds) return multiIds.Select(s => s.NameId).ToList();
            return new List<string> { "default" };
        }

        public string ResolveUnitId(AdUnitType type, string where, EcpmFloor floor = EcpmFloor.All) 
        {
            return GetEntry(type, where, floor).Id;
        }

        private enum RuntimeAdPlatform { Android, IOS }

        private RuntimeAdPlatform GetRuntimeAdPlatform()
        {
#if UNITY_ANDROID
            return RuntimeAdPlatform.Android;
#elif UNITY_IOS || UNITY_IPHONE
            return RuntimeAdPlatform.IOS;
#elif UNITY_EDITOR
            return UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS ? RuntimeAdPlatform.IOS : RuntimeAdPlatform.Android;
#else
            return RuntimeAdPlatform.Android;
#endif
        }
    }
}