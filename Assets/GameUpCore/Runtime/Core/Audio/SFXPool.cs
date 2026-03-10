using System.Collections.Generic;
using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// Pool of AudioSources for playing multiple SFX simultaneously.
    /// </summary>
    public sealed class SFXPool
    {
        readonly List<AudioSource> _sources = new();
        readonly Transform _parent;
        readonly int _maxSources;

        public SFXPool(Transform parent, int maxSources = 8)
        {
            _parent = parent;
            _maxSources = maxSources;
            for (int i = 0; i < maxSources; i++)
                _sources.Add(CreateSource(i));
        }

        /// <summary>Play a clip using an available pooled AudioSource.</summary>
        public void Play(AudioClip clip, float volume = 1f)
        {
            var source = GetAvailable();
            if (source == null) return;

            source.clip = clip;
            source.volume = volume;
            source.loop = false;
            source.Play();
        }

        /// <summary>Stop all currently playing SFX.</summary>
        public void StopAll()
        {
            foreach (var s in _sources)
                s.Stop();
        }

        AudioSource GetAvailable()
        {
            foreach (var s in _sources)
                if (!s.isPlaying) return s;

            if (_sources.Count < _maxSources * 2)
            {
                var s = CreateSource(_sources.Count);
                _sources.Add(s);
                return s;
            }

            return null;
        }

        AudioSource CreateSource(int index)
        {
            var go = new GameObject($"SFX_Source_{index}");
            go.transform.SetParent(_parent);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            return src;
        }
    }
}
