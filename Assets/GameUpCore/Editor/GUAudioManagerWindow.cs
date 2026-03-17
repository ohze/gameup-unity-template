#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameUp.Core;
using UnityEditor;
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

            // Group các clip theo tên đã chuẩn hoá
            var clipGroups = clips
                .GroupBy(c => Sanitize(c.name))
                .ToDictionary(g => g.Key, g => g.ToList());

            // Đảm bảo thư mục chứa AudioIdentity assets (nằm trong Resources) tồn tại
            if (!Directory.Exists(audioIdentityFolderPath))
            {
                Directory.CreateDirectory(audioIdentityFolderPath);
            }

            // Tạo / cập nhật AudioIdentity cho từng nhóm clip
            var identityGuids = new List<(string name, string guid)>();
            var identityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in clipGroups)
            {
                var sanitizedName = kvp.Key;
                var identityAssetPath = $"{audioIdentityFolderPath}/{sanitizedName}.asset";

                var identity = AssetDatabase.LoadAssetAtPath<AudioIdentity>(identityAssetPath);
                if (!identity)
                {
                    identity = ScriptableObject.CreateInstance<AudioIdentity>();
                    identity.name = sanitizedName;
                    AssetDatabase.CreateAsset(identity, identityAssetPath);
                }

                // Gán reference đến AudioClip qua Addressables (GUID)
                var clip = kvp.Value[0];
                var clipGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip));
                if (!string.IsNullOrEmpty(clipGuid))
                {
                    identity.clipRef = new AudioClipReference(clipGuid);
                    EditorUtility.SetDirty(identity);
                }

                // Lưu GUID của chính AudioIdentity asset để sinh AudioID dùng Addressables
                var identityGuid = AssetDatabase.AssetPathToGUID(identityAssetPath);
                if (!string.IsNullOrEmpty(identityGuid))
                {
                    identityGuids.Add((sanitizedName, identityGuid));
                    identityNames.Add(sanitizedName);
                }
            }

            AssetDatabase.SaveAssets();

            SetupIdentityReferencesOnManager(manager, identityGuids);

            // 3. Sinh file AudioID.cs trong Assets, getter sync đọc cache AudioManager
            GenerateAudioIdClass(identityNames, audioIdOutputPath);
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

            // outputPath đã được normalize sang file .cs ở trên

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

