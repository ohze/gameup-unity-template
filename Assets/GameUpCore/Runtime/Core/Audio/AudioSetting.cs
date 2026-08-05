using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// Cài đặt âm thanh lưu vào bộ nhớ cục bộ: bật/tắt và độ lớn cho từng kênh.
    /// Mọi thay đổi đều dispatch signal, <see cref="AudioManager"/> nghe để cập nhật ngay các source đang phát.
    /// </summary>
    public class AudioSetting : Singleton<AudioSetting>
    {
        public const string SoundKey = "audio_sk";
        public const string MusicKey = "audio_mk";
        public const string SoundVolumeKey = "audio_svk";
        public const string MusicVolumeKey = "audio_mvk";

        public readonly BooleanVar IsMusicOn = new(MusicKey);
        public readonly BooleanVar IsSoundOn = new(SoundKey);

        /// <summary>Độ lớn nhạc nền, 0..1.</summary>
        public readonly FloatVar MusicVolume = new(MusicVolumeKey, 1f);

        /// <summary>Độ lớn SFX và UI, 0..1.</summary>
        public readonly FloatVar SoundVolume = new(SoundVolumeKey, 1f);

        /// <summary>Volume của kênh tương ứng, đã tính cả trạng thái bật/tắt (tắt = 0).</summary>
        public float GetVolume(AudioCategory category)
        {
            return IsOn(category) ? Mathf.Clamp01(GetVolumeVar(category).Value) : 0f;
        }

        public bool IsOn(AudioCategory category)
        {
            return category == AudioCategory.Music ? IsMusicOn.Value : IsSoundOn.Value;
        }

        public FloatVar GetVolumeVar(AudioCategory category)
        {
            return category == AudioCategory.Music ? MusicVolume : SoundVolume;
        }

        public void SetVolume(AudioCategory category, float value)
        {
            GetVolumeVar(category).Value = Mathf.Clamp01(value);
        }
    }
}
