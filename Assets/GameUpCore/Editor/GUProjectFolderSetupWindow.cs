#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    public sealed class GUProjectFolderSetupWindow : EditorWindow
    {
        private const string WindowTitle = "GU Folder Setup";
        private const string MenuPath = "GameUp/Project/Folder Setup";
        private const string EditorPrefsKey = "GameUp.ProjectFolderSetup.CustomFolders";
        private const string SetupCompletedKey = "GameUp.ProjectFolderSetup.Completed";
        private const float IndentWidth = 18f;
        private static readonly Color ExistsColor = new Color(0.2f, 0.75f, 0.25f);
        private static readonly Color MissingColor = new Color(0.9f, 0.28f, 0.28f);

        private static readonly string[] RequiredFolders =
        {
            "Assets/_MainProject/Resources",
            "Assets/_MainProject/Resources/Data",
            "Assets/_MainProject/Resources/Data/Singletons",
            //
            "Assets/_MainProject/Art",
            //
            "Assets/_MainProject/Audio",
            //
            "Assets/_MainProject/Data",
            "Assets/_MainProject/Data/Singletons",
            "Assets/_MainProject/Data/NoneSingleton",
            "Assets/_MainProject/Data/NoneSingleton/AudioIdentity",
            //
            "Assets/_MainProject/Prefabs",
            "Assets/_MainProject/Prefabs/Core",
            "Assets/_MainProject/Prefabs/UI",
            "Assets/_MainProject/Prefabs/UI/Helpers",
            "Assets/_MainProject/Prefabs/UI/Popups",
            "Assets/_MainProject/Prefabs/UI/Screens",
            "Assets/_MainProject/Prefabs/Gameplay",
            //
            "Assets/_MainProject/Scenes",
            "Assets/_MainProject/Scenes/Boot",
            "Assets/_MainProject/Scenes/Loading",
            "Assets/_MainProject/Scenes/MainMenu",
            "Assets/_MainProject/Scenes/Gameplay",
            //
            "Assets/_MainProject/Scripts",
            "Assets/_MainProject/Scripts/Core",
            "Assets/_MainProject/Scripts/Gameplay",
            "Assets/_MainProject/Scripts/UI",
            "Assets/_MainProject/Scripts/Audio"
        };

        private static readonly DefaultScriptableObjectConfig[] RequiredScriptableObjects =
        {
            //new DefaultScriptableObjectConfig("Assets/_MainProject/Data/Singletons/AudioDatabase.asset", typeof(GameUp.Core.AudioDatabase)),
            new DefaultScriptableObjectConfig("Assets/_MainProject/Resources/Data/PopupData.asset", typeof(GameUp.Core.UI.PopupData)),
            new DefaultScriptableObjectConfig("Assets/_MainProject/Resources/Data/ScreenData.asset", typeof(GameUp.Core.UI.ScreenData))
        };

        private const string UiPopupsFolderPath = "Assets/_MainProject/Prefabs/UI/Popups";
        private const string UiScreensFolderPath = "Assets/_MainProject/Prefabs/UI/Screens";
        private const string AddressablesUiPopupsGroupName = "UI_Popups";
        private const string AddressablesUiScreensGroupName = "UI_Screens";
        private const string AddressablesUiPopupLabel = "Popup";
        private const string AddressablesUiScreenLabel = "Screen";

        private const string DataSingletonsFolderPath = "Assets/_MainProject/Data/Singletons";
        private const string AddressablesDataGroupName = "Data";
        private const string AddressablesDataLabel = "Data";

        private readonly FolderNode _requiredTreeRoot = new FolderNode("Assets", "Assets");

        [Serializable]
        private sealed class FolderListData
        {
            public List<string> folders = new List<string>();
        }

        private sealed class FolderNode
        {
            public FolderNode(string name, string fullPath)
            {
                Name = name;
                FullPath = fullPath;
            }

            public string Name { get; }
            public string FullPath { get; }
            public Dictionary<string, FolderNode> Children { get; } = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class DefaultScriptableObjectConfig
        {
            public DefaultScriptableObjectConfig(string assetPath, Type assetType)
            {
                AssetPath = assetPath;
                AssetType = assetType;
            }

            public string AssetPath { get; }
            public Type AssetType { get; }
        }

        private readonly List<string> _customFolders = new List<string>();
        private Vector2 _scrollPosition;
        private GUIStyle _treeLabelStyle;

        [MenuItem(MenuPath)]
        private static void OpenWindow()
        {
            var window = GetWindow<GUProjectFolderSetupWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(580f, 440f);
            window.Show();
        }

        public static bool IsSetupCompleted()
        {
            if (!EditorPrefs.GetBool(SetupCompletedKey, false))
            {
                return false;
            }

            for (int index = 0; index < RequiredFolders.Length; index++)
            {
                if (!AssetDatabase.IsValidFolder(RequiredFolders[index]))
                {
                    return false;
                }
            }

            for (int index = 0; index < RequiredScriptableObjects.Length; index++)
            {
                var config = RequiredScriptableObjects[index];
                if (AssetDatabase.LoadAssetAtPath(config.AssetPath, config.AssetType) == null)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnEnable()
        {
            LoadCustomFolders();
            BuildRequiredTree();
        }

        private void OnGUI()
        {
            EnsureGuiStyles();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Project Folder Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Danh sach bat buoc (bao gom Resources) se luon duoc tao. " +
                "Ban co the them/sua/xoa thu muc tuy chinh ben duoi.",
                MessageType.Info);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawRequiredFolders();
            EditorGUILayout.Space(8f);
            DrawCustomFolders();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10f);
            DrawActions();
        }

        private void EnsureGuiStyles()
        {
            if (_treeLabelStyle == null)
            {
                _treeLabelStyle = new GUIStyle(EditorStyles.label) { richText = true };
            }
        }

        private void DrawRequiredFolders()
        {
            EditorGUILayout.LabelField("Required Folders (Locked)", EditorStyles.boldLabel);
            DrawRequiredTree();
            DrawRequiredSummary();
            EditorGUILayout.Space(6f);
            DrawRequiredScriptableObjects();
        }

        private void DrawRequiredTree()
        {
            List<FolderNode> rootChildren = _requiredTreeRoot.Children.Values
                .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int index = 0; index < rootChildren.Count; index++)
            {
                bool isLast = index == rootChildren.Count - 1;
                DrawTreeNode(rootChildren[index], 0, isLast);
            }
        }

        private void DrawRequiredSummary()
        {
            int existingCount = 0;
            for (int index = 0; index < RequiredFolders.Length; index++)
            {
                if (AssetDatabase.IsValidFolder(RequiredFolders[index]))
                {
                    existingCount++;
                }
            }

            int missingCount = RequiredFolders.Length - existingCount;
            EditorGUILayout.HelpBox(
                $"Required folders: Exists {existingCount}/{RequiredFolders.Length} - Missing {missingCount}",
                missingCount == 0 ? MessageType.Info : MessageType.Warning);
        }

        private void DrawTreeNode(FolderNode node, int depth, bool isLast)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * IndentWidth);

            string branch = isLast ? "└──" : "├──";
            bool exists = AssetDatabase.IsValidFolder(node.FullPath);
            string statusColor = ColorUtility.ToHtmlStringRGB(exists ? ExistsColor : MissingColor);
            string nameColor = ColorUtility.ToHtmlStringRGB(exists ? ExistsColor : GUI.contentColor);
            string status = exists ? "EXISTS" : "MISSING";
            string display = $"{branch} <color=#{nameColor}>{node.Name}</color>  <color=#{statusColor}>[{status}]</color>";
            EditorGUILayout.LabelField(display, _treeLabelStyle);

            EditorGUILayout.EndHorizontal();

            List<FolderNode> children = node.Children.Values
                .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int index = 0; index < children.Count; index++)
            {
                bool isChildLast = index == children.Count - 1;
                DrawTreeNode(children[index], depth + 1, isChildLast);
            }
        }

        private void DrawCustomFolders()
        {
            EditorGUILayout.LabelField("Custom Folders", EditorStyles.boldLabel);

            int removeIndex = -1;
            for (int index = 0; index < _customFolders.Count; index++)
            {
                EditorGUILayout.BeginHorizontal();
                _customFolders[index] = EditorGUILayout.TextField($"Custom {index + 1}", _customFolders[index]);
                bool exists = AssetDatabase.IsValidFolder(NormalizePath(_customFolders[index]));
                GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel);
                statusStyle.normal.textColor = exists ? ExistsColor : MissingColor;
                GUILayout.Label(exists ? "EXISTS" : "MISSING", statusStyle, GUILayout.Width(56f));
                if (GUILayout.Button("X", GUILayout.Width(28f)))
                {
                    removeIndex = index;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
            {
                _customFolders.RemoveAt(removeIndex);
                SaveCustomFolders();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Folder", GUILayout.Width(110f)))
            {
                _customFolders.Add("Assets/NewFolder");
                SaveCustomFolders();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRequiredScriptableObjects()
        {
            EditorGUILayout.LabelField("Required ScriptableObjects (Default)", EditorStyles.boldLabel);

            int existingCount = 0;
            for (int index = 0; index < RequiredScriptableObjects.Length; index++)
            {
                var config = RequiredScriptableObjects[index];
                bool exists = AssetDatabase.LoadAssetAtPath(config.AssetPath, config.AssetType) != null;
                if (exists)
                {
                    existingCount++;
                }

                string statusColor = ColorUtility.ToHtmlStringRGB(exists ? ExistsColor : MissingColor);
                string pathColor = ColorUtility.ToHtmlStringRGB(exists ? ExistsColor : GUI.contentColor);
                string status = exists ? "EXISTS" : "MISSING";
                string display = $"• <color=#{pathColor}>{config.AssetPath}</color>  <color=#{statusColor}>[{status}]</color>";
                EditorGUILayout.LabelField(display, _treeLabelStyle);
            }

            int missingCount = RequiredScriptableObjects.Length - existingCount;
            EditorGUILayout.HelpBox(
                $"Required ScriptableObjects: Exists {existingCount}/{RequiredScriptableObjects.Length} - Missing {missingCount}",
                missingCount == 0 ? MessageType.Info : MessageType.Warning);
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Custom List", GUILayout.Height(32f)))
            {
                SaveCustomFolders();
                ShowNotification(new GUIContent("Saved custom folder list."));
            }

            if (GUILayout.Button("Create All Folders", GUILayout.Height(32f)))
            {
                CreateAllFolders();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void CreateAllFolders()
        {
            List<string> allFolders = new List<string>(RequiredFolders.Length + _customFolders.Count);
            allFolders.AddRange(RequiredFolders);
            allFolders.AddRange(_customFolders);

            int createdCount = 0;
            HashSet<string> uniqueFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in allFolders)
            {
                string normalized = NormalizePath(folder);
                if (string.IsNullOrWhiteSpace(normalized) || uniqueFolders.Contains(normalized))
                {
                    continue;
                }

                uniqueFolders.Add(normalized);
                if (!TryEnsureFolder(normalized))
                {
                    EditorUtility.DisplayDialog("Invalid Folder Path", $"Khong the tao thu muc: {normalized}", "OK");
                    return;
                }

                createdCount++;
            }

            AssetDatabase.Refresh();
            EnsureDefaultAudioAssets();
            EnsureDefaultUiDataAssets();
            EnsureDataFoldersInAddressables();
            EnsureAddressableDataHolderAsset();
            EditorPrefs.SetBool(SetupCompletedKey, true);
            ShowNotification(new GUIContent($"Done. Checked {createdCount} folder(s)."));
        }

        private static void EnsureDefaultAudioAssets()
        {
            EnsureAudioIdScript();
            AssetDatabase.Refresh();
        }

        private static void EnsureAudioIdScript()
        {
            const string audioIdScriptPath = "Assets/_MainProject/Scripts/Audio/AudioID.cs";
            if (File.Exists(audioIdScriptPath))
            {
                return;
            }

            const string content =
@"public static class AudioID
{
    private static GameUp.Core.AudioIdentity Get(string name)
    {
        return GameUp.Core.AudioManager.TryGetIdentity(name, out var identity) ? identity : null;
    }
}";
            File.WriteAllText(audioIdScriptPath, content);
        }

        private static void EnsureDefaultUiDataAssets()
        {
            for (int index = 0; index < RequiredScriptableObjects.Length; index++)
            {
                var config = RequiredScriptableObjects[index];
                EnsureScriptableObjectAsset(config.AssetPath, config.AssetType);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EnsureUiFoldersInAddressables();
        }

        private static void EnsureAddressableDataHolderAsset()
        {
            var holder = GameUp.Core.AddressableDataHolder.Editor_EnsureAssetExists();
            if (!holder)
            {
                return;
            }

            holder.Editor_RebuildReferencesFromFolder();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureDataFoldersInAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
            {
                settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            }

            if (settings == null)
            {
                GULogger.Warning("FolderSetup", "Không tìm thấy AddressableAssetSettings. Bỏ qua bước add Data folders vào Addressables.");
                return;
            }

            EnsureAddressablesLabel(settings, AddressablesDataLabel);

            var dataGroup = GetOrCreateAddressablesGroup(settings, AddressablesDataGroupName);
            if (dataGroup == null)
            {
                return;
            }

            var processed = 0;
            processed += EnsureFolderEntry(settings, dataGroup, DataSingletonsFolderPath, AddressablesDataLabel) ? 1 : 0;

            if (processed > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, settings, true, false);
                GULogger.Log("FolderSetup", $"Đã thêm/cập nhật {processed} Data folder(s) vào Addressables (group \"{AddressablesDataGroupName}\", label \"{AddressablesDataLabel}\").");
            }
        }

        private static void EnsureUiFoldersInAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
            {
                settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            }

            if (settings == null)
            {
                GULogger.Warning("FolderSetup", "Không tìm thấy AddressableAssetSettings. Bỏ qua bước add UI folders vào Addressables.");
                return;
            }

            EnsureAddressablesLabel(settings, AddressablesUiPopupLabel);
            EnsureAddressablesLabel(settings, AddressablesUiScreenLabel);

            var popupsGroup = GetOrCreateAddressablesGroup(settings, AddressablesUiPopupsGroupName);
            var screensGroup = GetOrCreateAddressablesGroup(settings, AddressablesUiScreensGroupName);
            if (popupsGroup == null || screensGroup == null)
            {
                return;
            }

            var processed = 0;
            processed += EnsureFolderEntry(settings, popupsGroup, UiPopupsFolderPath, AddressablesUiPopupLabel) ? 1 : 0;
            processed += EnsureFolderEntry(settings, screensGroup, UiScreensFolderPath, AddressablesUiScreenLabel) ? 1 : 0;

            if (processed > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, settings, true, false);
                GULogger.Log("FolderSetup", $"Đã thêm/cập nhật {processed} UI folder(s) vào Addressables (groups \"{AddressablesUiPopupsGroupName}\", \"{AddressablesUiScreensGroupName}\").");
            }
        }

        private static void EnsureAddressablesLabel(AddressableAssetSettings settings, string label)
        {
            if (settings == null || string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            var labels = settings.GetLabels();
            if (labels == null || !labels.Contains(label))
            {
                settings.AddLabel(label, false);
            }
        }

        private static AddressableAssetGroup GetOrCreateAddressablesGroup(AddressableAssetSettings settings, string groupName)
        {
            if (settings == null || string.IsNullOrWhiteSpace(groupName))
            {
                return null;
            }

            var group = settings.FindGroup(groupName);
            if (group != null)
            {
                return group;
            }

            group = settings.CreateGroup(
                groupName,
                false,
                false,
                false,
                null,
                typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema));

            if (group == null)
            {
                GULogger.Error("FolderSetup", $"Không tạo được Addressables group \"{groupName}\".");
                return null;
            }

            GULogger.Log("FolderSetup", $"Đã tạo Addressables group \"{groupName}\".");
            return group;
        }

        private static bool EnsureFolderEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string folderAssetPath, string label)
        {
            if (settings == null || group == null || string.IsNullOrWhiteSpace(folderAssetPath))
            {
                return false;
            }

            var path = folderAssetPath.Replace("\\", "/").TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(path))
            {
                return false;
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }

            var entry = settings.CreateOrMoveEntry(guid, group, false, true);
            if (entry == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(label))
            {
                entry.SetLabel(label, true, true, true);
            }

            return true;
        }

        private static void EnsureScriptableObjectAsset(string assetPath, Type scriptableObjectType)
        {
            if (AssetDatabase.LoadAssetAtPath(assetPath, scriptableObjectType) != null)
            {
                return;
            }

            if (!typeof(ScriptableObject).IsAssignableFrom(scriptableObjectType))
            {
                return;
            }

            string folderPath = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                TryEnsureFolder(folderPath);
            }

            var asset = ScriptableObject.CreateInstance(scriptableObjectType);
            if (asset == null)
            {
                return;
            }

            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        private static bool TryEnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return true;
            }

            if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] parts = assetPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !parts[0].Equals("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string currentPath = "Assets";
            for (int index = 1; index < parts.Length; index++)
            {
                string nextPath = $"{currentPath}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    string createdGuid = AssetDatabase.CreateFolder(currentPath, parts[index]);
                    if (string.IsNullOrEmpty(createdGuid))
                    {
                        return false;
                    }
                }

                currentPath = nextPath;
            }

            return true;
        }

        private static string NormalizePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return string.Empty;
            }

            string normalized = rawPath.Replace('\\', '/').Trim();
            while (normalized.Contains("//"))
            {
                normalized = normalized.Replace("//", "/");
            }

            return normalized.TrimEnd('/');
        }

        private void SaveCustomFolders()
        {
            List<string> normalized = _customFolders
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _customFolders.Clear();
            _customFolders.AddRange(normalized);

            var data = new FolderListData { folders = normalized };
            string json = JsonUtility.ToJson(data);
            EditorPrefs.SetString(EditorPrefsKey, json);
        }

        private void LoadCustomFolders()
        {
            _customFolders.Clear();
            string json = EditorPrefs.GetString(EditorPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            FolderListData data = JsonUtility.FromJson<FolderListData>(json);
            if (data == null || data.folders == null)
            {
                return;
            }

            foreach (string folder in data.folders)
            {
                string normalized = NormalizePath(folder);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    _customFolders.Add(normalized);
                }
            }
        }

        private void BuildRequiredTree()
        {
            _requiredTreeRoot.Children.Clear();

            for (int folderIndex = 0; folderIndex < RequiredFolders.Length; folderIndex++)
            {
                string folderPath = NormalizePath(RequiredFolders[folderIndex]);
                string[] segments = folderPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length == 0 || !segments[0].Equals("Assets", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                FolderNode currentNode = _requiredTreeRoot;
                string currentPath = "Assets";
                for (int segmentIndex = 1; segmentIndex < segments.Length; segmentIndex++)
                {
                    string segment = segments[segmentIndex];
                    currentPath = $"{currentPath}/{segment}";

                    if (!currentNode.Children.TryGetValue(segment, out FolderNode nextNode))
                    {
                        nextNode = new FolderNode(segment, currentPath);
                        currentNode.Children.Add(segment, nextNode);
                    }

                    currentNode = nextNode;
                }
            }
        }
    }
}
#endif
