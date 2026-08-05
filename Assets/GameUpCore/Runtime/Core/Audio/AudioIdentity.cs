using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameUp.Core
{
    [CreateAssetMenu(
        fileName = "SO_AudioIdentity",
        menuName = "GameUp/Audio/Audio Identity")]
    public class AudioIdentity : ScriptableObject
    {
        public List<AudioClipReference> clipRefs = new List<AudioClipReference>();

        /// <summary>Kênh phát: quyết định lấy volume/mute từ Music hay Sound trong <see cref="AudioSetting"/>.</summary>
        public AudioCategory category = AudioCategory.Sfx;

        /// <summary>Độ lớn riêng của clip này, sẽ được nhân với volume của kênh.</summary>
        [Range(0f, 1f)]
        public float volume = 1f;

        public bool isLoop = false;
    }
}

