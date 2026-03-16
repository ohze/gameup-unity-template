#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Đảm bảo luôn có ít nhất một định nghĩa enum AudioClipType để AudioManager compile được.
    /// - Ưu tiên enum sẵn có trong template (Assets/GameUpCore/...) hoặc trong package (Packages/com.gameup.core/...).
    /// - Nếu cả hai đều không tồn tại (thường gặp khi dùng UPM mà package không ship sẵn enum),
    ///   script sẽ tự tạo một file enum tối thiểu tại path mà user đã chọn trong GUAudioManagerWindow.
    /// </summary>
    [InitializeOnLoad]
    public static class AudioClipTypeBootstrap
    {
        // Giữ sync với GUAudioManagerWindow
        private const string PrefsKeyEnumPath = "GameUp.Audio.EnumPath";
        private const string DefaultEnumAssetPath = "Assets/GameUpCore/Runtime/Core/Audio/AudioClipType.cs";
        private const string DefaultEnumPackagePath = "Packages/com.gameup.core/Runtime/Core/Audio/AudioClipType.cs";

        static AudioClipTypeBootstrap()
        {
            EnsureEnumExists();
        }

        private static void EnsureEnumExists()
        {
            // Nếu template enum trong Assets tồn tại -> OK
            if (File.Exists(DefaultEnumAssetPath))
                return;

            // Nếu enum trong package tồn tại -> OK
            if (File.Exists(DefaultEnumPackagePath))
                return;

            // Nếu AssetDatabase biết bất kỳ script C# nào tên "AudioClipType" thì cũng coi như đã có enum
            var guids = AssetDatabase.FindAssets("AudioClipType t:MonoScript");
            if (guids != null && guids.Length > 0)
                return;

            // Lấy path enum mà user đã chọn trong GUAudioManagerWindow
            var enumFilePath = EditorPrefs.GetString(PrefsKeyEnumPath, string.Empty);

            // Nếu user chưa cấu hình path, không auto tạo bừa: để user tự chọn qua GUAudioManagerWindow
            if (string.IsNullOrEmpty(enumFilePath))
                return;

            // Chỉ tự tạo khi path trỏ vào Assets (không ghi vào Packages)
            if (!enumFilePath.StartsWith("Assets/") && !enumFilePath.StartsWith("Assets\\"))
                return;

            // Nếu file đã tồn tại (nhưng có thể rỗng / lỗi) thì không ghi đè
            if (File.Exists(enumFilePath))
                return;

            var directory = Path.GetDirectoryName(enumFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var defaultEnumContent =
@"namespace GameUp.Core
{
    // Enum tối thiểu, game có thể chỉnh sửa / mở rộng sau
    public enum AudioClipType
    {
        None = 0,
    }
}
";

            File.WriteAllText(enumFilePath, defaultEnumContent);
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"[AudioManager] Auto-created minimal AudioClipType enum at: {enumFilePath}");
        }
    }
}
#endif

