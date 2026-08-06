using System;
using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    public abstract class SetupTabBase
    {
        public abstract string Title { get; }
        public virtual bool IsVisible => true;

        /// <summary>
        /// Tab có cần prefab ghi được trong Assets hay không.
        /// Các tab Ads đã chuyển sang ScriptableObject nên trả về false ⇒ dùng được ngay,
        /// không phải clone prefab từ package.
        /// </summary>
        public virtual bool RequiresWritablePrefab => true;

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

    /// <summary>Tab bind thẳng vào một ScriptableObject cấu hình, không đụng prefab.</summary>
    public abstract class ConfigAssetTabBase<T> : SetupTabBase where T : ScriptableObject
    {
        public override bool RequiresWritablePrefab => false;

        private SerializedObject _serializedConfig;

        protected T Config { get; private set; }

        protected SerializedObject SerializedConfig
        {
            get
            {
                if (Config == null) return null;
                if (_serializedConfig == null || _serializedConfig.targetObject != Config)
                    _serializedConfig = new SerializedObject(Config);
                return _serializedConfig;
            }
        }

        protected abstract T CreateOrLoadAsset();
        protected abstract void DrawSection(SerializedObject so);

        public override void Load()
        {
            Config = CreateOrLoadAsset();
            _serializedConfig = null;
        }

        public override void Draw()
        {
            var so = SerializedConfig;
            if (so == null)
            {
                EditorGUILayout.HelpBox($"Chưa có asset {typeof(T).Name}.", MessageType.Warning);
                if (GUILayout.Button($"Tạo {typeof(T).Name}")) Load();
                return;
            }

            so.Update();
            DrawHeader();
            DrawSection(so);
            so.ApplyModifiedProperties();
        }

        protected virtual void DrawHeader() { }

        public override void Save()
        {
            if (Config == null) return;
            SerializedConfig?.ApplyModifiedProperties();
            EditorUtility.SetDirty(Config);
        }
    }

    /// <summary>Tab cấu hình ads: bind vào GameUpAdsConfig.</summary>
    public abstract class AdsConfigTabBase : ConfigAssetTabBase<GameUpAdsConfig>
    {
        protected AdMobIdEditorPlatform Platform;

        protected override GameUpAdsConfig CreateOrLoadAsset()
        {
            Platform = NetworkEditorUI.DefaultPlatform;
            return GameUpAdsConfigAsset.GetOrCreate();
        }

        protected override void DrawHeader()
        {
            EditorGUILayout.LabelField(Title, EditorStyles.boldLabel);
            Platform = NetworkEditorUI.DrawPlatformSelector(Platform);
            EditorGUILayout.Space();
        }
    }

    /// <summary>Tab cấu hình analytics / remote config: bind vào GameUpSdkConfig.</summary>
    public abstract class SdkConfigTabBase : ConfigAssetTabBase<GameUpSdkConfig>
    {
        protected override GameUpSdkConfig CreateOrLoadAsset() => GameUpSdkConfigAsset.GetOrCreate();
    }
}
