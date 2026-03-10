using System;
using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// ScriptableObject holding audio clip references and settings.
    /// Create via Assets > Create > GameUp > Audio Data.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioData", menuName = "GameUp/Audio Data")]
    public sealed class AudioData : ScriptableObject
    {
        [SerializeField] AudioEntry[] _entries = Array.Empty<AudioEntry>();

        public AudioEntry[] Entries => _entries;

        public AudioEntry GetEntry(string id)
        {
            foreach (var e in _entries)
                if (e.Id == id) return e;
            return null;
        }
    }

    [Serializable]
    public sealed class AudioEntry
    {
        [SerializeField] string _id;
        [SerializeField] AudioClip _clip;
        [SerializeField, Range(0f, 1f)] float _volume = 1f;
        [SerializeField] bool _loop;

        public string Id => _id;
        public AudioClip Clip => _clip;
        public float Volume => _volume;
        public bool Loop => _loop;
    }
}
