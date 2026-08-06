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
        private bool _showOnlyMissing;

        [MenuItem(MenuPath)]
        private static void OpenWindow()
        {
            OpenWindowFromInstaller();
        }

        /// <summary>Mở cửa sổ không qua menu validate (installer gọi khi bước 1 đã xong).</summary>
        public static void OpenWindowFromInstaller()
        {
            var window = GetWindow<GUProjectFolderSetupWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(580f, 440f);
            window.Show();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateOpenWindow()
        {
            return GUDotweenDependencyUtility.CanUseCoreTools();
        }

        /// <summary>Số thư mục / ScriptableObject bắt buộc đã có, dùng cho thanh tiến độ.</summary>
        public static (int folders, int totalFolders, int assets, int totalAssets) GetSetupProgress()
        {
            int folders = 0;
            for (int index = 0; index < RequiredFolders.Length; index++)
            {
                if (AssetDatabase.IsValidFolder(RequiredFolders[index])) folders++;
            }

            int assets = 0;
            for (int index = 0; index < RequiredScriptableObjects.Length; index++)
            {
                var config = RequiredScriptableObjects[index];
                if (AssetDatabase.LoadAssetAtPath(config.AssetPath, config.AssetType) != null) assets++;
            }

            return (folders, RequiredFolders.Length, assets, RequiredScriptableObjects.Length);
        }

        /// <summary>
        /// Setup coi là xong khi project THỰC SỰ có đủ thư mục + asset bắt buộc.
        /// Trước đây phải có thêm cờ EditorPrefs, nên project clone sẵn (hoặc đổi máy/đổi user)
        /// dù đủ file vẫn bị khoá hết menu — nay cờ chỉ được đồng bộ lại theo trạng thái thật.
        /// </summary>
        public static bool IsSetupCompleted()
        {
            var (folders, totalFolders, assets, totalAssets) = GetSetupProgress();
            bool completed = folders == totalFolders && assets == totalAssets;

            if (EditorPrefs.GetBool(SetupCompletedKey, false) != completed)
            {
                EditorPrefs.SetBool(SetupCompletedKey, completed);
            }

            return completed;
        }

        private void OnEnable()
        {
            LoadCustomFolders();
            BuildRequiredTree();
            EditorApplication.projectChanged += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= Repaint;
        }

        private void OnFocus() => Repaint();

        private void OnGUI()
        {
            EnsureGuiStyles();
            GUInstallerUI.EnsureStyles();

            DrawHeader();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawRequiredFolders();
            DrawRequiredScriptableObjects();
            DrawCustomFolders();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8f);
            DrawActions();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Project Folder Setup", EditorStyles.largeLabel);
            GUInstallerUI.Hint("Thư mục bắt buộc luôn được tạo lại khi bấm \"Tạo tất cả\". Thư mục tùy chỉnh nằm ở cuối danh sách.");

            var (folders, totalFolders, assets, totalAssets) = GetSetupProgress();
            EditorGUILayout.Space(4f);
            GUInstallerUI.ProgressBar("Đã có", folders + assets, totalFolders + totalAssets);

            EditorGUILayout.BeginHorizontal();
            _showOnlyMissing = GUILayout.Toggle(_showOnlyMissing, "Chỉ hiện mục còn thiếu", EditorStyles.miniButton, GUILayout.Width(170f));
            GUILayout.FlexibleSpace();
            if (GUInstallerUI.MiniButton("Kiểm tra lại", true, 100f))
            {
                AssetDatabase.Refresh();
                BuildRequiredTree();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2f);
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
            var (folders, totalFolders, _, _) = GetSetupProgress();

            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader(
                    $"{folders}/{totalFolders}",
                    "Thư mục bắt buộc",
                    folders == totalFolders ? GUSetupState.Done : GUSetupState.Missing);

                if (_showOnlyMissing && folders == totalFolders)
                {
                    GUInstallerUI.Hint("Không còn thư mục nào thiếu.");
                    return;
                }

                List<FolderNode> rootChildren = _requiredTreeRoot.Children.Values
                    .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                for (int index = 0; index < rootChildren.Count; index++)
                {
                    bool isLast = index == rootChildren.Count - 1;
                    DrawTreeNode(rootChildren[index], 0, isLast);
                }
            }
        }

        /// <summary>Node hoặc bất kỳ con nào của node còn thiếu — dùng cho bộ lọc "chỉ hiện mục thiếu".</summary>
        private static bool HasMissingInSubtree(FolderNode node)
        {
            if (!AssetDatabase.IsValidFolder(node.FullPath)) return true;
            foreach (var child in node.Children.Values)
            {
                if (HasMissingInSubtree(child)) return true;
            }

            return false;
        }

        private void DrawTreeNode(FolderNode node, int depth, bool isLast)
        {
            if (_showOnlyMissing && !HasMissingInSubtree(node)) return;

            bool exists = AssetDatabase.IsValidFolder(node.FullPath);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * IndentWidth);

            string branch = isLast ? "└──" : "├──";
            string nameColor = ColorUtility.ToHtmlStringRGB(exists ? ExistsColor : MissingColor);
            string mark = exists ? "✔" : "✚";
            EditorGUILayout.LabelField($"{branch} <color=#{nameColor}>{mark} {node.Name}</color>", _treeLabelStyle);

            if (!exists)
            {
                if (GUILayout.Button("Tạo", EditorStyles.miniButton, GUILayout.Width(50f)))
                {
                    TryEnsureFolder(node.FullPath);
                    AssetDatabase.Refresh();
                }
            }
            else if (GUILayout.Button("Mở", EditorStyles.miniButton, GUILayout.Width(50f)))
            {
                GUInstallerUI.PingPath(node.FullPath);
            }

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
            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader($"{_customFolders.Count}", "Thư mục tùy chỉnh", GUSetupState.Optional);

                if (_customFolders.Count == 0)
                {
                    GUInstallerUI.Hint("Chưa có thư mục tùy chỉnh nào. Danh sách này được lưu theo máy (EditorPrefs).");
                }

                int removeIndex = -1;
                for (int index = 0; index < _customFolders.Count; index++)
                {
                    bool exists = AssetDatabase.IsValidFolder(NormalizePath(_customFolders[index]));

                    EditorGUILayout.BeginHorizontal();
                    GUInstallerUI.DrawBadge(exists ? "ĐÃ CÓ" : "CHƯA CÓ", exists ? GUInstallerUI.OkColor : GUInstallerUI.MissingColor, 76f);
                    GUILayout.Space(4f);
                    _customFolders[index] = EditorGUILayout.TextField(_customFolders[index]);
                    if (GUILayout.Button("X", GUILayout.Width(24f)))
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

                EditorGUILayout.Space(2f);
                EditorGUILayout.BeginHorizontal();
                if (GUInstallerUI.MiniButton("+ Thêm thư mục", true, 130f))
                {
                    _customFolders.Add("Assets/NewFolder");
                    SaveCustomFolders();
                }

                if (GUInstallerUI.MiniButton("Lưu danh sách", true, 120f))
                {
                    SaveCustomFolders();
                    ShowNotification(new GUIContent("Đã lưu danh sách thư mục tùy chỉnh."));
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawRequiredScriptableObjects()
        {
            var (_, _, assets, totalAssets) = GetSetupProgress();

            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader(
                    $"{assets}/{totalAssets}",
                    "ScriptableObject mặc định",
                    assets == totalAssets ? GUSetupState.Done : GUSetupState.Missing);

                for (int index = 0; index < RequiredScriptableObjects.Length; index++)
                {
                    var config = RequiredScriptableObjects[index];
                    bool exists = AssetDatabase.LoadAssetAtPath(config.AssetPath, config.AssetType) != null;
                    if (_showOnlyMissing && exists) continue;

                    if (GUInstallerUI.StatusRow(
                            Path.GetFileName(config.AssetPath),
                            exists ? GUSetupState.Done : GUSetupState.Missing,
                            Path.GetDirectoryName(config.AssetPath)?.Replace("\\", "/"),
                            exists ? "Mở" : "Tạo",
                            true,
                            60f))
                    {
                        if (exists)
                        {
                            GUInstallerUI.PingPath(config.AssetPath);
                        }
                        else
                        {
                            EnsureScriptableObjectAsset(config.AssetPath, config.AssetType);
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                        }
                    }
                }
            }
        }

        private void DrawActions()
        {
            bool completed = IsSetupCompleted();

            using (GUInstallerUI.BeginCard(0f))
            {
                if (completed)
                {
                    EditorGUILayout.HelpBox("Đủ thư mục và asset bắt buộc — các menu GameUp khác đã được mở khoá.", MessageType.Info);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUInstallerUI.PrimaryButton(completed ? "Tạo lại / kiểm tra tất cả" : "Tạo tất cả thư mục & asset", true, 32f))
                {
                    CreateAllFolders();
                    BuildRequiredTree();
                }

                if (GUInstallerUI.PrimaryButton("Mở Core Installer", true, 32f))
                {
                    EditorApplication.ExecuteMenuItem("GameUp/Project/GameUpCore Installer");
                }
                EditorGUILayout.EndHorizontal();
            }
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
