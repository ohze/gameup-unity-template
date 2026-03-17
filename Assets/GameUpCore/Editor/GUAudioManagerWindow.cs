#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GameUp.Core;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameUp.Core.Editor
{
    public class GUAudioManagerWindow : EditorWindow
    {
        private const string WindowTitle = "GameUp Audio Setup";
        private const string PrefsKeyAudioFolder = "GameUp.Audio.FolderPath";
        private const string PrefsKeyAudioIdentityFolder = "GameUp.Audio.IdentityFolderPath";
        private const string PrefsKeyAudioIdOutputPath = "GameUp.Audio.AudioIDOutputPath";
        private const string PrefsKeyAudioDatabaseFolder = "GameUp.Audio.DatabaseFolderPath";

        /// <summary> Tên group và label Addressables dùng cho audio (AudioIdentity + AudioClip). </summary>
        private const string AddressablesAudioIdentitiesGroupName = "Audio_Identities";
        private const string AddressablesAudioClipsGroupName = "Audio_Clips";
        private const string AddressablesAudioLabel = "Audio";

        private string audioFolderPath;
        private string audioIdentityFolderPath;
        private string audioIdOutputPath;
        private string audioDatabaseFolderPath;

        [MenuItem("GameUp/Audio/Setup AudioManager")]
        public static void ShowWindow()
        {
            var window = GetWindow<GUAudioManagerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.Show();
        }

        private void OnEnable()
        {
            audioFolderPath = EditorPrefs.GetString(PrefsKeyAudioFolder,
                "Games/Addressables/Sounds");
            audioIdentityFolderPath = EditorPrefs.GetString(PrefsKeyAudioIdentityFolder,
                "Assets/GameData/Resources/AudioIdentities");
            audioIdOutputPath = EditorPrefs.GetString(PrefsKeyAudioIdOutputPath,
                "Assets/AudioID.cs");
            audioDatabaseFolderPath = EditorPrefs.GetString(PrefsKeyAudioDatabaseFolder,
                "Assets/GameData/Resources/");
        }

        private static string ToProjectRelativePath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return absolutePath;
            var normalized = absolutePath.Replace("\\", "/").TrimEnd('/');
            var dataPath = Application.dataPath.Replace("\\", "/");
            if (normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                var sub = normalized.Length == dataPath.Length ? "" : normalized.Substring(dataPath.Length).TrimStart('/');
                return string.IsNullOrEmpty(sub) ? "Assets" : $"Assets/{sub}";
            }
            var projectRoot = Path.GetDirectoryName(dataPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(projectRoot) && normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                var sub = normalized.Substring(projectRoot.Length).TrimStart('/');
                return string.IsNullOrEmpty(sub) ? "Assets" : sub;
            }
            return absolutePath;
        }

        /// <summary> Lấy thư mục tương đối của clip so với searchFolder (vd: "hero" từ "Assets/.../Sounds/hero/attack.wav"). </summary>
        private static string GetRelativeFolderFromClipPath(string clipPath, string searchFolder)
        {
            if (string.IsNullOrEmpty(clipPath) || string.IsNullOrEmpty(searchFolder)) return "";
            var normalizedClip = clipPath.Replace("\\", "/");
            var prefix = "Assets/" + searchFolder.Trim('/').Replace("\\", "/");
            if (!normalizedClip.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return "";
            var relative = normalizedClip.Substring(prefix.Length).TrimStart('/');
            var dir = Path.GetDirectoryName(relative)?.Replace("\\", "/");
            return string.IsNullOrEmpty(dir) ? "" : dir;
        }

        private static string FolderToNamePart(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return "";
            return folder.Replace(" ", "_").Replace("-", "_").Replace("/", "_").Trim('_');
        }

        private void DrawPathField(string label, ref string path, string prefsKey, bool isFolder, string defaultFileName = null)
        {
            EditorGUILayout.BeginHorizontal();
            path = EditorGUILayout.TextField(label, path);
            if (GUILayout.Button("Browse", GUILayout.Width(64)))
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? "";
                string startDir = Application.dataPath;
                if (!string.IsNullOrEmpty(path))
                {
                    var dir = path.Replace("\\", "/");
                    var relDir = isFolder ? dir : (Path.GetDirectoryName(dir) ?? dir).Replace("\\", "/");
                    if (relDir.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || relDir.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                    {
                        var full = Path.Combine(projectRoot, relDir);
                        if (Directory.Exists(full))
                            startDir = full;
                    }
                }
                startDir = startDir.Replace("\\", "/");

                string chosen;
                if (isFolder)
                {
                    chosen = EditorUtility.OpenFolderPanel("Chọn thư mục", startDir, "");
                }
                else
                {
                    chosen = EditorUtility.SaveFilePanel("Chọn nơi lưu AudioID.cs", startDir, defaultFileName ?? "AudioID.cs", "cs");
                }
                if (!string.IsNullOrEmpty(chosen))
                {
                    path = ToProjectRelativePath(chosen);
                    if (!isFolder && !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(defaultFileName))
                        path = path.TrimEnd('/') + "/" + defaultFileName;
                    path = path.Replace("\\", "/");
                    EditorPrefs.SetString(prefsKey, path);
                    Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(WindowTitle, EditorStyles.boldLabel);
            EditorGUILayout.Space();

            var manager = FindObjectOfType<AudioManager>();
            var hasDatabase = manager != null && HasDatabaseAssigned(manager);

            if (!hasDatabase)
            {
                DrawInitialSetup(manager);
                return;
            }

            DrawGenerationSettings(manager);
        }

        private void DrawInitialSetup(AudioManager manager)
        {
            EditorGUILayout.HelpBox(
                "GIAI ĐOẠN 1/2 — Initial Setup\n" +
                "Bước này chỉ tạo/tìm AudioManager và khởi tạo AudioDatabase.asset rồi gán vào AudioManager trong Scene.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("AudioManager", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Find/Create AudioManager", GUILayout.Height(26)))
            {
                manager = FindOrCreateAudioManagerInScene();
            }

            using (new EditorGUI.DisabledScope(manager == null))
            {
                if (GUILayout.Button("Select", GUILayout.Height(26), GUILayout.Width(80)))
                {
                    Selection.activeGameObject = manager.gameObject;
                    EditorGUIUtility.PingObject(manager.gameObject);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (manager == null)
            {
                EditorGUILayout.HelpBox("Chưa có AudioManager trong Scene. Hãy bấm \"Find/Create AudioManager\" trước.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox($"Đã tìm thấy AudioManager trên \"{manager.gameObject.name}\".", MessageType.None);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Audio Database", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            DrawPathField("Database Path (folder under Assets)", ref audioDatabaseFolderPath, PrefsKeyAudioDatabaseFolder, isFolder: true);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(PrefsKeyAudioDatabaseFolder, audioDatabaseFolderPath);
            }

            using (new EditorGUI.DisabledScope(manager == null))
            {
                if (GUILayout.Button("Initialize Audio Database", GUILayout.Height(30)))
                {
                    InitializeDatabase(manager, audioDatabaseFolderPath);
                }
            }
        }

        private void DrawGenerationSettings(AudioManager manager)
        {
            var database = GetDatabase(manager);
            if (!database)
                return;

            EditorGUILayout.HelpBox(
                "GIAI ĐOẠN 2/2 — Generation Settings\n" +
                "Bước này chỉ cập nhật AudioIdentity assets + AudioDatabase.asset + AudioID.cs.\n" +
                "Lưu ý: Stage 2 KHÔNG chỉnh sửa AudioManager trong Scene để tránh dirty Scene/Prefab không cần thiết.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);

            if (string.IsNullOrEmpty(audioFolderPath))
            {
                // keep UI consistent; validation happens on button click
            }

            if (string.IsNullOrEmpty(audioIdentityFolderPath))
            {
                // keep UI consistent; validation happens on button click
            }

            EditorGUI.BeginChangeCheck();
            DrawPathField("Audio Folder", ref audioFolderPath, PrefsKeyAudioFolder, isFolder: true);
            DrawPathField("Identity Folder", ref audioIdentityFolderPath, PrefsKeyAudioIdentityFolder, isFolder: true);
            DrawPathField("AudioID Output Path", ref audioIdOutputPath, PrefsKeyAudioIdOutputPath, isFolder: false, defaultFileName: "AudioID.cs");
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(PrefsKeyAudioFolder, audioFolderPath);
                EditorPrefs.SetString(PrefsKeyAudioIdentityFolder, audioIdentityFolderPath);
                EditorPrefs.SetString(PrefsKeyAudioIdOutputPath, audioIdOutputPath);
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Scan & Update sẽ:\n" +
                "- Quét Audio Folder và tạo/cập nhật AudioIdentity assets\n" +
                "- Cập nhật danh sách vào AudioDatabase.asset (dirty asset, KHÔNG dirty Scene)\n" +
                "- Sinh lại AudioID.cs\n" +
                "- Tự động thêm AudioIdentity + AudioClip vào Addressables (group/label \"Audio\") nếu Addressables đã được setup.",
                MessageType.None);

            if (GUILayout.Button("Scan & Update", GUILayout.Height(32)))
            {
                UpdateAudioData(manager, database);
            }
        }

        private void UpdateAudioData(AudioManager manager, GameUp.Core.AudioDatabase database)
        {
            if (!manager || !database)
            {
                Debug.LogError("[AudioManager] Không tìm thấy AudioManager hoặc AudioDatabase.");
                return;
            }

            if (string.IsNullOrWhiteSpace(audioFolderPath))
            {
                Debug.LogError("[AudioManager] Audio Folder đang trống.");
                return;
            }

            if (string.IsNullOrWhiteSpace(audioIdentityFolderPath))
            {
                Debug.LogError("[AudioManager] Identity Folder đang trống.");
                return;
            }

            // Chuẩn hoá path: cho phép nhập kèm "Assets/..."
            var searchFolder = audioFolderPath.Trim();
            if (searchFolder.StartsWith("Assets/") || searchFolder.StartsWith("Assets\\"))
            {
                searchFolder = searchFolder.Substring("Assets/".Length).TrimStart('/', '\\');
            }

            // Load tất cả AudioClip trong folder (project-relative, không kèm "Assets/")
            var clips = GameUtils.GetAssetList<AudioClip>(searchFolder);
            if (clips == null || clips.Count == 0)
            {
                Debug.LogWarning($"[AudioManager] Không tìm thấy AudioClip nào trong folder: {audioFolderPath}");
                return;
            }

            string SanitizeNamePart(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return "";
                var s = raw.Trim();
                s = Regex.Replace(s, @"\s+", "_");
                s = s.Replace("-", "_");
                s = Regex.Replace(s, @"[^A-Za-z0-9_]+", "_");
                s = Regex.Replace(s, @"_+", "_").Trim('_');
                return s;
            }

            // Base name: "Attack 1", "Attack 2", "Attack" -> "Attack" (gộp variant vào một identity)
            string GetBaseName(string clipName)
            {
                if (string.IsNullOrWhiteSpace(clipName)) return clipName ?? "";
                var trimmed = clipName.Trim();
                var match = Regex.Match(trimmed, @"^(.+?)\s+\d+$");
                return match.Success ? match.Groups[1].Value.Trim() : trimmed;
            }

            // Sắp xếp clip trong group: tên trùng baseName trước, còn lại theo số hậu tố (1, 2, 3...)
            int ClipOrder(AudioClip c, string baseName)
            {
                var name = c.name.Trim();
                if (string.Equals(name, baseName, StringComparison.OrdinalIgnoreCase))
                    return 0;
                var m = Regex.Match(name, @"\s+(\d+)$");
                return m.Success ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : int.MaxValue;
            }

            // Group theo (relativeFolder, baseName) để Attack, Attack 1, Attack 2... cùng một identity
            var clipGroups = clips
                .GroupBy(c =>
                {
                    var clipPath = AssetDatabase.GetAssetPath(c);
                    var relFolder = GetRelativeFolderFromClipPath(clipPath, searchFolder);
                    var baseName = GetBaseName(c.name);
                    return (relFolder, SanitizeNamePart(baseName));
                })
                .ToDictionary(g => g.Key, g =>
                {
                    var baseName = GetBaseName(g.First().name).Trim();
                    return g.OrderBy(c => ClipOrder(c, baseName)).ToList();
                });

            var identityFolderNormalized = audioIdentityFolderPath.Replace("\\", "/").TrimEnd('/');
            if (!Directory.Exists(identityFolderNormalized))
                Directory.CreateDirectory(identityFolderNormalized);

            var identityGuids = new List<(string name, string guid)>();
            var identityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var currentIdentityPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var identityPathsForAddressables = new List<string>();
            var clipPathsForAddressables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in clipGroups)
            {
                var (relativeFolder, sanitizedName) = kvp.Key;
                var folderPart = FolderToNamePart(relativeFolder);
                folderPart = SanitizeNamePart(folderPart);
                if (string.IsNullOrEmpty(sanitizedName))
                    sanitizedName = "Unnamed";
                var identityName = string.IsNullOrEmpty(folderPart) ? sanitizedName : $"{folderPart}_{sanitizedName}";

                // Unity cảnh báo nếu main object name != asset filename.
                // Vì identity.name có thể include folderPart để unique, ta đồng bộ filename theo identityName.
                var identitySubPath = string.IsNullOrEmpty(relativeFolder)
                    ? $"{identityName}.asset"
                    : $"{relativeFolder.Replace('\\', '/')}/{identityName}.asset";
                var desiredIdentityAssetPath = $"{identityFolderNormalized}/{identitySubPath}".Replace("\\", "/");

                // Backward-compat: file cũ theo sanitizedName (không có folderPart) -> move sang desired path nếu có.
                var legacySubPath = string.IsNullOrEmpty(relativeFolder)
                    ? $"{sanitizedName}.asset"
                    : $"{relativeFolder.Replace('\\', '/')}/{sanitizedName}.asset";
                var legacyIdentityAssetPath = $"{identityFolderNormalized}/{legacySubPath}".Replace("\\", "/");

                var identityDir = Path.GetDirectoryName(desiredIdentityAssetPath)?.Replace("\\", "/");
                if (!string.IsNullOrEmpty(identityDir) && !Directory.Exists(identityDir))
                    Directory.CreateDirectory(identityDir);

                var identity = AssetDatabase.LoadAssetAtPath<AudioIdentity>(desiredIdentityAssetPath);
                if (!identity && !string.Equals(legacyIdentityAssetPath, desiredIdentityAssetPath, StringComparison.OrdinalIgnoreCase))
                {
                    identity = AssetDatabase.LoadAssetAtPath<AudioIdentity>(legacyIdentityAssetPath);
                    if (identity)
                    {
                        // Move/rename legacy asset để filename khớp identity.name
                        var moveErr = AssetDatabase.MoveAsset(legacyIdentityAssetPath, desiredIdentityAssetPath);
                        if (!string.IsNullOrEmpty(moveErr))
                        {
                            Debug.LogWarning($"[AudioManager] Không thể rename/move AudioIdentity từ \"{legacyIdentityAssetPath}\" sang \"{desiredIdentityAssetPath}\": {moveErr}");
                            // fallback: vẫn dùng legacy path
                            desiredIdentityAssetPath = legacyIdentityAssetPath;
                        }
                    }
                }

                identity = AssetDatabase.LoadAssetAtPath<AudioIdentity>(desiredIdentityAssetPath);
                if (!identity)
                {
                    identity = ScriptableObject.CreateInstance<AudioIdentity>();
                    identity.name = identityName;
                    AssetDatabase.CreateAsset(identity, desiredIdentityAssetPath);
                }
                else
                {
                    identity.name = identityName;
                    EditorUtility.SetDirty(identity);
                }

                identity.clipRefs.Clear();
                foreach (var clip in kvp.Value)
                {
                    var clipPath = AssetDatabase.GetAssetPath(clip);
                    if (!string.IsNullOrEmpty(clipPath))
                        clipPathsForAddressables.Add(clipPath.Replace("\\", "/"));
                    var clipGuid = AssetDatabase.AssetPathToGUID(clipPath);
                    if (!string.IsNullOrEmpty(clipGuid))
                        identity.clipRefs.Add(new AudioClipReference(clipGuid));
                }
                if (identity.clipRefs.Count > 0)
                    EditorUtility.SetDirty(identity);

                var identityGuid = AssetDatabase.AssetPathToGUID(desiredIdentityAssetPath);
                if (!string.IsNullOrEmpty(identityGuid))
                {
                    identityGuids.Add((identityName, identityGuid));
                    identityNames.Add(identityName);
                    var identityPathNorm = desiredIdentityAssetPath.Replace("\\", "/");
                    currentIdentityPaths.Add(identityPathNorm);
                    identityPathsForAddressables.Add(identityPathNorm);
                }
            }

            // Xóa các AudioIdentity nằm trong thư mục identity nhưng không còn tương ứng clip nào (thừa)
            RemoveOrphanAudioIdentities(identityFolderNormalized, currentIdentityPaths);

            AssetDatabase.SaveAssets();

            UpdateDatabaseAsset(database, identityGuids);

            var databaseAssetPath = AssetDatabase.GetAssetPath(database);
            EnsureAudioInAddressables(identityPathsForAddressables, clipPathsForAddressables, databaseAssetPath, searchFolder);

            GenerateAudioIdClass(identityNames, audioIdOutputPath);
        }

        /// <summary>
        /// Đưa AudioIdentity/AudioDatabase vào group "Audio_Identities" và AudioClip vào group "Audio_Clips", label "Audio".
        /// Đồng thời dọn các entry AudioClip không còn tồn tại trong folder nguồn để tránh rác dữ liệu.
        /// Yêu cầu đã cài package Addressables. AssetReferenceT (AudioClipReference, AudioIdentityReference) sẽ hoạt động mà không cần setup thủ công.
        /// </summary>
        private static void EnsureAudioInAddressables(
            List<string> identityAssetPaths,
            HashSet<string> clipAssetPaths,
            string databaseAssetPath,
            string searchFolder)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
            {
                settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            }

            if (settings == null)
            {
                Debug.LogError("[AudioManager] Không tìm thấy AddressableAssetSettings. Hãy cài/thiết lập package Addressables (Window > Asset Management > Addressables > Groups).");
                return;
            }

            // Đảm bảo label "Audio" tồn tại
            var labels = settings.GetLabels();
            if (labels == null || !labels.Contains(AddressablesAudioLabel))
            {
                settings.AddLabel(AddressablesAudioLabel, false);
            }

            AddressableAssetGroup GetOrCreateGroup(string groupName)
            {
                var group = settings.FindGroup(groupName);
                if (group != null) return group;

                group = settings.CreateGroup(
                    groupName,
                    false,
                    false,
                    false,
                    null,
                    typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema));

                if (group == null)
                {
                    Debug.LogError($"[AudioManager] Không tạo được Addressables group \"{groupName}\".");
                    return null;
                }

                Debug.Log($"[AudioManager] Đã tạo Addressables group \"{groupName}\".");
                return group;
            }

            var identitiesGroup = GetOrCreateGroup(AddressablesAudioIdentitiesGroupName);
            if (identitiesGroup == null) return;

            var clipsGroup = GetOrCreateGroup(AddressablesAudioClipsGroupName);
            if (clipsGroup == null) return;

            var processed = 0;

            // Ensure AudioDatabase.asset cũng nằm trong group identities
            if (!string.IsNullOrEmpty(databaseAssetPath))
            {
                databaseAssetPath = databaseAssetPath.Replace("\\", "/");
                var dbGuid = AssetDatabase.AssetPathToGUID(databaseAssetPath);
                if (!string.IsNullOrEmpty(dbGuid))
                {
                    var dbEntry = settings.CreateOrMoveEntry(dbGuid, identitiesGroup, false, true);
                    if (dbEntry != null)
                    {
                        dbEntry.SetLabel(AddressablesAudioLabel, true, true, true);
                        processed++;
                    }
                }
            }

            foreach (var path in identityAssetPaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) continue;
                var entry = settings.CreateOrMoveEntry(guid, identitiesGroup, false, true);
                if (entry != null)
                {
                    entry.SetLabel(AddressablesAudioLabel, true, true, true);
                    processed++;
                }
            }

            foreach (var path in clipAssetPaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) continue;
                var entry = settings.CreateOrMoveEntry(guid, clipsGroup, false, true);
                if (entry != null)
                {
                    entry.SetLabel(AddressablesAudioLabel, true, true, true);
                    processed++;
                }
            }

            // Clean up: remove clip entries không còn tồn tại trong folder nguồn
            var sourcePrefix = "Assets/" + (searchFolder ?? "").Trim('/').Replace("\\", "/");
            bool IsAudioFilePath(string p)
            {
                if (string.IsNullOrEmpty(p)) return false;
                var ext = Path.GetExtension(p).ToLowerInvariant();
                return ext == ".mp3" || ext == ".wav" || ext == ".ogg" || ext == ".m4a" || ext == ".aiff" || ext == ".aif";
            }

            var toRemove = new List<AddressableAssetEntry>();
            if (clipsGroup.entries != null)
            {
                foreach (var e in clipsGroup.entries)
                {
                    if (e == null) continue;
                    var p = AssetDatabase.GUIDToAssetPath(e.guid);
                    p = p?.Replace("\\", "/");
                    if (string.IsNullOrEmpty(p)) { toRemove.Add(e); continue; }
                    if (!IsAudioFilePath(p)) continue;
                    if (!string.IsNullOrEmpty(searchFolder) && !p.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Nếu clip không còn trong kết quả scan hoặc đã mất asset -> remove khỏi group
                    if (!clipAssetPaths.Contains(p) || !AssetDatabase.LoadAssetAtPath<AudioClip>(p))
                        toRemove.Add(e);
                }
            }

            if (toRemove.Count > 0)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    clipsGroup.RemoveAssetEntry(toRemove[i]);
                }
                processed += toRemove.Count;
                Debug.Log($"[AudioManager] Đã remove {toRemove.Count} AudioClip không còn tồn tại khỏi Addressables group \"{AddressablesAudioClipsGroupName}\".");
            }

            if (processed > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, settings, true, false);
                Debug.Log($"[AudioManager] Đã thêm/cập nhật {processed} asset vào Addressables (groups \"{AddressablesAudioIdentitiesGroupName}\" & \"{AddressablesAudioClipsGroupName}\", label \"{AddressablesAudioLabel}\").");
            }
        }

        private static void RemoveOrphanAudioIdentities(string identityFolderPath, HashSet<string> currentIdentityPaths)
        {
            var folderNorm = identityFolderPath.Replace("\\", "/").TrimEnd('/');
            var guids = AssetDatabase.FindAssets("t:AudioIdentity", new[] { folderNorm });
            var toDelete = new List<string>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var pathNorm = path.Replace("\\", "/");
                if (!currentIdentityPaths.Contains(pathNorm))
                    toDelete.Add(path);
            }
            foreach (var path in toDelete)
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[AudioManager] Đã xóa AudioIdentity thừa: {path}");
            }
        }

        private static void UpdateDatabaseAsset(GameUp.Core.AudioDatabase database, List<(string name, string guid)> identityGuids)
        {
            if (!database) return;
            if (identityGuids == null) return;

            database.identityReferences.Clear();
            for (int i = 0; i < identityGuids.Count; i++)
            {
                var guid = identityGuids[i].guid;
                if (string.IsNullOrEmpty(guid)) continue;
                database.identityReferences.Add(new AudioIdentityReference(guid));
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        private static void GenerateAudioIdClass(HashSet<string> identityNames, string outputPath)
        {
            if (identityNames == null || identityNames.Count == 0)
                return;

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = "Assets/AudioID.cs";
            }

            outputPath = outputPath.Trim();
            outputPath = outputPath.Replace("\\", "/");

            // Cho phép nhập folder (vd: Assets/Game/Test) hoặc file .cs (vd: Assets/Game/Test/AudioID.cs)
            if (outputPath.EndsWith("/", StringComparison.Ordinal))
            {
                outputPath = outputPath.TrimEnd('/');
            }

            var isCsFile = outputPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
            if (!isCsFile)
            {
                outputPath = $"{outputPath}/AudioID.cs";
                isCsFile = true;
            }

            if (!outputPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && !outputPath.Equals("Assets", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"[AudioManager] AudioID output path phải nằm dưới 'Assets/'. Hiện tại: {outputPath}");
                return;
            }

            // Xóa mọi file AudioID.cs đã tồn tại trong project (bất kể đường dẫn) để chỉ còn một file tại output
            var guids = AssetDatabase.FindAssets("AudioID", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("AudioID.cs", StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"[AudioManager] Đã xóa file AudioID cũ: {path}");
                }
            }

            var ordered = identityNames
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            static string SanitizeIdentifier(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return "_";
                var s = raw.Trim();
                s = Regex.Replace(s, @"\s+", "_");
                s = Regex.Replace(s, @"[^A-Za-z0-9_]+", "_");
                s = Regex.Replace(s, @"_+", "_");
                if (string.IsNullOrEmpty(s)) s = "_";
                if (char.IsDigit(s[0])) s = "_" + s;

                switch (s)
                {
                    case "abstract":
                    case "as":
                    case "base":
                    case "bool":
                    case "break":
                    case "byte":
                    case "case":
                    case "catch":
                    case "char":
                    case "checked":
                    case "class":
                    case "const":
                    case "continue":
                    case "decimal":
                    case "default":
                    case "delegate":
                    case "do":
                    case "double":
                    case "else":
                    case "enum":
                    case "event":
                    case "explicit":
                    case "extern":
                    case "false":
                    case "finally":
                    case "fixed":
                    case "float":
                    case "for":
                    case "foreach":
                    case "goto":
                    case "if":
                    case "implicit":
                    case "in":
                    case "int":
                    case "interface":
                    case "internal":
                    case "is":
                    case "lock":
                    case "long":
                    case "namespace":
                    case "new":
                    case "null":
                    case "object":
                    case "operator":
                    case "out":
                    case "override":
                    case "params":
                    case "private":
                    case "protected":
                    case "public":
                    case "readonly":
                    case "ref":
                    case "return":
                    case "sbyte":
                    case "sealed":
                    case "short":
                    case "sizeof":
                    case "stackalloc":
                    case "static":
                    case "string":
                    case "struct":
                    case "switch":
                    case "this":
                    case "throw":
                    case "true":
                    case "try":
                    case "typeof":
                    case "uint":
                    case "ulong":
                    case "unchecked":
                    case "unsafe":
                    case "ushort":
                    case "using":
                    case "virtual":
                    case "void":
                    case "volatile":
                    case "while":
                        return "_" + s;
                    default:
                        return s;
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("public static class AudioID");
            sb.AppendLine("{");
            sb.AppendLine("    private static GameUp.Core.AudioIdentity Get(string name)");
            sb.AppendLine("    {");
            sb.AppendLine("        return GameUp.Core.AudioManager.TryGetIdentity(name, out var identity) ? identity : null;");
            sb.AppendLine("    }");
            sb.AppendLine();

            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var identityName in ordered)
            {
                var prop = SanitizeIdentifier(identityName);
                var unique = prop;
                var suffix = 2;
                while (!used.Add(unique))
                {
                    unique = $"{prop}_{suffix}";
                    suffix++;
                }

                sb.AppendLine($"    public static GameUp.Core.AudioIdentity {unique} => Get(\"{identityName}\");");
            }

            sb.AppendLine("}");

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(outputPath, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[AudioManager] Đã sinh lại file AudioID.cs với {ordered.Count} entries tại: {outputPath}");
        }

        private static bool HasDatabaseAssigned(AudioManager manager)
        {
            if (!manager) return false;
            var so = new SerializedObject(manager);
            var dbProp = so.FindProperty("database");
            return dbProp != null && dbProp.objectReferenceValue != null;
        }

        private static GameUp.Core.AudioDatabase GetDatabase(AudioManager manager)
        {
            if (!manager) return null;
            var so = new SerializedObject(manager);
            var dbProp = so.FindProperty("database");
            return dbProp != null ? dbProp.objectReferenceValue as GameUp.Core.AudioDatabase : null;
        }

        private static AudioManager FindOrCreateAudioManagerInScene()
        {
            var existing = FindObjectOfType<AudioManager>();
            if (existing) return existing;

            var go = new GameObject("AudioManager");
            var created = go.AddComponent<AudioManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create AudioManager");

            var scene = SceneManager.GetActiveScene();
            if (!scene.isDirty)
                EditorSceneManager.MarkSceneDirty(scene);

            Selection.activeGameObject = go;
            Debug.Log("[AudioManager] Đã tạo GameObject 'AudioManager' và gắn component.");
            return created;
        }

        private static void InitializeDatabase(AudioManager manager, string databaseFolder)
        {
            if (!manager)
            {
                Debug.LogError("[AudioManager] Không tìm thấy AudioManager.");
                return;
            }

            if (string.IsNullOrWhiteSpace(databaseFolder))
            {
                databaseFolder = "Assets/GameData/Resources/";
            }

            databaseFolder = databaseFolder.Trim().Replace("\\", "/").TrimEnd('/');
            if (!databaseFolder.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"[AudioManager] Database Path phải nằm dưới 'Assets/'. Hiện tại: {databaseFolder}");
                return;
            }

            if (!AssetDatabase.IsValidFolder(databaseFolder))
            {
                var parent = Path.GetDirectoryName(databaseFolder)?.Replace("\\", "/");
                var name = Path.GetFileName(databaseFolder);
                if (!string.IsNullOrEmpty(parent) && AssetDatabase.IsValidFolder(parent))
                {
                    AssetDatabase.CreateFolder(parent, name);
                }
                else
                {
                    Directory.CreateDirectory(databaseFolder);
                    AssetDatabase.Refresh();
                }
            }

            var assetPath = $"{databaseFolder}/AudioDatabase.asset";
            var database = AssetDatabase.LoadAssetAtPath<GameUp.Core.AudioDatabase>(assetPath);
            if (!database)
            {
                database = ScriptableObject.CreateInstance<GameUp.Core.AudioDatabase>();
                AssetDatabase.CreateAsset(database, assetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[AudioManager] Đã tạo AudioDatabase asset tại: {assetPath}");
            }

            var so = new SerializedObject(manager);
            var dbProp = so.FindProperty("database");
            if (dbProp == null)
            {
                Debug.LogError("[AudioManager] Không tìm thấy serialized field 'database' trên AudioManager.");
                return;
            }

            dbProp.objectReferenceValue = database;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);

            var scene = SceneManager.GetActiveScene();
            if (!scene.isDirty)
                EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[AudioManager] Đã gán AudioDatabase vào AudioManager (Stage 1).");
        }
    }
}
#endif

