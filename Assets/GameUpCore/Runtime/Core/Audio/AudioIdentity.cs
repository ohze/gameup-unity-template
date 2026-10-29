using System.Collections.Generic;
using UnityEngine;

namespace GameUp.Core
{
    [CreateAssetMenu(
        fileName = "SO_AudioIdentity",
        menuName = "GameUp/Audio/Audio Identity")]
    public class AudioIdentity : ScriptableObject
    {
        /// <summary>Khoảng pitch an toàn — mọi pitch tính ra (kể cả pitch code tự đè) đều bị kẹp vào đây.</summary>
        public const float MinPitch = 0.1f;
        public const float MaxPitch = 3f;

        public List<AudioClipReference> clipRefs = new List<AudioClipReference>();

        /// <summary>Kênh phát: quyết định lấy volume/mute từ Music hay Sound trong <see cref="AudioSetting"/>.</summary>
        public AudioCategory category = AudioCategory.Sfx;

        /// <summary>Độ lớn riêng của clip này, sẽ được nhân với volume của kênh.</summary>
        [Range(0f, 1f)]
        public float volume = 1f;

        public bool isLoop = false;

        /// <summary>Pitch gốc của identity. 1 = tốc độ/cao độ gốc của clip.</summary>
        [Range(MinPitch, MaxPitch)]
        [Tooltip("Pitch gốc khi phát. 1 = giữ nguyên clip; > 1 cao và nhanh hơn, < 1 trầm và chậm hơn.")]
        public float pitch = 1f;

        /// <summary>
        /// Biên độ random pitch (đơn vị pitch) cho mỗi lần phát SFX: pitch thực = <see cref="pitch"/> ± giá trị này.
        /// Nhạc nền bỏ qua field này.
        /// </summary>
        [Range(0f, 1f)]
        [Tooltip("Random pitch mỗi lần phát SFX trong khoảng pitch ± giá trị này (vd 0.05 = ±0.05) để tiếng lặp lại không bị máy móc. 0 = không random. Nhạc nền bỏ qua.")]
        public float randomPitchRange = 0f;

        /// <summary>
        /// Load sẵn clip (không phát) khi identity được preload qua <see cref="AudioDatabase"/>,
        /// để lần phát đầu tiên không phải chờ Addressables.
        /// </summary>
        [Tooltip("Load sẵn clip khi AudioManager preload identity qua AudioDatabase — phát lần đầu không phải chờ load.")]
        public bool preloadClips = false;

        /// <summary>Bốc pitch cho một lần phát SFX: <see cref="pitch"/> cộng random trong ±<see cref="randomPitchRange"/>, đã kẹp.</summary>
        public float EvaluatePitch() => CalculatePitch(pitch, randomPitchRange, Random.value);

        /// <summary>Pitch gốc đã kẹp, không random — dùng cho nhạc nền.</summary>
        public float BasePitch => CalculatePitch(pitch, 0f, 0f);

        /// <summary>
        /// Hàm thuần tính pitch: <paramref name="random01"/> = 0 cho pitch - range, 1 cho pitch + range.
        /// Kết quả kẹp trong [<see cref="MinPitch"/>, <see cref="MaxPitch"/>]; range âm coi như 0.
        /// pitch ≤ 0 (dữ liệu bị zero-init, vd phần tử list mới thêm trong Inspector) coi như 1 thay vì kẹp về 0.1.
        /// </summary>
        public static float CalculatePitch(float pitch, float randomRange, float random01)
        {
            if (pitch <= 0f) pitch = 1f;

            var range = Mathf.Max(0f, randomRange);
            var offset = Mathf.Lerp(-range, range, random01); // Lerp tự kẹp random01 về [0, 1]
            return ClampPitch(pitch + offset);
        }

        public static float ClampPitch(float pitch) => Mathf.Clamp(pitch, MinPitch, MaxPitch);
    }
}

