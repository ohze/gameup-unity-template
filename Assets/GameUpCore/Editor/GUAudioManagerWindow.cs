#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        private const string DefaultAudioFolderPath = "Assets/_MainProject/Audio";
        private const string DefaultAudioIdentityFolderPath = "Assets/_MainProject/Data/NoneSingleton/AudioIdentity";
        private const string DefaultAudioIdOutputPath = "Assets/_MainProject/Scripts/Audio/AudioID.cs";
        private const string DefaultAudioDatabaseFolderPath = "Assets/_MainProject/Data/Singletons";

        /// <summary> Tên group và label Addressables dùng cho audio (AudioIdentity + AudioClip). </summary>
        private const string AddressablesAudioIdentitiesGroupName = "Audio_Identities";
        private const string AddressablesAudioClipsGroupName = "Audio_Clips";
        private const string AddressablesAudioLabel = "Audio";

        private string audioFolderPath;
        private string audioIdentityFolderPath;
        private string audioIdOutputPath;
        private string audioDatabaseFolderPath;

        private const string MenuPath = "GameUp/Audio/Setup AudioManager";

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<GUAudioManagerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.Show();
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateShowWindow()
        {
            return GUDotweenDependencyUtility.CanUseCoreTools()
                   && GUProjectFolderSetupWindow.IsSetupCompleted();
        }

        private int _clipCount;
        private int _identityCount;

        private void OnEnable()
        {
            audioFolderPath = DefaultAudioFolderPath;
            audioIdentityFolderPath = DefaultAudioIdentityFolderPath;
            audioIdOutputPath = DefaultAudioIdOutputPath;
            audioDatabaseFolderPath = DefaultAudioDatabaseFolderPath;

            RefreshStats();
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        private void OnProjectChanged()
        {
            RefreshStats();
            Repaint();
        }

        private void OnFocus()
        {
            RefreshStats();
            Repaint();
        }

        /// <summary>Đếm clip/identity một lần khi project đổi thay vì quét lại mỗi lần vẽ GUI.</summary>
        private void RefreshStats()
        {
            _clipCount = CountAssets("t:AudioClip", audioFolderPath);
            _identityCount = CountAssets("t:AudioIdentity", audioIdentityFolderPath);
        }

        private static int CountAssets(string filter, string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder)) return 0;
            return AssetDatabase.FindAssets(filter, new[] { folder }).Length;
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


        private void OnGUI()
        {
            GUInstallerUI.EnsureStyles();

            var manager = FindObjectOfType<AudioManager>();
            var database = manager != null ? GetDatabase(manager) : null;
            var stage1Done = manager != null && database != null;

            DrawHeader(stage1Done);
            DrawStage1(manager, database, stage1Done);
            DrawStage2(manager, database, stage1Done);
        }

        private void DrawHeader(bool stage1Done)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(WindowTitle, EditorStyles.largeLabel);
            GUInstallerUI.Hint("Giai đoạn 1 chạy một lần cho mỗi project. Giai đoạn 2 chạy lại mỗi khi thêm/xoá file audio.");

            EditorGUILayout.Space(4);
            GUInstallerUI.ProgressBar("Giai đoạn", stage1Done ? 1 : 0, 2);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUInstallerUI.MiniButton("Kiểm tra lại", true, 100f))
            {
                AssetDatabase.Refresh();
                RefreshStats();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStage1(AudioManager manager, GameUp.Core.AudioDatabase database, bool stage1Done)
        {
            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader("GIAI ĐOẠN 1", "AudioManager + AudioDatabase",
                    stage1Done ? GUSetupState.Done : GUSetupState.Missing);
                GUILayout.Label(
                    "Tìm/tạo AudioManager trong scene, tạo AudioDatabase.asset và gán vào AudioManager.",
                    GUInstallerUI.Desc);

                EditorGUILayout.Space(4);

                if (GUInstallerUI.StatusRow(
                        "AudioManager trong scene",
                        manager != null ? GUSetupState.Done : GUSetupState.Missing,
                        manager != null ? manager.gameObject.name : "chưa có",
                        manager != null ? "Chọn" : "Core setup",
                        true,
                        manager != null ? 70f : 100f))
                {
                    if (manager != null)
                    {
                        Selection.activeGameObject = manager.gameObject;
                        EditorGUIUtility.PingObject(manager.gameObject);
                    }
                    else
                    {
                        FindOrCreateAudioManagerInScene();
                        GUIUtility.ExitGUI();
                    }
                }

                var databasePath = database != null ? AssetDatabase.GetAssetPath(database) : null;
                if (GUInstallerUI.StatusRow(
                        "AudioDatabase.asset",
                        database != null ? GUSetupState.Done : manager != null ? GUSetupState.Missing : GUSetupState.Blocked,
                        database != null ? databasePath : audioDatabaseFolderPath,
                        database != null ? "Mở" : "Tạo & gán",
                        manager != null,
                        database != null ? 70f : 100f))
                {
                    if (database != null)
                    {
                        GUInstallerUI.PingPath(databasePath);
                    }
                    else
                    {
                        InitializeDatabase(manager, audioDatabaseFolderPath);
                        GUIUtility.ExitGUI();
                    }
                }

                if (manager == null)
                {
                    GUInstallerUI.Hint("Chưa có AudioManager: bấm \"Core setup\" để copy prefab ====Manager==== vào scene rồi thử lại.");
                }
            }
        }

        private void DrawStage2(AudioManager manager, GameUp.Core.AudioDatabase database, bool stage1Done)
        {
            var audioIdExists = File.Exists(audioIdOutputPath);

            using (GUInstallerUI.BeginCard())
            {
                GUInstallerUI.CardHeader("GIAI ĐOẠN 2", "Scan & Update (Identity + Database + AudioID.cs)",
                    !stage1Done ? GUSetupState.Blocked : _identityCount > 0 ? GUSetupState.Done : GUSetupState.Missing);

                GUILayout.Label(
                    "Quét Audio Folder → tạo/cập nhật AudioIdentity, cập nhật AudioDatabase, sinh lại AudioID.cs " +
                    "và đưa identity/clip vào Addressables. Không chỉnh sửa scene.",
                    GUInstallerUI.Desc);

                EditorGUILayout.Space(4);

                var audioFolderOk = AssetDatabase.IsValidFolder(audioFolderPath);
                if (GUInstallerUI.StatusRow(
                        "Audio Folder",
                        audioFolderOk ? (_clipCount > 0 ? GUSetupState.Done : GUSetupState.Missing) : GUSetupState.Missing,
                        audioFolderOk ? $"{_clipCount} clip · {audioFolderPath}" : $"thiếu · {audioFolderPath}",
                        audioFolderOk ? "Mở" : null,
                        true,
                        60f))
                {
                    GUInstallerUI.PingPath(audioFolderPath);
                }

                var identityFolderOk = AssetDatabase.IsValidFolder(audioIdentityFolderPath);
                if (GUInstallerUI.StatusRow(
                        "Identity Folder",
                        identityFolderOk ? GUSetupState.Done : GUSetupState.Missing,
                        identityFolderOk ? $"{_identityCount} identity · {audioIdentityFolderPath}" : $"thiếu · {audioIdentityFolderPath}",
                        identityFolderOk ? "Mở" : null,
                        true,
                        60f))
                {
                    GUInstallerUI.PingPath(audioIdentityFolderPath);
                }

                if (GUInstallerUI.StatusRow(
                        "AudioID.cs",
                        audioIdExists ? GUSetupState.Done : GUSetupState.Missing,
                        audioIdOutputPath,
                        audioIdExists ? "Mở" : null,
                        true,
                        60f))
                {
                    GUInstallerUI.PingPath(audioIdOutputPath);
                }

                EditorGUILayout.Space(4);

                if (!stage1Done)
                {
                    GUInstallerUI.Hint("Hoàn tất giai đoạn 1 trước khi scan.");
                }
                else if (_clipCount == 0)
                {
                    GUInstallerUI.Hint($"Chưa có AudioClip nào trong {audioFolderPath} — thêm file audio rồi bấm Scan.");
                }

                if (GUInstallerUI.PrimaryButton("Scan & Update", stage1Done && _clipCount > 0, 32f))
                {
                    UpdateAudioData(manager, database);
                    RefreshStats();
                }
            }
        }

        private void UpdateAudioData(AudioManager manager, GameUp.Core.AudioDatabase database)
        {
            if (!manager || !database)
            {
                GULogger.Error("AudioManager", "Không tìm thấy AudioManager hoặc AudioDatabase.");
                return;
            }

            if (string.IsNullOrWhiteSpace(audioFolderPath))
            {
                GULogger.Error("AudioManager", "Audio Folder đang trống.");
                return;
            }

            if (string.IsNullOrWhiteSpace(audioIdentityFolderPath))
            {
                GULogger.Error("AudioManager", "Identity Folder đang trống.");
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
                GULogger.Warning("AudioManager", $"Không tìm thấy AudioClip nào trong folder: {audioFolderPath}");
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
            var identityNameCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var currentIdentityPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var identityPathsForAddressables = new List<string>();
            var clipPathsForAddressables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in clipGroups)
            {
                var (relativeFolder, sanitizedName) = kvp.Key;
                if (string.IsNullOrEmpty(sanitizedName))
                    sanitizedName = "Unnamed";

                // Theo yêu cầu chỉ lấy tên file làm identity name (không ghép tên folder).
                // Nếu trùng tên giữa nhiều folder, thêm hậu tố _2, _3... để giữ unique key.
                var identityName = sanitizedName;
                if (identityNameCounters.TryGetValue(sanitizedName, out var currentCount))
                {
                    currentCount++;
                    identityNameCounters[sanitizedName] = currentCount;
                    identityName = $"{sanitizedName}_{currentCount}";
                }
                else
                {
                    identityNameCounters[sanitizedName] = 1;
                }

                // Unity cảnh báo nếu main object name != asset filename.
                // Vì identity.name có thể include hậu tố để unique, ta đồng bộ filename theo identityName.
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
                            GULogger.Warning("AudioManager", $"Không thể rename/move AudioIdentity từ \"{legacyIdentityAssetPath}\" sang \"{desiredIdentityAssetPath}\": {moveErr}");
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
                GULogger.Error("AudioManager", "Không tìm thấy AddressableAssetSettings. Hãy cài/thiết lập package Addressables (Window > Asset Management > Addressables > Groups).");
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
                    GULogger.Error("AudioManager", $"Không tạo được Addressables group \"{groupName}\".");
                    return null;
                }

                GULogger.Log("AudioManager", $"Đã tạo Addressables group \"{groupName}\".");
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
                GULogger.Log("AudioManager", $"Đã remove {toRemove.Count} AudioClip không còn tồn tại khỏi Addressables group \"{AddressablesAudioClipsGroupName}\".");
            }

            if (processed > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, settings, true, false);
                GULogger.Log("AudioManager", $"Đã thêm/cập nhật {processed} asset vào Addressables (groups \"{AddressablesAudioIdentitiesGroupName}\" & \"{AddressablesAudioClipsGroupName}\", label \"{AddressablesAudioLabel}\").");
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
                GULogger.Log("AudioManager", $"Đã xóa AudioIdentity thừa: {path}");
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
                GULogger.Error("AudioManager", $"AudioID output path phải nằm dưới 'Assets/'. Hiện tại: {outputPath}");
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
                    GULogger.Log("AudioManager", $"Đã xóa file AudioID cũ: {path}");
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
            GULogger.Log("AudioManager", $"Đã sinh lại file AudioID.cs với {ordered.Count} entries tại: {outputPath}");
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
            if (GUCoreProjectSetup.EnsureAudioManagerInScene(log: true))
                return FindObjectOfType<AudioManager>();

            GULogger.Error("AudioManager", "Core setup không tạo được AudioManager trên scene. Kiểm tra prefab ====Manager==== trong Assets/_MainProject/Prefabs/Core.");
            return null;
        }

        private static void InitializeDatabase(AudioManager manager, string databaseFolder)
        {
            if (!manager)
            {
                GULogger.Error("AudioManager", "Không tìm thấy AudioManager.");
                return;
            }

            if (string.IsNullOrWhiteSpace(databaseFolder))
            {
                databaseFolder = DefaultAudioDatabaseFolderPath;
            }

            databaseFolder = databaseFolder.Trim().Replace("\\", "/").TrimEnd('/');
            if (!databaseFolder.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                GULogger.Error("AudioManager", $"Database Path phải nằm dưới 'Assets/'. Hiện tại: {databaseFolder}");
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
                GULogger.Log("AudioManager", $"Đã tạo AudioDatabase asset tại: {assetPath}");
            }

            var so = new SerializedObject(manager);
            var dbProp = so.FindProperty("database");
            if (dbProp == null)
            {
                GULogger.Error("AudioManager", "Không tìm thấy serialized field 'database' trên AudioManager.");
                return;
            }

            dbProp.objectReferenceValue = database;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);

            var scene = SceneManager.GetActiveScene();
            if (!scene.isDirty)
                EditorSceneManager.MarkSceneDirty(scene);

            GULogger.Log("AudioManager", "Đã gán AudioDatabase vào AudioManager (Stage 1).");
        }
    }
}
#endif

