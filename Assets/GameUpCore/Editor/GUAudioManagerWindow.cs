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
        private const string PrefsKeyEnumPath = "GameUp.Audio.EnumPath";
        private const string PrefsKeyAudioFolder = "GameUp.Audio.FolderPath";
        private const string DefaultEnumAssetPath = "Assets/GameUpCore/Runtime/Core/Audio/AudioClipType.cs";

        private string enumFilePath;
        private string audioFolderPath;

        [MenuItem("GameUp/Audio/Setup AudioManager")]
        public static void ShowWindow()
        {
            var window = GetWindow<GUAudioManagerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.Show();
        }

        private void OnEnable()
        {
            enumFilePath = EditorPrefs.GetString(PrefsKeyEnumPath,
                DefaultEnumAssetPath);
            audioFolderPath = EditorPrefs.GetString(PrefsKeyAudioFolder,
                "Games/Addressables/Sounds");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Audio Manager Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawAudioManagerSection();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            enumFilePath = EditorGUILayout.TextField("Enum file path", enumFilePath);
            audioFolderPath = EditorGUILayout.TextField("Audio folder (project-relative)", audioFolderPath);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(PrefsKeyEnumPath, enumFilePath);
                EditorPrefs.SetString(PrefsKeyAudioFolder, audioFolderPath);
            }

            // Nếu người dùng trỏ enum sang path khác, đề xuất xoá file enum mặc định trong template (Assets/)
            if (!string.IsNullOrEmpty(enumFilePath) &&
                enumFilePath != DefaultEnumAssetPath &&
                AssetDatabase.LoadAssetAtPath<MonoScript>(DefaultEnumAssetPath) != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "Template đang có sẵn file AudioClipType.cs trong package (Assets/GameUpCore/...). " +
                    "Để tránh trùng enum khi project game định nghĩa enum riêng, bạn có thể xoá file template này.",
                    MessageType.Info);

                if (GUILayout.Button("Delete template AudioClipType.cs in Assets/GameUpCore", GUILayout.Height(22)))
                {
                    var confirm = EditorUtility.DisplayDialog(
                        "Delete template AudioClipType.cs?",
                        "Hành động này sẽ xoá file Assets/GameUpCore/Runtime/Core/Audio/AudioClipType.cs trong project template hiện tại.\n\n" +
                        "Trong các project game dùng UPM, file enum nên được định nghĩa bên ngoài package. Bạn chắc chắn muốn xoá chứ?",
                        "Yes, delete", "Cancel");

                    if (confirm)
                    {
                        if (AssetDatabase.DeleteAsset(DefaultEnumAssetPath))
                        {
                            AssetDatabase.Refresh();
                            Debug.Log("[AudioManager] Đã xoá file template AudioClipType.cs trong Assets/GameUpCore.");
                        }
                        else
                        {
                            Debug.LogWarning("[AudioManager] Không thể xoá Assets/GameUpCore/Runtime/Core/Audio/AudioClipType.cs (có thể do đang dùng UPM read-only).");
                        }
                    }
                }
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

            if (string.IsNullOrEmpty(enumFilePath) || string.IsNullOrEmpty(audioFolderPath))
            {
                Debug.LogError("[AudioManager] Enum file path hoặc audio folder path đang trống.");
                return;
            }

            if (!File.Exists(enumFilePath))
            {
                Debug.LogError($"[AudioManager] Không tìm thấy file enum tại path: {enumFilePath}");
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

            // Đọc enum cũ để giữ nguyên thứ tự
            var enumType = typeof(AudioClipType);
            var existingNames = Enum.GetNames(enumType).ToList(); // giữ thứ tự cũ

            // Tìm các name mới (chỉ thêm, không xoá / reorder)
            var allNewNames = clipGroups.Keys
                .Where(n => !existingNames.Contains(n))
                .ToList();

            // Build lại audioInfos: giữ data cũ, chỉ thêm item cho enum mới
            var audioInfosField = typeof(AudioManager)
                .GetField("audioInfos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (audioInfosField == null)
            {
                Debug.LogError("[AudioManager] Không truy cập được field 'audioInfos'.");
                return;
            }

            var list = (List<AudioInfoWithType>)audioInfosField.GetValue(manager);
            var existingTypes = new HashSet<AudioClipType>(list.Select(i => i.type));

            // 1. Đảm bảo có entry cho tất cả enum hiện tại (None + các giá trị cũ)
            foreach (var enumName in existingNames)
            {
                if (!Enum.TryParse(enumName, out AudioClipType enumValue))
                    continue;

                if (enumValue == AudioClipType.None)
                {
                    if (!existingTypes.Contains(enumValue))
                    {
                        list.Add(new AudioInfoWithType
                        {
                            name = enumName,
                            type = enumValue,
                            clipReferences = new List<AudioClipReference>(),
                            volume = 0.3f,
                            isLoop = false
                        });
                    }

                    continue;
                }

                if (existingTypes.Contains(enumValue))
                    continue;

                var refs = new List<AudioClipReference>();

                if (clipGroups.TryGetValue(enumName, out var exactClips))
                {
                    for (int i = 0; i < exactClips.Count; i++)
                    {
                        var clip = exactClips[i];
                        var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip));
                        refs.Add(new AudioClipReference(guid));
                    }
                }
                else
                {
                    var fallbackClips = clips.Where(c =>
                    {
                        var sanitized = Sanitize(c.name);
                        return sanitized.Equals(enumName, StringComparison.OrdinalIgnoreCase) ||
                               sanitized.StartsWith(enumName + "_", StringComparison.OrdinalIgnoreCase);
                    });

                    foreach (var clip in fallbackClips)
                    {
                        var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip));
                        refs.Add(new AudioClipReference(guid));
                    }
                }

                var info = new AudioInfoWithType
                {
                    name = enumName,
                    type = enumValue,
                    clipReferences = refs,
                    volume = 0.5f,
                    isLoop = false
                };

                list.Add(info);
                existingTypes.Add(enumValue);
            }

            // 2. Thêm entry cho các enum mới (tên mới), gán giá trị int theo thứ tự append
            if (allNewNames.Count > 0)
            {
                var existingWithoutNone = existingNames
                    .Where(n => n != nameof(AudioClipType.None))
                    .ToList();

                var orderedNewNames = allNewNames.OrderBy(n => n).ToList();

                for (int i = 0; i < orderedNewNames.Count; i++)
                {
                    var enumName = orderedNewNames[i];

                    // None = 0, các giá trị còn lại tăng dần
                    var rawValue = existingWithoutNone.Count + i + 1;
                    var enumValue = (AudioClipType)rawValue;

                    if (existingTypes.Contains(enumValue))
                        continue;

                    var refs = new List<AudioClipReference>();

                    if (clipGroups.TryGetValue(enumName, out var exactClips))
                    {
                        for (int j = 0; j < exactClips.Count; j++)
                        {
                            var clip = exactClips[j];
                            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip));
                            refs.Add(new AudioClipReference(guid));
                        }
                    }
                    else
                    {
                        var fallbackClips = clips.Where(c =>
                        {
                            var sanitized = Sanitize(c.name);
                            return sanitized.Equals(enumName, StringComparison.OrdinalIgnoreCase) ||
                                   sanitized.StartsWith(enumName + "_", StringComparison.OrdinalIgnoreCase);
                        });

                        foreach (var clip in fallbackClips)
                        {
                            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip));
                            refs.Add(new AudioClipReference(guid));
                        }
                    }

                    var info = new AudioInfoWithType
                    {
                        name = enumName,
                        type = enumValue,
                        clipReferences = refs,
                        volume = 0.5f,
                        isLoop = false
                    };

                    list.Add(info);
                    existingTypes.Add(enumValue);
                }
            }

            EditorUtility.SetDirty(manager);
            Debug.Log("[AudioManager] Đã cập nhật danh sách audioInfos (giữ thứ tự enum cũ, chỉ thêm mới).");

            // 3. Cuối cùng mới ghi lại file enum để tránh phụ thuộc vào compile ngay lập tức
            if (allNewNames.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("namespace GameUp.Core");
                sb.AppendLine("{");
                sb.AppendLine("    public enum AudioClipType");
                sb.AppendLine("    {");
                sb.AppendLine("        None = 0,");

                foreach (var name in existingNames)
                {
                    if (name == nameof(AudioClipType.None)) continue;
                    sb.AppendLine($"        {name},");
                }

                foreach (var name in allNewNames.OrderBy(n => n))
                {
                    sb.AppendLine($"        {name},");
                }

                sb.AppendLine("    }");
                sb.AppendLine("}");

                File.WriteAllText(enumFilePath, sb.ToString());
                AssetDatabase.Refresh();
                Debug.Log($"[AudioManager] Đã cập nhật enum AudioClipType, thêm {allNewNames.Count} entries mới.");
            }
        }
    }
}
#endif

