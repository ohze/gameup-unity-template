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

        private string audioFolderPath;
        private string audioIdentityFolderPath;

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
                "Assets/GameData/AudioIdentities");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Audio Manager Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawAudioManagerSection();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            audioFolderPath = EditorGUILayout.TextField("Audio folder (project-relative)", audioFolderPath);
            audioIdentityFolderPath = EditorGUILayout.TextField("AudioIdentity folder (under Assets)", audioIdentityFolderPath);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(PrefsKeyAudioFolder, audioFolderPath);
                EditorPrefs.SetString(PrefsKeyAudioIdentityFolder, audioIdentityFolderPath);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Scan & Update Audio Data", GUILayout.Height(30)))
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

            // Sanitize tên clip giống logic cũ
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

            // Build lại audioInfos với AudioIdentity: giữ data cũ, chỉ thêm / cập nhật theo clipGroups
            var audioInfosField = typeof(AudioManager)
                .GetField("audioInfos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (audioInfosField == null)
            {
                Debug.LogError("[AudioManager] Không truy cập được field 'audioInfos'.");
                return;
            }

            var list = (List<AudioInfoWithType>)audioInfosField.GetValue(manager);
            var identityToInfo = new Dictionary<AudioIdentity, AudioInfoWithType>();
            for (int i = 0; i < list.Count; i++)
            {
                var info = list[i];
                if (info == null || !info.identity) continue;
                if (!identityToInfo.ContainsKey(info.identity))
                    identityToInfo.Add(info.identity, info);
            }

            // Đảm bảo thư mục chứa AudioIdentity assets tồn tại
            if (!Directory.Exists(audioIdentityFolderPath))
            {
                Directory.CreateDirectory(audioIdentityFolderPath);
            }

            // Tạo / lấy AudioIdentity cho từng nhóm clip, đồng thời cập nhật AudioInfoWithType tương ứng
            var identityEntries = new List<(AudioIdentity identity, string sanitizedName)>();

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

                // Chuẩn bị danh sách clip references mới, merge với cũ (không làm mất data)
                var newRefs = new List<AudioClipReference>();

                if (identityToInfo.TryGetValue(identity, out var existingInfo) && existingInfo.clipReferences != null)
                {
                    // Giữ nguyên toàn bộ reference cũ
                    newRefs.AddRange(existingInfo.clipReferences);
                }

                foreach (var clip in kvp.Value)
                {
                    var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip));
                    if (string.IsNullOrEmpty(guid)) continue;
                    newRefs.Add(new AudioClipReference(guid));
                }

                if (!identityToInfo.TryGetValue(identity, out var infoWithIdentity))
                {
                    infoWithIdentity = new AudioInfoWithType
                    {
                        name = sanitizedName,
                        identity = identity,
                        clipReferences = newRefs,
                        volume = 0.5f,
                        isLoop = false
                    };
                    list.Add(infoWithIdentity);
                    identityToInfo.Add(identity, infoWithIdentity);
                }
                else
                {
                    infoWithIdentity.name = sanitizedName;
                    infoWithIdentity.clipReferences = newRefs;
                }

                identityEntries.Add((identity, sanitizedName));
            }

            EditorUtility.SetDirty(manager);
            Debug.Log("[AudioManager] Đã cập nhật danh sách audioInfos dựa trên AudioIdentity (giữ và merge data cũ).");

            // 3. Sinh file AudioID.cs trong Assets, trỏ tới từng AudioIdentity asset
            GenerateAudioIdClass(identityEntries);
        }

        private static void GenerateAudioIdClass(List<(AudioIdentity identity, string sanitizedName)> entries)
        {
            if (entries == null || entries.Count == 0)
                return;

            var ordered = entries
                .Where(e => e.identity)
                .OrderBy(e => e.sanitizedName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine("public static class AudioID");
            sb.AppendLine("{");
            sb.AppendLine("    private static T Get<T>(string path, ref T field) where T : UnityEngine.Object {");
            sb.AppendLine("        if (field == null) field = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);");
            sb.AppendLine("        return field;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Lấy folder hiện tại từ EditorPrefs (đồng bộ với UpdateAudioData)
            var audioIdentityFolderPath = EditorPrefs.GetString(
                PrefsKeyAudioIdentityFolder,
                "Assets/GameData/AudioIdentities");

            foreach (var entry in ordered)
            {
                var sanitizedName = entry.sanitizedName;
                var fieldName = "_" + sanitizedName.ToLowerInvariant();
                var assetPath = $"{audioIdentityFolderPath}/{sanitizedName}.asset";

                sb.AppendLine($"    private static GameUp.Core.AudioIdentity {fieldName};");
                sb.AppendLine(
                    $"    public static GameUp.Core.AudioIdentity {sanitizedName} => Get(\"{assetPath}\", ref {fieldName});");
                sb.AppendLine();
            }

            sb.AppendLine("}");
            sb.AppendLine("#endif");

            var targetPath = "Assets/AudioID.cs";
            File.WriteAllText(targetPath, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[AudioManager] Đã sinh lại file AudioID.cs với {ordered.Count} entries tại: {targetPath}");
        }
    }
}
#endif

