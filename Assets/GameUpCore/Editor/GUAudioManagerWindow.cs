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

        /// <summary> Tên group và label Addressables dùng cho audio (AudioIdentity + AudioClip). </summary>
        private const string AddressablesAudioGroupName = "Audio";
        private const string AddressablesAudioLabel = "Audio";

        private string audioFolderPath;
        private string audioIdentityFolderPath;
        private string audioIdOutputPath;

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
            if (GUILayout.Button("...", GUILayout.Width(24)))
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
            EditorGUILayout.LabelField("Audio Manager Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawAudioManagerSection();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            DrawPathField("Audio folder (project-relative)", ref audioFolderPath, PrefsKeyAudioFolder, isFolder: true, defaultFileName: null);
            DrawPathField("AudioIdentity Resources folder (under Assets)", ref audioIdentityFolderPath, PrefsKeyAudioIdentityFolder, isFolder: true, defaultFileName: null);
            DrawPathField("AudioID output path (folder or .cs file)", ref audioIdOutputPath, PrefsKeyAudioIdOutputPath, isFolder: false, defaultFileName: "AudioID.cs");
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(PrefsKeyAudioFolder, audioFolderPath);
                EditorPrefs.SetString(PrefsKeyAudioIdentityFolder, audioIdentityFolderPath);
                EditorPrefs.SetString(PrefsKeyAudioIdOutputPath, audioIdOutputPath);
            }

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Scan sẽ tạo/cập nhật AudioIdentity, gán clip, sinh AudioID.cs và tự động thêm tất cả asset vào Addressables (group + label \"Audio\") nếu đã cài package Addressables — dùng được AssetReferenceT mà không cần setup thủ công.",
                MessageType.None);

            if (GUILayout.Button("Scan & Setup Audio Identities", GUILayout.Height(30)))
            {
                UpdateAudioData();
            }
        }

        private static void DrawAudioManagerSection()
        {
            EditorGUILayout.LabelField("AudioManager in Scene", EditorStyles.boldLabel);

            var existing = FindObjectOfType<AudioManager>();
            if (existing)
            {
                EditorGUILayout.HelpBox($"AudioManager đã tồn tại trên \"{existing.gameObject.name}\".", MessageType.Info);
                if (GUILayout.Button("Select AudioManager"))
                {
                    Selection.activeGameObject = existing.gameObject;
                    EditorGUIUtility.PingObject(existing.gameObject);
                }

                return;
            }

            EditorGUILayout.HelpBox("Scene hiện chưa có AudioManager.", MessageType.Warning);

            if (GUILayout.Button("Create AudioManager GameObject", GUILayout.Height(25)))
            {
                var go = new GameObject("AudioManager");
                go.AddComponent<AudioManager>();
                Undo.RegisterCreatedObjectUndo(go, "Create AudioManager");

                var scene = SceneManager.GetActiveScene();
                if (!scene.isDirty)
                    EditorSceneManager.MarkSceneDirty(scene);

                Selection.activeGameObject = go;
                Debug.Log("[AudioManager] Đã tạo GameObject 'AudioManager' và gắn component.");
            }
        }

        private void UpdateAudioData()
        {
            var manager = FindObjectOfType<AudioManager>();
            if (!manager)
            {
                Debug.LogError("[AudioManager] Không tìm thấy AudioManager trong scene. Hãy tạo trước.");
                return;
            }

            if (string.IsNullOrEmpty(audioFolderPath))
            {
                Debug.LogError("[AudioManager] Audio folder path đang trống.");
                return;
            }

            if (string.IsNullOrEmpty(audioIdentityFolderPath))
            {
                Debug.LogError("[AudioManager] AudioIdentity folder path đang trống.");
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

            // Sanitize tên clip để dùng làm tên Asset & property
            string Sanitize(string rawName)
            {
                return rawName
                    .Replace(" ", "_")
                    .Replace("-", "_");
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
                    return (relFolder, Sanitize(baseName));
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
                var identityName = string.IsNullOrEmpty(folderPart) ? sanitizedName : $"{folderPart}_{sanitizedName}";

                var identitySubPath = string.IsNullOrEmpty(relativeFolder)
                    ? $"{sanitizedName}.asset"
                    : $"{relativeFolder.Replace('\\', '/')}/{sanitizedName}.asset";
                var identityAssetPath = $"{identityFolderNormalized}/{identitySubPath}";

                var identityDir = Path.GetDirectoryName(identityAssetPath)?.Replace("\\", "/");
                if (!string.IsNullOrEmpty(identityDir) && !Directory.Exists(identityDir))
                    Directory.CreateDirectory(identityDir);

                var identity = AssetDatabase.LoadAssetAtPath<AudioIdentity>(identityAssetPath);
                if (!identity)
                {
                    identity = ScriptableObject.CreateInstance<AudioIdentity>();
                    identity.name = identityName;
                    AssetDatabase.CreateAsset(identity, identityAssetPath);
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

                var identityGuid = AssetDatabase.AssetPathToGUID(identityAssetPath);
                if (!string.IsNullOrEmpty(identityGuid))
                {
                    identityGuids.Add((identityName, identityGuid));
                    identityNames.Add(identityName);
                    var identityPathNorm = identityAssetPath.Replace("\\", "/");
                    currentIdentityPaths.Add(identityPathNorm);
                    identityPathsForAddressables.Add(identityPathNorm);
                }
            }

            // Xóa các AudioIdentity nằm trong thư mục identity nhưng không còn tương ứng clip nào (thừa)
            RemoveOrphanAudioIdentities(identityFolderNormalized, currentIdentityPaths);

            AssetDatabase.SaveAssets();

            SetupIdentityReferencesOnManager(manager, identityGuids);

            EnsureAudioInAddressables(identityPathsForAddressables, clipPathsForAddressables);

            GenerateAudioIdClass(identityNames, audioIdOutputPath);
        }

        /// <summary>
        /// Đưa tất cả AudioIdentity và AudioClip vào Addressables: group "Audio", label "Audio".
        /// Yêu cầu đã cài package Addressables. AssetReferenceT (AudioClipReference, AudioIdentityReference) sẽ hoạt động mà không cần setup thủ công.
        /// </summary>
        private static void EnsureAudioInAddressables(
            List<string> identityAssetPaths,
            HashSet<string> clipAssetPaths)
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

            // Tìm hoặc tạo group "Audio"
            var audioGroup = settings.FindGroup(AddressablesAudioGroupName);
            if (audioGroup == null)
            {
                audioGroup = settings.CreateGroup(AddressablesAudioGroupName, false, false, false, null, typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema));
                if (audioGroup == null)
                {
                    Debug.LogError("[AudioManager] Không tạo được Addressables group \"Audio\".");
                    return;
                }
                Debug.Log($"[AudioManager] Đã tạo Addressables group \"{AddressablesAudioGroupName}\".");
            }

            var processed = 0;
            foreach (var path in identityAssetPaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) continue;
                var entry = settings.CreateOrMoveEntry(guid, audioGroup, false, true);
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
                var entry = settings.CreateOrMoveEntry(guid, audioGroup, false, true);
                if (entry != null)
                {
                    entry.SetLabel(AddressablesAudioLabel, true, true, true);
                    processed++;
                }
            }

            if (processed > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, audioGroup, true, false);
                Debug.Log($"[AudioManager] Đã thêm/cập nhật {processed} asset vào Addressables (group \"{AddressablesAudioGroupName}\", label \"{AddressablesAudioLabel}\").");
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

        private static void SetupIdentityReferencesOnManager(AudioManager manager, List<(string name, string guid)> identityGuids)
        {
            if (!manager) return;
            if (identityGuids == null) return;

            var so = new SerializedObject(manager);
            var listProp = so.FindProperty("identityReferences");
            if (listProp == null || !listProp.isArray)
            {
                Debug.LogError("[AudioManager] Không tìm thấy serialized field 'identityReferences' trên AudioManager.");
                return;
            }

            listProp.ClearArray();
            for (int i = 0; i < identityGuids.Count; i++)
            {
                listProp.InsertArrayElementAtIndex(i);
                var element = listProp.GetArrayElementAtIndex(i);
                var guidProp = element.FindPropertyRelative("m_AssetGUID");
                if (guidProp != null)
                {
                    guidProp.stringValue = identityGuids[i].guid;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);

            var scene = SceneManager.GetActiveScene();
            if (!scene.isDirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
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

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("public static class AudioID");
            sb.AppendLine("{");
            sb.AppendLine("    private static GameUp.Core.AudioIdentity Get(string name)");
            sb.AppendLine("    {");
            sb.AppendLine("        return GameUp.Core.AudioManager.TryGetIdentity(name, out var identity) ? identity : null;");
            sb.AppendLine("    }");
            sb.AppendLine();

            foreach (var sanitizedName in ordered)
            {
                sb.AppendLine($"    public static GameUp.Core.AudioIdentity {sanitizedName} => Get(\"{sanitizedName}\");");
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
    }
}
#endif

