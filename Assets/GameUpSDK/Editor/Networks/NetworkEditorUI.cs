using GameUp.SDK;
using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    public enum AdMobIdEditorPlatform
    {
        Android,
        IOS
    }

    /// <summary>
    /// Vẽ UI cấu hình quảng cáo trực tiếp trên SerializedProperty của GameUpAdsConfig.
    /// Không có lớp dữ liệu trung gian ⇒ không cần Load/Save thủ công, có sẵn Undo và
    /// đánh dấu dirty, và mọi field đều được kiểm tra ở thời điểm biên dịch.
    /// </summary>
    public static class NetworkEditorUI
    {
        public static AdMobIdEditorPlatform DefaultPlatform =>
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS
                ? AdMobIdEditorPlatform.IOS
                : AdMobIdEditorPlatform.Android;

        public static AdMobIdEditorPlatform DrawPlatformSelector(AdMobIdEditorPlatform platform)
        {
            return (AdMobIdEditorPlatform)EditorGUILayout.EnumPopup(
                new GUIContent("Platform đang sửa", "Chỉ đổi bộ ID đang hiển thị, không ảnh hưởng build target."),
                platform);
        }

        // =====================================================================
        // SECTIONS
        // =====================================================================

        public static void DrawAdmobSection(SerializedProperty admob, AdMobIdEditorPlatform platform)
        {
            if (admob == null) return;

            EditorGUILayout.LabelField("Google Mobile Ads App ID", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(admob.FindPropertyRelative("appIdAndroid"), new GUIContent("Android App ID"));
            EditorGUILayout.PropertyField(admob.FindPropertyRelative("appIdIOS"), new GUIContent("iOS App ID"));
            EditorGUILayout.HelpBox("App ID được ghi sang GoogleMobileAdsSettings.asset khi bấm Save Configuration.", MessageType.None);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            DrawStringList(admob.FindPropertyRelative("testDevices"), "Test Devices", "+ Add Device");
            EditorGUILayout.PropertyField(admob.FindPropertyRelative("showMediationInspector"), new GUIContent("Show Ad Inspector (debug)"));

            EditorGUILayout.Space();
            DrawUnitSet(admob.FindPropertyRelative("units"), platform, includeNative: true);
        }

        public static void DrawMaxSection(SerializedProperty max, AdMobIdEditorPlatform platform)
        {
            if (max == null) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(max.FindPropertyRelative("sdkKey"), new GUIContent("SDK Key"));
            EditorGUILayout.PropertyField(max.FindPropertyRelative("showMediationDebugger"), new GUIContent("Show Mediation Debugger"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            DrawUnitSet(max.FindPropertyRelative("units"), platform, includeNative: false);
        }

        public static void DrawIronSourceSection(SerializedProperty ironSource, AdMobIdEditorPlatform platform)
        {
            if (ironSource == null) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(ironSource.FindPropertyRelative("appKey"), new GUIContent("LevelPlay App Key"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            var units = ironSource.FindPropertyRelative("units");
            DrawAdUnitConfig(units.FindPropertyRelative("banner"), "Banner Configuration", AdUnitType.Banner, platform);
            DrawAdUnitConfig(units.FindPropertyRelative("interstitial"), "Interstitial Configuration", AdUnitType.Interstitial, platform);
            DrawAdUnitConfig(units.FindPropertyRelative("rewarded"), "Rewarded Configuration", AdUnitType.RewardedVideo, platform);
        }

        private static void DrawUnitSet(SerializedProperty units, AdMobIdEditorPlatform platform, bool includeNative)
        {
            if (units == null) return;

            DrawAdUnitConfig(units.FindPropertyRelative("banner"), "Banner Configuration", AdUnitType.Banner, platform);
            DrawAdUnitConfig(units.FindPropertyRelative("interstitial"), "Interstitial Configuration", AdUnitType.Interstitial, platform);
            DrawAdUnitConfig(units.FindPropertyRelative("rewarded"), "Rewarded Configuration", AdUnitType.RewardedVideo, platform);
            DrawAdUnitConfig(units.FindPropertyRelative("appOpen"), "App Open Configuration", AdUnitType.AppOpen, platform);
            if (includeNative)
                DrawAdUnitConfig(units.FindPropertyRelative("nativeAd"), "Native Ad Configuration", AdUnitType.NativeAd, platform);
        }

        // =====================================================================
        // MỘT AD UNIT CONFIG
        // =====================================================================

        public static void DrawAdUnitConfig(SerializedProperty config, string label, AdUnitType adType, AdMobIdEditorPlatform platform)
        {
            if (config == null) return;

            bool isAndroid = platform == AdMobIdEditorPlatform.Android;
            var waterfall = config.FindPropertyRelative("enableWaterfallFloor");
            var useMulti = config.FindPropertyRelative("useMultiAdUnitIds");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            if (adType == AdUnitType.Banner)
            {
                waterfall.boolValue = false; // Banner không chạy waterfall floor
            }
            else
            {
                EditorGUILayout.PropertyField(waterfall,
                    new GUIContent("Enable Waterfall Floor", "Bật để dùng 3 ID (High → Medium → All). Tắt để dùng 1 ID tiêu chuẩn."));
            }

            EditorGUILayout.PropertyField(useMulti,
                new GUIContent("Use Multi IDs", "Bật để mỗi placement (where) có ID riêng."));
            EditorGUILayout.Space(4);

            if (useMulti.boolValue)
            {
                var placements = config.FindPropertyRelative(isAndroid ? "placementsAndroid" : "placementsIOS");
                DrawPlacementList(placements, adType, waterfall.boolValue);
            }
            else
            {
                EditorGUILayout.LabelField("Default IDs", EditorStyles.boldLabel);
                string suffix = isAndroid ? "Android" : "IOS";
                if (waterfall.boolValue)
                {
                    EditorGUILayout.PropertyField(config.FindPropertyRelative($"defaultId{suffix}_High"), new GUIContent("High Floor ID"));
                    EditorGUILayout.PropertyField(config.FindPropertyRelative($"defaultId{suffix}_Medium"), new GUIContent("Medium Floor ID"));
                    EditorGUILayout.PropertyField(config.FindPropertyRelative($"defaultId{suffix}_All"), new GUIContent("All (Base) ID"));
                }
                else
                {
                    EditorGUILayout.PropertyField(config.FindPropertyRelative($"defaultId{suffix}_All"), new GUIContent("Ad Unit ID"));
                }

                if (adType == AdUnitType.Banner)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.PropertyField(config.FindPropertyRelative("defaultBannerFormat"), new GUIContent("Banner Format"));
                    EditorGUILayout.PropertyField(config.FindPropertyRelative("defaultBannerSize"), new GUIContent("Banner Size"));
                    EditorGUILayout.PropertyField(config.FindPropertyRelative("defaultCollapsible"), new GUIContent("Collapsible"));
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private static void DrawPlacementList(SerializedProperty placements, AdUnitType adType, bool isWaterfallEnabled)
        {
            if (placements == null) return;

            int removeAt = -1;
            for (int i = 0; i < placements.arraySize; i++)
            {
                var element = placements.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Where (Placement):", GUILayout.Width(115));
                EditorGUILayout.PropertyField(element.FindPropertyRelative("where"), GUIContent.none);
                if (GUILayout.Button("X", GUILayout.Width(24))) removeAt = i;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);
                EditorGUI.indentLevel++;
                if (isWaterfallEnabled)
                {
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("idHigh"), new GUIContent("High Floor ID"));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("idMedium"), new GUIContent("Medium Floor ID"));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("idAll"), new GUIContent("All (Base) ID"));
                }
                else
                {
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("idAll"), new GUIContent("Ad Unit ID"));
                }
                EditorGUI.indentLevel--;

                if (adType == AdUnitType.Banner)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(15);
                    GUILayout.Label("↳ Format:", EditorStyles.miniLabel, GUILayout.Width(50));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("bannerFormat"), GUIContent.none, GUILayout.Width(95));
                    GUILayout.Label("Size:", EditorStyles.miniLabel, GUILayout.Width(30));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("bannerSize"), GUIContent.none, GUILayout.Width(85));
                    GUILayout.Label("Collapsible:", EditorStyles.miniLabel, GUILayout.Width(65));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("collapsible"), GUIContent.none, GUILayout.Width(70));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            if (removeAt >= 0) placements.DeleteArrayElementAtIndex(removeAt);

            if (GUILayout.Button($"+ Add {adType} Placement"))
            {
                placements.arraySize++;
                var added = placements.GetArrayElementAtIndex(placements.arraySize - 1);
                added.FindPropertyRelative("where").stringValue = "new_placement";
                added.FindPropertyRelative("idHigh").stringValue = "";
                added.FindPropertyRelative("idMedium").stringValue = "";
                added.FindPropertyRelative("idAll").stringValue = "";
                added.FindPropertyRelative("bannerFormat").enumValueIndex = (int)BannerFormatType.StandardBanner;
                added.FindPropertyRelative("bannerSize").enumValueIndex = (int)BannerSize.Adaptive;
                added.FindPropertyRelative("collapsible").enumValueIndex = (int)CollapsibleBannerPlacement.None;
            }
        }

        // =====================================================================
        // TIỆN ÍCH
        // =====================================================================

        public static void DrawStringList(SerializedProperty listProp, string label, string addLabel)
        {
            if (listProp == null) return;

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            int removeAt = -1;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(listProp.GetArrayElementAtIndex(i), GUIContent.none);
                if (GUILayout.Button("-", GUILayout.Width(24))) removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0) listProp.DeleteArrayElementAtIndex(removeAt);

            if (GUILayout.Button(addLabel, GUILayout.Width(120)))
            {
                listProp.arraySize++;
                listProp.GetArrayElementAtIndex(listProp.arraySize - 1).stringValue = "";
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }
    }
}
