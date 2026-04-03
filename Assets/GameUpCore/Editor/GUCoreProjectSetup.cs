#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using GameUp.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Đồng bộ prefab Core/UI helpers từ GameUpCore (Assets hoặc UPM Packages) sang _MainProject và đặt root prefab lên scene hiện tại.
    /// </summary>
    public static class GUCoreProjectSetup
    {
        public const string MenuPath = "GameUp/Project/Core setup";

        private const string MainProjectCorePrefabsPath = "Assets/_MainProject/Prefabs/Core";
        private const string MainProjectUiHelpersPath = "Assets/_MainProject/Prefabs/UI/Helpers";
        private const string ManagerPrefabFileName = "====Manager====.prefab";
        private const string UiRootPrefabFileName = "=====UI=====.prefab";

        private static readonly string ManagerPrefabProjectPath = $"{MainProjectCorePrefabsPath}/{ManagerPrefabFileName}";
        private static readonly string UiRootPrefabProjectPath = $"{MainProjectCorePrefabsPath}/{UiRootPrefabFileName}";

        [MenuItem(MenuPath)]
        public static void RunFromMenu()
        {
            RunCoreSetup(log: true);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateRunFromMenu()
        {
            return GUProjectFolderSetupWindow.IsSetupCompleted();
        }

        /// <summary>
        /// Copy prefab nguồn sang _MainProject (nếu thiếu) và instantiate lên scene hiện tại nếu chưa có instance tương ứng.
        /// </summary>
        public static bool RunCoreSetup(bool log)
        {
            if (!TryGetGameUpCoreAssetRoot(out var root))
            {
                if (log)
                    GULogger.Error("CoreSetup", "Không xác định được thư mục gốc GameUpCore (Prefab/Core). Kiểm tra package/runtime có chứa GUPoolers.");
                return false;
            }

            var srcCore = $"{root}/Prefab/Core".Replace("\\", "/");
            var srcLoading = $"{root}/Prefab/UI/Loading".Replace("\\", "/");
            var srcToast = $"{root}/Prefab/UI/Toast".Replace("\\", "/");

            CopyAssetsIntoFolder(srcLoading, $"{MainProjectUiHelpersPath}/Loading");
            CopyAssetsIntoFolder(srcToast, $"{MainProjectUiHelpersPath}/Toast");
            CopyAssetsIntoFolder(srcCore, MainProjectCorePrefabsPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RemapUiPrefabGuidsToMainProjectHelpers(root, log);

            var managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabProjectPath);
            var uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPrefabProjectPath);
            if (!managerPrefab || !uiPrefab)
            {
                if (log)
                    GULogger.Error("CoreSetup", $"Sau khi copy vẫn thiếu prefab tại {ManagerPrefabProjectPath} hoặc {UiRootPrefabProjectPath}.");
                return false;
            }

            EnsurePrefabRootInScene(managerPrefab, ManagerPrefabProjectPath, log);
            EnsurePrefabRootInScene(uiPrefab, UiRootPrefabProjectPath, log);

            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && !scene.isDirty)
                EditorSceneManager.MarkSceneDirty(scene);

            if (log)
                GULogger.Log("CoreSetup", "Đã hoàn tất Core setup (copy prefab + scene).");

            return true;
        }

        /// <summary>
        /// Đảm bảo scene có AudioManager (qua prefab ====Manager==== trong _MainProject). Chạy <see cref="RunCoreSetup"/> nếu cần.
        /// </summary>
        public static bool EnsureAudioManagerInScene(bool log)
        {
            if (UnityEngine.Object.FindObjectOfType<AudioManager>())
                return true;

            RunCoreSetup(log);
            return UnityEngine.Object.FindObjectOfType<AudioManager>();
        }

        internal static bool TryGetGameUpCoreAssetRoot(out string rootAssetPath)
        {
            rootAssetPath = null;
            var guids = AssetDatabase.FindAssets("GUPoolers t:MonoScript");
            foreach (var guid in guids)
            {
                var scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!scriptPath.EndsWith("GUPoolers.cs", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var dir = Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");
                for (var i = 0; i < 12 && !string.IsNullOrEmpty(dir); i++)
                {
                    var test = $"{dir}/Prefab/Core/{ManagerPrefabFileName}";
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(test))
                    {
                        rootAssetPath = dir;
                        return true;
                    }

                    dir = Path.GetDirectoryName(dir)?.Replace("\\", "/");
                }
            }

            return false;
        }

        private static void EnsureAssetFolderExists(string assetFolderPath)
        {
            assetFolderPath = assetFolderPath.Replace("\\", "/").TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetFolderPath))
                return;

            var parent = Path.GetDirectoryName(assetFolderPath)?.Replace("\\", "/");
            var leaf = Path.GetFileName(assetFolderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
                return;

            EnsureAssetFolderExists(parent);
            if (!AssetDatabase.IsValidFolder(assetFolderPath))
                AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>
        /// Ghi đè GUID trong YAML prefab: mọi tham chiếu tới prefab UI trong package được trỏ sang bản
        /// <c>Assets/_MainProject/Prefabs/UI/Helpers</c> (Select trong Inspector sẽ mở đúng _MainProject).
        /// </summary>
        private static void RemapUiPrefabGuidsToMainProjectHelpers(string packageRoot, bool log)
        {
            packageRoot = packageRoot.Replace("\\", "/").TrimEnd('/');
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);

            void MapPath(string packageRelative, string mainAssetPath)
            {
                mainAssetPath = mainAssetPath.Replace("\\", "/");
                var pkgPath = $"{packageRoot}/{packageRelative}".Replace("\\", "/");
                var fromGuid = AssetDatabase.AssetPathToGUID(pkgPath);
                var toGuid = AssetDatabase.AssetPathToGUID(mainAssetPath);
                if (string.IsNullOrEmpty(toGuid))
                    return;
                if (!string.IsNullOrEmpty(fromGuid) && !string.Equals(fromGuid, toGuid, StringComparison.Ordinal))
                    replacements[fromGuid] = toGuid;
            }

            MapPath("Prefab/UI/Loading/Loading.prefab", $"{MainProjectUiHelpersPath}/Loading/Loading.prefab");
            MapPath("Prefab/UI/Toast/Toast.prefab", $"{MainProjectUiHelpersPath}/Toast/Toast.prefab");
            MapPath("Prefab/UI/Loading/LoadingItem.prefab", $"{MainProjectUiHelpersPath}/Loading/LoadingItem.prefab");
            MapPath("Prefab/UI/Loading/LoadingItemCutoutMask.prefab", $"{MainProjectUiHelpersPath}/Loading/LoadingItemCutoutMask.prefab");
            MapPath("Prefab/UI/Toast/ToastItem.prefab", $"{MainProjectUiHelpersPath}/Toast/ToastItem.prefab");

            var mainLoadingGuid = AssetDatabase.AssetPathToGUID($"{MainProjectUiHelpersPath}/Loading/Loading.prefab");
            var mainToastGuid = AssetDatabase.AssetPathToGUID($"{MainProjectUiHelpersPath}/Toast/Toast.prefab");
            var mainToastItemGuid = AssetDatabase.AssetPathToGUID($"{MainProjectUiHelpersPath}/Toast/ToastItem.prefab");

            void MapLegacy(string legacyGuid, string toGuid)
            {
                if (string.IsNullOrEmpty(legacyGuid) || string.IsNullOrEmpty(toGuid))
                    return;
                if (string.Equals(legacyGuid, toGuid, StringComparison.Ordinal))
                    return;
                replacements[legacyGuid] = toGuid;
            }

            MapLegacy("3e3a94b5277140849afb57f2262bf6a9", mainLoadingGuid);
            MapLegacy("03f2ed98d7387a14198736508cf2b3e7", mainToastGuid);
            MapLegacy("fcbeefaea26185641a293351cc62a9a4", mainToastItemGuid);

            if (replacements.Count == 0)
                return;

            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                UiRootPrefabProjectPath.Replace("\\", "/"),
                $"{MainProjectUiHelpersPath}/Loading/Loading.prefab",
                $"{MainProjectUiHelpersPath}/Toast/Toast.prefab",
            };

            foreach (var assetPath in targets)
            {
                if (!assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;
                var physical = TryGetPhysicalAssetPath(assetPath);
                if (string.IsNullOrEmpty(physical) || !File.Exists(physical))
                    continue;

                var text = File.ReadAllText(physical);
                var original = text;
                foreach (var kv in replacements)
                    text = text.Replace(kv.Key, kv.Value);

                if (string.Equals(text, original, StringComparison.Ordinal))
                    continue;

                File.WriteAllText(physical, text);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                if (log)
                    GULogger.Log("CoreSetup", $"Đã trỏ prefab UI về _MainProject/Helpers: {assetPath}");
            }
        }

        private static string TryGetPhysicalAssetPath(string assetPath)
        {
            assetPath = assetPath.Replace("\\", "/");
            if (assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)));
            if (assetPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                return string.IsNullOrEmpty(projectRoot)
                    ? null
                    : Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            }

            return null;
        }

        private static void CopyAssetsIntoFolder(string sourceFolderAssetPath, string destinationFolderAssetPath)
        {
            sourceFolderAssetPath = sourceFolderAssetPath.Replace("\\", "/").TrimEnd('/');
            destinationFolderAssetPath = destinationFolderAssetPath.Replace("\\", "/").TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(sourceFolderAssetPath))
                return;

            EnsureAssetFolderExists(destinationFolderAssetPath);

            var guids = AssetDatabase.FindAssets("", new[] { sourceFolderAssetPath });
            var processed = new HashSet<string>();
            foreach (var guid in guids)
            {
                var srcPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!processed.Add(srcPath))
                    continue;
                if (!srcPath.StartsWith(sourceFolderAssetPath, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (AssetDatabase.IsValidFolder(srcPath))
                    continue;

                var relative = srcPath.Substring(sourceFolderAssetPath.Length).TrimStart('/', '\\');
                var dstPath = $"{destinationFolderAssetPath}/{relative}".Replace("\\", "/");
                var dstDir = Path.GetDirectoryName(dstPath)?.Replace("\\", "/");
                if (!string.IsNullOrEmpty(dstDir))
                    EnsureAssetFolderExists(dstDir);

                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dstPath))
                    continue;

                if (!AssetDatabase.CopyAsset(srcPath, dstPath))
                    GULogger.Warning("CoreSetup", $"CopyAsset thất bại: {srcPath} -> {dstPath}");
            }
        }

        private static void EnsurePrefabRootInScene(GameObject prefabAsset, string prefabAssetPath, bool log)
        {
            if (IsPrefabRootAlreadyInActiveScene(prefabAsset, prefabAssetPath))
                return;

            var instance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
            if (!instance)
            {
                if (log)
                    GULogger.Error("CoreSetup", $"Không InstantiatePrefab được: {prefabAssetPath}");
                return;
            }

            Undo.RegisterCreatedObjectUndo(instance, $"Core setup: {instance.name}");
            UnpackCreatedPrefabInstance(instance, prefabAssetPath, log);
            Selection.activeGameObject = instance;
        }

        private static void UnpackCreatedPrefabInstance(GameObject instance, string prefabAssetPath, bool log)
        {
            if (!instance)
                return;
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(instance))
                return;

            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            if (log)
                GULogger.Log("CoreSetup", $"Đã unpack prefab instance sau khi tạo: {prefabAssetPath}");
        }

        private static bool IsPrefabRootAlreadyInActiveScene(GameObject prefabAsset, string prefabAssetPath)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return false;

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (!root)
                    continue;
                if (PrefabUtility.GetCorrespondingObjectFromSource(root) != prefabAsset)
                    continue;
                var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                if (string.Equals(path, prefabAssetPath, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
#endif
