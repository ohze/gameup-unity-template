using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameUp.SDK.Editor.Setup
{
    public enum ConfigIssueSeverity
    {
        Error,
        Warning,
        Info
    }

    /// <summary>Một phát hiện của <see cref="GameUpConfigValidator"/>, kèm object để Ping tới tận nơi.</summary>
    public class ConfigIssue
    {
        public ConfigIssueSeverity Severity;

        /// <summary>Nơi tìm thấy: tên scene hoặc đường dẫn prefab.</summary>
        public string Location;

        /// <summary>Đường dẫn object trong hierarchy, vd "SDK/AdmobAds".</summary>
        public string ObjectPath;

        public string Message;

        /// <summary>Cách xử lý cụ thể, hiện dưới dạng dòng thứ hai.</summary>
        public string Fix;

        /// <summary>Object để PingObject khi bấm vào. Null với prefab chưa load được.</summary>
        public UnityEngine.Object Target;

        public string Title => string.IsNullOrEmpty(ObjectPath) ? Location : $"{Location} → {ObjectPath}";
    }

    /// <summary>
    /// Soát các component của SDK trong scene đang mở và trong prefab của project để phát hiện
    /// trường hợp lúc chạy sẽ KHÔNG lấy được config — thứ mà Editor không hề báo, chỉ tới lúc
    /// chạy trên máy mới thấy dòng "thiếu GameUpAdsConfig, bỏ qua init" rồi mất sạch quảng cáo.
    ///
    /// Điểm mấu chốt: <c>configOverride</c> là tham chiếu TRỰC TIẾP nên không cần nằm trong
    /// Resources; chỉ asset dùng chung (nạp bằng <c>Resources.Load</c>) mới bắt buộc.
    /// </summary>
    public static class GameUpConfigValidator
    {
        private const string OverridePropertyName = "configOverride";
        private const string SdkNamespace = "GameUp.SDK";

        [MenuItem("GameUp/SDK/Kiểm tra tham chiếu Config")]
        private static void ValidateFromMenu()
        {
            var issues = Validate();
            if (issues.Count == 0)
            {
                Debug.Log("[GameUp.SDK] Kiểm tra tham chiếu Config: không phát hiện vấn đề.");
                return;
            }

            foreach (var issue in issues)
            {
                string line = $"[GameUp.SDK] {issue.Title}\n{issue.Message}\n→ {issue.Fix}";
                switch (issue.Severity)
                {
                    case ConfigIssueSeverity.Error: Debug.LogError(line, issue.Target); break;
                    case ConfigIssueSeverity.Warning: Debug.LogWarning(line, issue.Target); break;
                    default: Debug.Log(line, issue.Target); break;
                }
            }
        }

        /// <summary>
        /// Quét scene đang mở, và (nếu bật) toàn bộ prefab trong Assets.
        /// Prefab gốc trong package luôn bị bỏ qua: chúng read-only, để trống override là đúng thiết kế.
        /// </summary>
        public static List<ConfigIssue> Validate(bool includeProjectPrefabs = true)
        {
            var issues = new List<ConfigIssue>();

            var adsState = ResolveSharedAsset<GameUpAdsConfig>(GameUpAdsConfig.ResourcePath);
            var sdkState = ResolveSharedAsset<GameUpSdkConfig>(GameUpSdkConfig.ResourcePath);

            AppendSharedAssetIssues(issues, adsState);
            AppendSharedAssetIssues(issues, sdkState);

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (var root in scene.GetRootGameObjects())
                    ScanHierarchy(root, $"Scene \"{scene.name}\"", issues, adsState, sdkState);
            }

            if (includeProjectPrefabs)
            {
                string packagePrefabDir = GameUpSetupPaths.GetPackagePrefabDirectory();
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;
                    if (!path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                    if (path.StartsWith(packagePrefabDir, StringComparison.OrdinalIgnoreCase)) continue;

                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;
                    ScanHierarchy(go, path, issues, adsState, sdkState);
                }
            }

            return issues;
        }

        // =====================================================================
        // Quét hierarchy
        // =====================================================================

        private static void ScanHierarchy(GameObject root, string location, List<ConfigIssue> issues,
            SharedAssetState adsState, SharedAssetState sdkState)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                // Script bị mất (missing script) cũng cho ra component null — bỏ qua, không phải việc của hàm này.
                if (component == null) continue;

                var type = component.GetType();
                if (type.Namespace == null || !type.Namespace.StartsWith(SdkNamespace, StringComparison.Ordinal))
                    continue;

                var so = new SerializedObject(component);
                var prop = so.FindProperty(OverridePropertyName);
                if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference) continue;

                // prop.type có dạng "PPtr<$GameUpAdsConfig>" — đọc được cả khi ô đang để trống,
                // nhờ vậy không phải liệt kê cứng danh sách component nào dùng config nào.
                string declaredType = ExtractTypeName(prop.type);
                SharedAssetState state =
                    declaredType == nameof(GameUpAdsConfig) ? adsState :
                    declaredType == nameof(GameUpSdkConfig) ? sdkState : null;
                if (state == null) continue;

                Evaluate(component, location, prop.objectReferenceValue, state, issues);
            }
        }

        private static void Evaluate(Component component, string location, UnityEngine.Object overrideAsset,
            SharedAssetState state, List<ConfigIssue> issues)
        {
            string objectPath = GetHierarchyPath(component.transform);
            string label = $"{component.GetType().Name} trên \"{objectPath}\"";

            if (overrideAsset != null)
            {
                // Có tham chiếu trực tiếp thì lúc chạy chắc chắn lấy được, kể cả khi asset nằm
                // ngoài Resources — Unity đóng gói asset được tham chiếu trực tiếp vào build.
                if (state.Shared != null && overrideAsset != state.Shared)
                {
                    issues.Add(new ConfigIssue
                    {
                        Severity = ConfigIssueSeverity.Warning,
                        Location = location,
                        ObjectPath = objectPath,
                        Target = component,
                        Message = $"{label} đang dùng asset RIÊNG \"{AssetDatabase.GetAssetPath(overrideAsset)}\", " +
                                  "không phải asset chung của project.",
                        Fix = "Cố ý thì bỏ qua. Nếu không, xoá ô configOverride để nó dùng asset chung."
                    });
                }
                return;
            }

            // Ô trống = dùng asset chung. Từ đây trở xuống là các cách asset chung không tới được runtime.
            if (!state.AnyExists)
            {
                issues.Add(new ConfigIssue
                {
                    Severity = ConfigIssueSeverity.Error,
                    Location = location,
                    ObjectPath = objectPath,
                    Target = component,
                    Message = $"{label} để trống configOverride, mà project CHƯA có asset {state.TypeName} nào. " +
                              "Lúc chạy component này sẽ không lấy được cấu hình và tự bỏ qua init.",
                    Fix = $"Mở GameUp → SDK → Setup rồi bấm \"Lưu cấu hình\" để tạo asset tại {state.ExpectedPath}."
                });
                return;
            }

            if (!state.LoadableFromResources)
            {
                issues.Add(new ConfigIssue
                {
                    Severity = ConfigIssueSeverity.Error,
                    Location = location,
                    ObjectPath = objectPath,
                    Target = component,
                    Message = $"{label} để trống configOverride. Asset {state.TypeName} có tồn tại " +
                              $"(\"{state.FoundPath}\") nhưng KHÔNG nằm đúng chỗ Resources.Load đọc được. " +
                              "Trong Editor vẫn chạy nhờ đường vòng tìm asset trong project, nhưng BUILD sẽ null.",
                    Fix = $"Di chuyển asset về đúng {state.ExpectedPath}."
                });
            }
        }

        // =====================================================================
        // Trạng thái asset chung
        // =====================================================================

        private class SharedAssetState
        {
            public string TypeName;
            public string ResourcePath;
            public string ExpectedPath;
            public UnityEngine.Object Shared;
            public string FoundPath;
            public bool AnyExists;
            public bool LoadableFromResources;
            public List<string> AllPaths = new List<string>();
        }

        private static SharedAssetState ResolveSharedAsset<T>(string resourcePath) where T : ScriptableObject
        {
            var state = new SharedAssetState
            {
                TypeName = typeof(T).Name,
                ResourcePath = resourcePath,
                ExpectedPath = $"{GameUpSetupPaths.WritableResourcesRoot}/{resourcePath}.asset"
            };

            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                state.AllPaths.Add(path);
                state.AnyExists = true;

                // Resources.Load("GameUpSDK/GameUpAdsConfig") chỉ thấy asset nằm đúng
                // .../Resources/GameUpSDK/GameUpAdsConfig.asset — nằm chỗ khác là build không đọc được.
                if (path.EndsWith("/Resources/" + resourcePath + ".asset", StringComparison.OrdinalIgnoreCase))
                {
                    state.LoadableFromResources = true;
                    state.Shared = AssetDatabase.LoadAssetAtPath<T>(path);
                    state.FoundPath = path;
                }
            }

            if (state.Shared == null && state.AnyExists)
            {
                state.FoundPath = state.AllPaths[0];
                state.Shared = AssetDatabase.LoadAssetAtPath<T>(state.FoundPath);
            }

            return state;
        }

        private static void AppendSharedAssetIssues(List<ConfigIssue> issues, SharedAssetState state)
        {
            if (state.AllPaths.Count <= 1) return;

            issues.Add(new ConfigIssue
            {
                Severity = ConfigIssueSeverity.Warning,
                Location = "Project",
                ObjectPath = state.TypeName,
                Target = state.Shared,
                Message = $"Có {state.AllPaths.Count} asset {state.TypeName} trong project:\n  " +
                          string.Join("\n  ", state.AllPaths),
                Fix = "Chỉ giữ lại một bản ở " + state.ExpectedPath +
                      ". Nhiều bản khiến mỗi nơi đọc một kiểu và rất khó lần ra."
            });
        }

        /// <summary>
        /// Cảnh báo tại chỗ cho Inspector của component. Chỉ báo đúng một tình huống mà Inspector
        /// hiện tại không bắt được: asset chung CÓ tồn tại nên Editor chạy ngon, nhưng nằm ngoài
        /// đường dẫn <c>Resources.Load</c> đọc được nên build ra là null.
        /// Có <c>configOverride</c> thì im lặng — tham chiếu trực tiếp luôn an toàn khi build.
        /// </summary>
        public static void DrawInspectorConfigWarning(UnityEngine.Object resolvedConfig, bool usingOverride)
        {
            if (resolvedConfig == null || usingOverride) return;

            string resourcePath =
                resolvedConfig is GameUpAdsConfig ? GameUpAdsConfig.ResourcePath :
                resolvedConfig is GameUpSdkConfig ? GameUpSdkConfig.ResourcePath : null;
            if (resourcePath == null) return;

            string path = AssetDatabase.GetAssetPath(resolvedConfig);
            if (path.EndsWith("/Resources/" + resourcePath + ".asset", StringComparison.OrdinalIgnoreCase)) return;

            EditorGUILayout.HelpBox(
                $"Asset đang dùng nằm ngoài Resources nên BUILD sẽ không đọc được (Editor vẫn chạy nhờ " +
                $"đường vòng tìm asset trong project).\nHiện tại: {path}\n" +
                $"Cần chuyển về: {GameUpSetupPaths.WritableResourcesRoot}/{resourcePath}.asset",
                MessageType.Error);
        }

        // =====================================================================
        // Helper
        // =====================================================================

        /// <summary>"PPtr&lt;$GameUpAdsConfig&gt;" → "GameUpAdsConfig".</summary>
        private static string ExtractTypeName(string serializedType)
        {
            int start = serializedType.IndexOf('$');
            int end = serializedType.LastIndexOf('>');
            if (start < 0 || end <= start) return serializedType;
            return serializedType.Substring(start + 1, end - start - 1);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var parts = new List<string>();
            for (var t = transform; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
