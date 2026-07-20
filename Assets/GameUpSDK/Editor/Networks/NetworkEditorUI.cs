using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GameUp.SDK;

namespace GameUp.SDK.Editor.Setup
{
    public static class NetworkEditorUI
    {
        public static void DrawConfigDataUI(string label, AdUnitConfigData configData, AdMobIdEditorPlatform platform, AdUnitType defaultAdType)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            
            if (defaultAdType != AdUnitType.Banner)
            {
                configData.enableWaterfallFloor = EditorGUILayout.Toggle(new GUIContent("Enable Waterfall Floor", "Bật để sử dụng 3 ID (High -> Medium -> All). Tắt để dùng 1 ID tiêu chuẩn."), configData.enableWaterfallFloor);
            }
            else
            {
                configData.enableWaterfallFloor = false; // Banner luôn tắt
            }

            configData.useMultiIds = EditorGUILayout.Toggle("Use Multi IDs", configData.useMultiIds);
            EditorGUILayout.Space(4);

            if (configData.useMultiIds)
            {
                var list = platform == AdMobIdEditorPlatform.Android ? configData.multiIdsAndroidGrouped : configData.multiIdsIOSGrouped;
                DrawAdUnitIdGroupedUI(ref list, defaultAdType, configData.enableWaterfallFloor);
                
                if (platform == AdMobIdEditorPlatform.Android) configData.multiIdsAndroidGrouped = list;
                else configData.multiIdsIOSGrouped = list;
            }
            else
            {
                EditorGUILayout.LabelField("Default IDs", EditorStyles.boldLabel);
                if (platform == AdMobIdEditorPlatform.Android)
                {
                    if (configData.enableWaterfallFloor)
                    {
                        configData.defaultIdAndroid_High = EditorGUILayout.TextField("High Floor ID", configData.defaultIdAndroid_High);
                        configData.defaultIdAndroid_Medium = EditorGUILayout.TextField("Medium Floor ID", configData.defaultIdAndroid_Medium);
                        configData.defaultIdAndroid_All = EditorGUILayout.TextField("All (Base) ID", configData.defaultIdAndroid_All);
                    }
                    else
                    {
                        configData.defaultIdAndroid_All = EditorGUILayout.TextField("Ad Unit ID", configData.defaultIdAndroid_All);
                    }
                }
                else
                {
                    if (configData.enableWaterfallFloor)
                    {
                        configData.defaultIdIOS_High = EditorGUILayout.TextField("High Floor ID", configData.defaultIdIOS_High);
                        configData.defaultIdIOS_Medium = EditorGUILayout.TextField("Medium Floor ID", configData.defaultIdIOS_Medium);
                        configData.defaultIdIOS_All = EditorGUILayout.TextField("All (Base) ID", configData.defaultIdIOS_All);
                    }
                    else
                    {
                        configData.defaultIdIOS_All = EditorGUILayout.TextField("Ad Unit ID", configData.defaultIdIOS_All);
                    }
                }

                if (defaultAdType == AdUnitType.Banner)
                {
                    EditorGUILayout.Space();
                    configData.defaultBannerFormat = (BannerFormatType)EditorGUILayout.EnumPopup("Banner Format", configData.defaultBannerFormat);
                    configData.defaultBannerSize = (BannerSize)EditorGUILayout.EnumPopup("Banner Size", configData.defaultBannerSize);
                    configData.defaultCollapsible = (CollapsibleBannerPlacement)EditorGUILayout.EnumPopup("Collapsible", configData.defaultCollapsible);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private static void DrawAdUnitIdGroupedUI(ref List<PlacementGroup> groups, AdUnitType defaultAdType, bool isWaterfallEnabled)
        {
            if (groups == null) groups = new List<PlacementGroup>();

            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i] ?? (groups[i] = new PlacementGroup { AdType = defaultAdType });
                g.AdType = defaultAdType;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Where (Placement):", GUILayout.Width(115));
                g.NameId = EditorGUILayout.TextField(g.NameId ?? "");
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    groups.RemoveAt(i); i--;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);
                
                EditorGUI.indentLevel++;
                if (isWaterfallEnabled)
                {
                    g.IdHigh = EditorGUILayout.TextField("High Floor ID", g.IdHigh);
                    g.IdMedium = EditorGUILayout.TextField("Medium Floor ID", g.IdMedium);
                    g.IdAll = EditorGUILayout.TextField("All (Base) ID", g.IdAll);
                }
                else
                {
                    g.IdAll = EditorGUILayout.TextField("Ad Unit ID", g.IdAll);
                }
                EditorGUI.indentLevel--;

                if (g.AdType == AdUnitType.Banner)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(15);
                    GUILayout.Label("↳ Format:", EditorStyles.miniLabel, GUILayout.Width(50));
                    g.BannerFormat = (BannerFormatType)EditorGUILayout.EnumPopup(g.BannerFormat, GUILayout.Width(95));
                    GUILayout.Label("Size:", EditorStyles.miniLabel, GUILayout.Width(30));
                    g.BannerSize = (BannerSize)EditorGUILayout.EnumPopup(g.BannerSize, GUILayout.Width(85));
                    GUILayout.Label("Collapsible:", EditorStyles.miniLabel, GUILayout.Width(65));
                    g.CollapsiblePlacement = (CollapsibleBannerPlacement)EditorGUILayout.EnumPopup(g.CollapsiblePlacement, GUILayout.Width(70));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            if (GUILayout.Button($"+ Add {defaultAdType} Placement"))
            {
                groups.Add(new PlacementGroup { AdType = defaultAdType, NameId = "new_placement" });
            }
        }
    }
}