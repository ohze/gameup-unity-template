using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GameUp.SDK;

namespace GameUp.SDK.Editor.Setup
{
    public enum AdMobIdEditorPlatform
    {
        Android,
        IOS
    }

    public class PlacementGroup
    {
        public string NameId;
        public AdUnitType AdType;
        public string IdHigh;
        public string IdMedium;
        public string IdAll;
        public BannerFormatType BannerFormat = BannerFormatType.StandardBanner;
        public BannerSize BannerSize = BannerSize.Adaptive;
        public CollapsibleBannerPlacement CollapsiblePlacement = CollapsibleBannerPlacement.None;
    }

    public class AdUnitConfigData
    {
        public bool enableWaterfallFloor; 
        public bool useMultiIds;
        public string defaultIdAndroid_High;
        public string defaultIdAndroid_Medium;
        public string defaultIdAndroid_All;
        public string defaultIdIOS_High;
        public string defaultIdIOS_Medium;
        public string defaultIdIOS_All;

        public BannerSize defaultBannerSize;
        public BannerFormatType defaultBannerFormat;
        public CollapsibleBannerPlacement defaultCollapsible;

        public List<PlacementGroup> multiIdsAndroidGrouped = new List<PlacementGroup>();
        public List<PlacementGroup> multiIdsIOSGrouped = new List<PlacementGroup>();

        public void Load(SerializedProperty configProp)
        {
            if (configProp == null) return;
            enableWaterfallFloor = configProp.FindPropertyRelative("enableWaterfallFloor")?.boolValue ?? false;
            useMultiIds = configProp.FindPropertyRelative("useMultiAdUnitIds")?.boolValue ?? false;
            
            defaultIdAndroid_High = configProp.FindPropertyRelative("defaultIdAndroid_High")?.stringValue ?? "";
            defaultIdAndroid_Medium = configProp.FindPropertyRelative("defaultIdAndroid_Medium")?.stringValue ?? "";
            defaultIdAndroid_All = configProp.FindPropertyRelative("defaultIdAndroid_All")?.stringValue ?? "";
            
            defaultIdIOS_High = configProp.FindPropertyRelative("defaultIdIOS_High")?.stringValue ?? "";
            defaultIdIOS_Medium = configProp.FindPropertyRelative("defaultIdIOS_Medium")?.stringValue ?? "";
            defaultIdIOS_All = configProp.FindPropertyRelative("defaultIdIOS_All")?.stringValue ?? "";
            
            var colProp = configProp.FindPropertyRelative("defaultCollapsible");
            if (colProp != null) defaultCollapsible = (CollapsibleBannerPlacement)colProp.intValue;
            
            var bsProp = configProp.FindPropertyRelative("defaultBannerSize");
            if (bsProp != null) defaultBannerSize = (BannerSize)bsProp.intValue;
            
            var colBannerFormat = configProp.FindPropertyRelative("defaultBannerFormat");
            if (colBannerFormat != null) defaultBannerFormat = (BannerFormatType)colBannerFormat.intValue;

            List<AdUnitIdEntry> rawAndroid = new List<AdUnitIdEntry>();
            SetupTabBase.AssignAdUnitIdListDirect(configProp.FindPropertyRelative("multiIdsAndroid"), rawAndroid);
            multiIdsAndroidGrouped = GroupEntries(rawAndroid);

            List<AdUnitIdEntry> rawIOS = new List<AdUnitIdEntry>();
            SetupTabBase.AssignAdUnitIdListDirect(configProp.FindPropertyRelative("multiIdsIOS"), rawIOS);
            multiIdsIOSGrouped = GroupEntries(rawIOS);
        }

        public void Save(SerializedProperty configProp)
        {
            if (configProp == null) return;
            if (configProp.FindPropertyRelative("enableWaterfallFloor") != null) configProp.FindPropertyRelative("enableWaterfallFloor").boolValue = enableWaterfallFloor;
            if (configProp.FindPropertyRelative("useMultiAdUnitIds") != null) configProp.FindPropertyRelative("useMultiAdUnitIds").boolValue = useMultiIds;
            
            if (configProp.FindPropertyRelative("defaultIdAndroid_High") != null) configProp.FindPropertyRelative("defaultIdAndroid_High").stringValue = defaultIdAndroid_High;
            if (configProp.FindPropertyRelative("defaultIdAndroid_Medium") != null) configProp.FindPropertyRelative("defaultIdAndroid_Medium").stringValue = defaultIdAndroid_Medium;
            if (configProp.FindPropertyRelative("defaultIdAndroid_All") != null) configProp.FindPropertyRelative("defaultIdAndroid_All").stringValue = defaultIdAndroid_All;
            
            if (configProp.FindPropertyRelative("defaultIdIOS_High") != null) configProp.FindPropertyRelative("defaultIdIOS_High").stringValue = defaultIdIOS_High;
            if (configProp.FindPropertyRelative("defaultIdIOS_Medium") != null) configProp.FindPropertyRelative("defaultIdIOS_Medium").stringValue = defaultIdIOS_Medium;
            if (configProp.FindPropertyRelative("defaultIdIOS_All") != null) configProp.FindPropertyRelative("defaultIdIOS_All").stringValue = defaultIdIOS_All;

            var colBannerFormat = configProp.FindPropertyRelative("defaultBannerFormat");
            if (colBannerFormat != null) colBannerFormat.intValue = (int)defaultBannerFormat;
            
            var bsProp = configProp.FindPropertyRelative("defaultBannerSize");
            if (bsProp != null) bsProp.intValue = (int)defaultBannerSize;

            var colProp = configProp.FindPropertyRelative("defaultCollapsible");
            if (colProp != null) colProp.intValue = (int)defaultCollapsible;

            var rawAndroid = FlattenEntries(multiIdsAndroidGrouped);
            SetupTabBase.SetAdUnitIdListDirect(configProp.FindPropertyRelative("multiIdsAndroid"), rawAndroid);

            var rawIOS = FlattenEntries(multiIdsIOSGrouped);
            SetupTabBase.SetAdUnitIdListDirect(configProp.FindPropertyRelative("multiIdsIOS"), rawIOS);
        }

        private List<PlacementGroup> GroupEntries(List<AdUnitIdEntry> raw)
        {
            var dict = new Dictionary<string, PlacementGroup>();
            foreach (var e in raw)
            {
                string key = e.NameId ?? "";
                if (!dict.ContainsKey(key))
                {
                    dict[key] = new PlacementGroup { NameId = key, AdType = e.AdType, BannerFormat = e.BannerFormat, BannerSize = e.BannerSize, CollapsiblePlacement = e.CollapsiblePlacement };
                }
                if (e.Floor == EcpmFloor.High) dict[key].IdHigh = e.Id;
                else if (e.Floor == EcpmFloor.Medium) dict[key].IdMedium = e.Id;
                else dict[key].IdAll = e.Id;
            }
            return new List<PlacementGroup>(dict.Values);
        }

        private List<AdUnitIdEntry> FlattenEntries(List<PlacementGroup> groups)
        {
            var list = new List<AdUnitIdEntry>();
            int index = 1;
            foreach (var g in groups)
            {
                bool hasAny = false;
                if (!string.IsNullOrEmpty(g.IdHigh)) { list.Add(CreateEntry(g, EcpmFloor.High, g.IdHigh, index++)); hasAny = true; }
                if (!string.IsNullOrEmpty(g.IdMedium)) { list.Add(CreateEntry(g, EcpmFloor.Medium, g.IdMedium, index++)); hasAny = true; }
                if (!string.IsNullOrEmpty(g.IdAll)) { list.Add(CreateEntry(g, EcpmFloor.All, g.IdAll, index++)); hasAny = true; }
                
                if (!hasAny) list.Add(CreateEntry(g, EcpmFloor.All, "", index++));
            }
            return list;
        }

        private AdUnitIdEntry CreateEntry(PlacementGroup g, EcpmFloor floor, string id, int intId)
        {
            return new AdUnitIdEntry { AdType = g.AdType, NameId = g.NameId, Id = id, intId = intId, Floor = floor, BannerFormat = g.BannerFormat, BannerSize = g.BannerSize, CollapsiblePlacement = g.CollapsiblePlacement };
        }
    }

    public abstract class SetupTabBase
    {
        public abstract string Title { get; }
        public virtual bool IsVisible => true;

        public abstract void Load();
        public abstract void Draw();
        public abstract void Save();

        protected void Assign(SerializedObject so, string prop, ref string target) { var p = so.FindProperty(prop); if (p != null) target = p.stringValue ?? ""; }
        protected void AssignInt(SerializedObject so, string prop, ref int target) { var p = so.FindProperty(prop); if (p != null) target = p.intValue; }
        protected void AssignBool(SerializedObject so, string prop, ref bool target) { var p = so.FindProperty(prop); if (p != null) target = p.boolValue; }
        protected void AssignFloat(SerializedObject so, string prop, ref float target) { var p = so.FindProperty(prop); if (p != null) target = p.floatValue; }

        protected void Set(SerializedObject so, string propName, string value) { var p = so.FindProperty(propName); if (p != null) p.stringValue = value ?? ""; }
        protected void SetInt(SerializedObject so, string propName, int value) { var p = so.FindProperty(propName); if (p != null) p.intValue = value; }
        protected void SetBool(SerializedObject so, string propName, bool value) { var p = so.FindProperty(propName); if (p != null) p.boolValue = value; }
        protected void SetFloat(SerializedObject so, string propName, float value) { var p = so.FindProperty(propName); if (p != null) p.floatValue = value; }

        public static void AssignStringList(SerializedProperty listProp, List<string> target)
        {
            if (target == null || listProp == null || !listProp.isArray) return;
            target.Clear();
            for (int i = 0; i < listProp.arraySize; i++) target.Add(listProp.GetArrayElementAtIndex(i).stringValue);
        }

        public static void SetStringList(SerializedProperty listProp, List<string> source)
        {
            if (listProp == null || !listProp.isArray) return;
            source ??= new List<string>();
            listProp.arraySize = source.Count;
            for (int i = 0; i < source.Count; i++) listProp.GetArrayElementAtIndex(i).stringValue = source[i];
        }

        public static void AssignAdUnitIdListDirect(SerializedProperty listProp, List<AdUnitIdEntry> target)
        {
            if (target == null || listProp == null || !listProp.isArray) return;
            target.Clear();
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var el = listProp.GetArrayElementAtIndex(i);
                var floorProp = el.FindPropertyRelative("Floor");
                target.Add(new AdUnitIdEntry
                {
                    AdType = (AdUnitType)(el.FindPropertyRelative("AdType")?.intValue ?? 0),
                    NameId = el.FindPropertyRelative("NameId")?.stringValue ?? "",
                    Id = el.FindPropertyRelative("Id")?.stringValue ?? "",
                    intId = el.FindPropertyRelative("intId")?.intValue ?? 0,
                    Floor = floorProp != null ? (EcpmFloor)floorProp.intValue : EcpmFloor.All,

                    BannerSize = (BannerSize)(el.FindPropertyRelative("BannerSize")?.intValue ?? 0),
                    BannerFormat = (BannerFormatType)(el.FindPropertyRelative("BannerFormat")?.intValue ?? 0),
                    CollapsiblePlacement = (CollapsibleBannerPlacement)(el.FindPropertyRelative("CollapsiblePlacement")?.intValue ?? 0)
                });
            }
        }

        public static void SetAdUnitIdListDirect(SerializedProperty listProp, List<AdUnitIdEntry> source)
        {
            if (listProp == null || !listProp.isArray) return;
            source ??= new List<AdUnitIdEntry>();
            listProp.arraySize = source.Count;
            for (int i = 0; i < source.Count; i++)
            {
                var el = listProp.GetArrayElementAtIndex(i);
                var e = source[i];

                if (el.FindPropertyRelative("AdType") != null) el.FindPropertyRelative("AdType").intValue = (int)e.AdType;
                if (el.FindPropertyRelative("NameId") != null) el.FindPropertyRelative("NameId").stringValue = e.NameId ?? "";
                if (el.FindPropertyRelative("Id") != null) el.FindPropertyRelative("Id").stringValue = e.Id ?? "";
                if (el.FindPropertyRelative("intId") != null) el.FindPropertyRelative("intId").intValue = e.intId;
                if (el.FindPropertyRelative("Floor") != null) el.FindPropertyRelative("Floor").intValue = (int)e.Floor;

                if (el.FindPropertyRelative("BannerFormat") != null) el.FindPropertyRelative("BannerFormat").intValue = (int)e.BannerFormat;
                if (el.FindPropertyRelative("BannerSize") != null) el.FindPropertyRelative("BannerSize").intValue = (int)e.BannerSize;
                if (el.FindPropertyRelative("CollapsiblePlacement") != null) el.FindPropertyRelative("CollapsiblePlacement").intValue = (int)e.CollapsiblePlacement;
            }
        }

        protected void DrawStringListUI(string label, List<string> list)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                list[i] = EditorGUILayout.TextField(list[i]);
                if (GUILayout.Button("-", GUILayout.Width(24))) { list.RemoveAt(i); i--; }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Device", GUILayout.Width(120))) list.Add("");
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
        }

        protected void DrawConfigDataUI(string label, AdUnitConfigData configData, AdMobIdEditorPlatform platform, AdUnitType defaultAdType)
        {
            // Ủy quyền gọi UI cho lớp NetworkEditorUI
            NetworkEditorUI.DrawConfigDataUI(label, configData, platform, defaultAdType);
        }

        protected void ModifyPrefab(string path, Action<GameObject> modifyAction)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                modifyAction(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                EditorUtility.SetDirty(root);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}