using System.Collections;
using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// Manages BGM (with cross-fade) and SFX (pool-based).
    /// Requires <see cref="AudioData"/> ScriptableObject for clip configuration.
    /// </summary>
    public sealed class AudioManager : PersistentSingleton<AudioManager>
    {
        [SerializeField] AudioData _audioData;
        [SerializeField, Range(0f, 1f)] float _masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] float _bgmVolume = 1f;
        [SerializeField, Range(0f, 1f)] float _sfxVolume = 1f;
        [SerializeField] float _crossFadeDuration = 1f;

        AudioSource _bgmSourceA;
        AudioSource _bgmSourceB;
        AudioSource _activeBgmSource;
        SFXPool _sfxPool;
        Coroutine _crossFadeCoroutine;

        public float MasterVolume
        {
            get => _masterVolume;
            set => _masterVolume = Mathf.Clamp01(value);
        }

        public float BgmVolume
        {
            get => _bgmVolume;
            set
            {
                _bgmVolume = Mathf.Clamp01(value);
                if (_activeBgmSource != null)
                    _activeBgmSource.volume = _bgmVolume * _masterVolume;
            }
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Mathf.Clamp01(value);
        }

        protected override void OnSingletonAwake()
        {
            _bgmSourceA = CreateAudioSource("BGM_A");
            _bgmSourceB = CreateAudioSource("BGM_B");
            _activeBgmSource = _bgmSourceA;
            _sfxPool = new SFXPool(transform);
        }

        /// <summary>Play background music by entry ID with cross-fade.</summary>
        public void PlayBGM(string id)
        {
            var entry = FindEntry(id);
            if (entry?.Clip == null) return;

            if (_activeBgmSource.clip == entry.Clip && _activeBgmSource.isPlaying)
                return;

            var nextSource = _activeBgmSource == _bgmSourceA ? _bgmSourceB : _bgmSourceA;
            nextSource.clip = entry.Clip;
            nextSource.loop = true;
            nextSource.volume = 0f;
            nextSource.Play();

            if (_crossFadeCoroutine != null) StopCoroutine(_crossFadeCoroutine);
            _crossFadeCoroutine = StartCoroutine(CrossFade(_activeBgmSource, nextSource, entry.Volume));
            _activeBgmSource = nextSource;
        }

        /// <summary>Stop current BGM with fade out.</summary>
        public void StopBGM()
        {
            if (_crossFadeCoroutine != null) StopCoroutine(_crossFadeCoroutine);
            _crossFadeCoroutine = StartCoroutine(FadeOut(_activeBgmSource));
        }

        /// <summary>Play a sound effect by entry ID.</summary>
        public void PlaySFX(string id)
        {
            var entry = FindEntry(id);
            if (entry?.Clip == null) return;
            _sfxPool.Play(entry.Clip, entry.Volume * _sfxVolume * _masterVolume);
        }

        /// <summary>Play a sound effect directly from a clip.</summary>
        public void PlaySFX(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            _sfxPool.Play(clip, volume * _sfxVolume * _masterVolume);
        }

        public void SetAudioData(AudioData data) => _audioData = data;

        AudioEntry FindEntry(string id)
        {
            if (_audioData == null)
            {
                GLogger.Warning("AudioManager", "AudioData not assigned.");
                return null;
            }
            var entry = _audioData.GetEntry(id);
            if (entry == null)
                GLogger.Warning("AudioManager", $"Audio entry not found: {id}");
            return entry;
        }

        IEnumerator CrossFade(AudioSource from, AudioSource to, float targetVolume)
        {
            float timer = 0f;
            float fromStartVol = from.volume;
            float toTargetVol = targetVolume * _bgmVolume * _masterVolume;

            while (timer < _crossFadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = timer / _crossFadeDuration;
                from.volume = Mathf.Lerp(fromStartVol, 0f, t);
                to.volume = Mathf.Lerp(0f, toTargetVol, t);
                yield return null;
            }

            from.Stop();
            from.volume = 0f;
            to.volume = toTargetVol;
        }

        IEnumerator FadeOut(AudioSource source)
        {
            float startVol = source.volume;
            float timer = 0f;
            while (timer < _crossFadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVol, 0f, timer / _crossFadeDuration);
                yield return null;
            }
            source.Stop();
            source.volume = 0f;
        }

        AudioSource CreateAudioSource(string sourceName)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            return src;
        }
    }
}
