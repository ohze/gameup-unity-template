using System;
using UnityEngine;

namespace GameUp.Core
{
    [CreateAssetMenu(
        fileName = "SO_AudioIdentity",
        menuName = "GameUp/Audio/Audio Identity")]
    public class AudioIdentity : ScriptableObject
    {
        public AudioClipReference clipRef;

        [Range(0f, 1f)]
        public float volume = 1f;

        public bool isLoop = false;
    }
}

