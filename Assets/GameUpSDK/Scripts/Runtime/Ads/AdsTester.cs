using System;
using System.Collections.Generic;
using System.Reflection;
using GameUp.Core;
using UnityEngine;

namespace GameUp.SDK
{
    [Serializable]
    public class AdsTestItem
    {
        [Tooltip("Text shown on the test button. Leave empty to auto-generate.")]
        public string buttonLabel;

        [Header("Call by intId (ShowById)")]
        [Tooltip("If true, call AdsManager.ShowById(intId). If false, call by adType + where.")]
        public bool useIntId;
        public int intId;

        [Header("Call by adType + where")]
        public AdUnitType adType = AdUnitType.Interstitial;
        public string where = "main";

        [Tooltip("Used for Interstitial/Rewarded when needed.")]
        public int currentLevel = 1;

        public string GetLabel()
        {
            if (!string.IsNullOrEmpty(buttonLabel))
                return buttonLabel;

            if (useIntId)
                return $"ShowById: {intId}";

            return $"{adType}: {where}";
        }
    }

    /// <summary>
    /// Runtime helper to test ad calls quickly.
    /// - Single mode: show one button from singleItem.
    /// - Multi mode: show one button per item in multiItems.
    /// </summary>
    public class AdsTester : MonoBehaviour
    {
        public enum TestMode
        {
            Single,
            Multi
        }

        [Header("Mode")]
        [SerializeField] private TestMode mode = TestMode.Single;
        [SerializeField] private bool showPanel = true;

        [Header("Single")]
        [SerializeField] private AdsTestItem singleItem = new AdsTestItem();

        [Header("Multi")]
        [SerializeField] private List<AdsTestItem> multiItems = new List<AdsTestItem>();
        [SerializeField] private bool autoBuildMultiItemsOnStart = true;

        [Header("Panel Layout (OnGUI)")]
        [SerializeField] private Vector2 panelPosition = new Vector2(16f, 16f);
        [SerializeField] private float panelWidth = 360f;
        [SerializeField] private float panelHeight = 520f;
        [SerializeField] private bool showUtilityButtons = true;
        [SerializeField] private float uiScale = 2f;

        private Vector2 _scrollPos;
        private GUIStyle _windowStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;
        private static readonly BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<AdsTestItem> _singleItems = new List<AdsTestItem>();
        private string _activeSourceName = "Unknown";

        private void Start()
        {
            if (autoBuildMultiItemsOnStart)
                AutoBuildMultiItemsFromAdsConfig();
        }

        private void OnGUI()
        {
            if (!showPanel)
                return;

            float scale = Mathf.Max(0.5f, uiScale);
            EnsureStyles(scale);

            Rect area = new Rect(
                panelPosition.x,
                panelPosition.y,
                panelWidth * scale,
                panelHeight * scale);
            GUILayout.BeginArea(area, "Ads Tester", _windowStyle);

            GUILayout.Label($"Source: {_activeSourceName}", _labelStyle);
            GUILayout.Label($"Mode: {mode} (from useMultiAdUnitIds)", _labelStyle);

            GUILayout.Space(8f * scale);
            DrawTestButtons(scale);

            if (showUtilityButtons)
            {
                GUILayout.Space(8f * scale);
                DrawUtilityButtons(scale);
            }

            GUILayout.EndArea();
        }

        private void DrawTestButtons(float scale)
        {
            List<AdsTestItem> items = GetActiveItems();
            if (items.Count == 0)
            {
                GUILayout.Label("No test item configured.", _labelStyle);
                return;
            }

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));
            for (int i = 0; i < items.Count; i++)
            {
                AdsTestItem item = items[i];
                if (item == null)
                    continue;

                if (GUILayout.Button(item.GetLabel(), _buttonStyle, GUILayout.Height(34f * scale)))
                    ExecuteItem(item);
            }
            GUILayout.EndScrollView();
        }

        private void DrawUtilityButtons(float scale)
        {
            if (GUILayout.Button("RequestAll", _buttonStyle, GUILayout.Height(34f * scale)))
            {
                AdsManager.Instance?.RequestAll();
                GULogger.Log("GameUp", "AdsTester -> RequestAll");
            }

            if (GUILayout.Button("Reset Interstitial Capping", _buttonStyle, GUILayout.Height(34f * scale)))
            {
                AdsManager.Instance?.ResetInterstitialCappingForTest();
                GULogger.Log("GameUp", "AdsTester -> ResetInterstitialCappingForTest");
            }

            if (GUILayout.Button("Auto Build Items From Ads Config", _buttonStyle, GUILayout.Height(34f * scale)))
                AutoBuildMultiItemsFromAdsConfig();
        }

        private void EnsureStyles(float scale)
        {
            int labelFontSize = Mathf.RoundToInt(14f * scale);
            int buttonFontSize = Mathf.RoundToInt(13f * scale);
            int windowFontSize = Mathf.RoundToInt(14f * scale);
            int padding = Mathf.RoundToInt(10f * scale);

            if (_windowStyle == null)
            {
                _windowStyle = new GUIStyle(GUI.skin.window);
                _buttonStyle = new GUIStyle(GUI.skin.button);
                _labelStyle = new GUIStyle(GUI.skin.label);
            }

            _windowStyle.fontSize = windowFontSize;
            _windowStyle.padding = new RectOffset(padding, padding, padding, padding);
            _buttonStyle.fontSize = buttonFontSize;
            _labelStyle.fontSize = labelFontSize;
        }

        private List<AdsTestItem> GetActiveItems()
        {
            if (mode == TestMode.Single)
            {
                if (_singleItems.Count > 0)
                    return _singleItems;
                if (_singleRuntimeCache == null)
                    _singleRuntimeCache = new List<AdsTestItem>(1);
                _singleRuntimeCache.Clear();
                if (singleItem != null)
                    _singleRuntimeCache.Add(singleItem);
                return _singleRuntimeCache;
            }

            return multiItems ?? EmptyItems;
        }

        private void ExecuteItem(AdsTestItem item)
        {
            if (AdsManager.Instance == null)
            {
                GULogger.Warning("GameUp", "AdsTester -> AdsManager.Instance is null.");
                return;
            }

            if (item.useIntId)
            {
                AdsManager.Instance.ShowById(item.intId, Mathf.Max(0, item.currentLevel),
                    () => GULogger.Log("GameUp", $"AdsTester success -> ShowById {item.intId}"),
                    () => GULogger.Warning("GameUp", $"AdsTester fail -> ShowById {item.intId}"));
                return;
            }

            string where = item.where ?? string.Empty;
            switch (item.adType)
            {
                case AdUnitType.Banner:
                    AdsManager.Instance.ShowBanner(where);
                    GULogger.Log("GameUp", $"AdsTester show -> Banner {where}");
                    break;
                case AdUnitType.Interstitial:
                    AdsManager.Instance.ShowInterstitial(where, Mathf.Max(0, item.currentLevel),
                        () => GULogger.Log("GameUp", $"AdsTester success -> Interstitial {where}"),
                        () => GULogger.Warning("GameUp", $"AdsTester fail -> Interstitial {where}"));
                    break;
                case AdUnitType.RewardedVideo:
                    AdsManager.Instance.ShowRewardedVideo(where, Mathf.Max(0, item.currentLevel),
                        () => GULogger.Log("GameUp", $"AdsTester success -> Rewarded {where}"),
                        () => GULogger.Warning("GameUp", $"AdsTester fail -> Rewarded {where}"));
                    break;
                case AdUnitType.AppOpen:
                    AdsManager.Instance.ShowAppOpenAds(where,
                        () => GULogger.Log("GameUp", $"AdsTester success -> AppOpen {where}"),
                        () => GULogger.Warning("GameUp", $"AdsTester fail -> AppOpen {where}"));
                    break;
                default:
                    GULogger.Warning("GameUp", $"AdsTester unsupported ad type: {item.adType}");
                    break;
            }
        }

        private static readonly List<AdsTestItem> EmptyItems = new List<AdsTestItem>(0);
        private List<AdsTestItem> _singleRuntimeCache;

        [ContextMenu("Auto Build Multi Items From Ads Config")]
        public void AutoBuildMultiItemsFromAdsConfig()
        {
            var manager = AdsManager.Instance;
            if (manager == null)
            {
                GULogger.Warning("GameUp", "AdsTester -> AdsManager.Instance is null. Cannot auto build items.");
                return;
            }

            if (multiItems == null)
                multiItems = new List<AdsTestItem>();
            else
                multiItems.Clear();
            _singleItems.Clear();

            var uniqueKeys = new HashSet<string>(StringComparer.Ordinal);
            var admob = FindPrimaryAdmob(manager);
            if (admob != null)
            {
                BuildFromAdmob(admob, uniqueKeys);
            }
            else
            {
                var ironSource = FindPrimaryIronSource(manager);
                if (ironSource != null)
                    BuildFromIronSource(ironSource, uniqueKeys);
                else
                    _activeSourceName = "No Ads Source";
            }

            if (multiItems.Count > 0)
            {
                if (singleItem == null)
                    singleItem = new AdsTestItem();

                CopyItem(multiItems[0], singleItem);
            }

            int count = mode == TestMode.Single ? _singleItems.Count : multiItems.Count;
            GULogger.Log("GameUp", $"AdsTester -> Source={_activeSourceName}, Mode={mode}, built {count} test item(s).");
        }

        private AdmobAds FindPrimaryAdmob(AdsManager manager)
        {
            var sources = manager.GetComponentsInChildren<AdmobAds>(true);
            if (sources == null || sources.Length == 0)
                return null;
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null)
                    return sources[i];
            }
            return null;
        }

        private IronSourceAds FindPrimaryIronSource(AdsManager manager)
        {
            var sources = manager.GetComponentsInChildren<IronSourceAds>(true);
            if (sources == null || sources.Length == 0)
                return null;
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null)
                    return sources[i];
            }
            return null;
        }

        private void BuildFromAdmob(AdmobAds src, HashSet<string> uniqueKeys)
        {
            _activeSourceName = "AdMob";
            bool useMulti = ReadPrivateField<bool>(src, "useMultiAdUnitIds");
            mode = useMulti ? TestMode.Multi : TestMode.Single;
            if (useMulti)
            {
                var entries = ReadPrivateField<List<AdUnitIdEntry>>(src, "adUnitIds");
                AddMultiItems(entries, "AdMob", uniqueKeys);
                return;
            }

            AddSingleItemIfValid(AdUnitType.Banner, "main", ReadPrivateField<string>(src, "bannerAdUnitId"), "AdMob");
            AddSingleItemIfValid(AdUnitType.Interstitial, "main", ReadPrivateField<string>(src, "interstitialAdUnitId"), "AdMob");
            AddSingleItemIfValid(AdUnitType.RewardedVideo, "main", ReadPrivateField<string>(src, "rewardedAdUnitId"), "AdMob");
            AddSingleItemIfValid(AdUnitType.AppOpen, "main", ReadPrivateField<string>(src, "appOpenAdUnitId"), "AdMob");
        }

        private void BuildFromIronSource(IronSourceAds src, HashSet<string> uniqueKeys)
        {
            _activeSourceName = "LevelPlay";
            bool useMulti = ReadPrivateField<bool>(src, "useMultiAdUnitIds");
            mode = useMulti ? TestMode.Multi : TestMode.Single;
            if (useMulti)
            {
                var entries = ReadPrivateField<List<AdUnitIdEntry>>(src, "adUnitIds");
                AddMultiItems(entries, "LevelPlay", uniqueKeys);
                return;
            }

            AddSingleItemIfValid(AdUnitType.Banner, "main", ReadPrivateField<string>(src, "bannerAdUnitId"), "LevelPlay");
            AddSingleItemIfValid(AdUnitType.Interstitial, "main", ReadPrivateField<string>(src, "interstitialAdUnitId"), "LevelPlay");
            AddSingleItemIfValid(AdUnitType.RewardedVideo, "main", ReadPrivateField<string>(src, "rewardedVideoAdUnitId"), "LevelPlay");
        }

        private void AddMultiItems(List<AdUnitIdEntry> entries, string source, HashSet<string> uniqueKeys)
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || !e.IsValid())
                    continue;
                AddItemIfMissing(e.AdType, e.NameId, source, uniqueKeys);
            }
        }

        private void AddItemIfMissing(AdUnitType adType, string where, string source, HashSet<string> uniqueKeys)
        {
            if (string.IsNullOrEmpty(where))
                return;

            string unique = $"{adType}|{where}";
            if (!uniqueKeys.Add(unique))
                return;

            multiItems.Add(new AdsTestItem
            {
                useIntId = false,
                adType = adType,
                where = where,
                currentLevel = 1,
                buttonLabel = $"{source} - {adType}: {where}"
            });
        }

        private void AddSingleItemIfValid(AdUnitType adType, string where, string adUnitId, string source)
        {
            if (string.IsNullOrEmpty(adUnitId))
                return;

            _singleItems.Add(new AdsTestItem
            {
                useIntId = false,
                adType = adType,
                where = where,
                currentLevel = 1,
                buttonLabel = $"{source} - {adType} (Single)"
            });
        }

        private static T ReadPrivateField<T>(object target, string fieldName)
        {
            if (target == null || string.IsNullOrEmpty(fieldName))
                return default;

            var field = target.GetType().GetField(fieldName, PrivateInstanceFlags);
            if (field == null)
                return default;

            object value = field.GetValue(target);
            if (value is T casted)
                return casted;

            return default;
        }

        private static void CopyItem(AdsTestItem from, AdsTestItem to)
        {
            if (from == null || to == null)
                return;

            to.buttonLabel = from.buttonLabel;
            to.useIntId = from.useIntId;
            to.intId = from.intId;
            to.adType = from.adType;
            to.where = from.where;
            to.currentLevel = from.currentLevel;
        }
    }
}
