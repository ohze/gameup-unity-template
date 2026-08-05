using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GameUp.Core
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoSingleton<AudioManager>
    {
        [SerializeField] private AudioSource musicSource;
        [SerializeField, Min(1)] private int maxSource = 16;
        [SerializeField] private bool preloadIdentityOnAwake = true;
        [SerializeField] private AudioDatabase database;

        [Header("Music")]
        [SerializeField, Min(0f), Tooltip("Thời gian fade mặc định khi đổi/nhạc dừng, tính bằng giây.")]
        private float defaultMusicFade = 0.5f;

        /// <summary>Một source đang phát cùng thông tin để tính lại volume khi người chơi chỉnh cài đặt.</summary>
        private class PlayingEntry
        {
            /// <summary>Định danh duy nhất của lần phát này, dùng cho <see cref="AudioHandle"/>. 0 = nhạc nền.</summary>
            public int Id;

            /// <summary>Identity đã phát ra source này — dùng để Stop theo ID.</summary>
            public AudioIdentity Identity;

            public AudioSource Source;
            public AudioCategory Category;

            /// <summary>Volume khai báo trong AudioIdentity.</summary>
            public float BaseVolume = 1f;

            /// <summary>Hệ số fade 0..1.</summary>
            public float FadeScale = 1f;

            /// <summary>Bị Stop trong lúc clip còn đang load — khi clip về thì không phát nữa.</summary>
            public bool Cancelled;

            public void Apply(AudioSetting setting)
            {
                if (!Source) return;
                Source.volume = BaseVolume * FadeScale * setting.GetVolume(Category);
            }
        }

        private readonly List<AudioSource> _sources = new();
        private readonly HashSet<AudioSource> _busySources = new(); // nguồn đang "reserve" trong khi loading
        private readonly List<PlayingEntry> _playing = new();
        private int _nextHandleId = 1;
        private readonly Dictionary<object, AsyncOperationHandle<AudioClip>> _clipHandles = new();
        private readonly Dictionary<AudioIdentityReference, AsyncOperationHandle<AudioIdentity>> _identityHandles = new();
        private readonly Dictionary<string, AudioIdentity> _identityByName = new(StringComparer.OrdinalIgnoreCase);

        private PlayingEntry _activeMusic;
        private PlayingEntry _idleMusic;
        private AudioIdentity _currentMusicIdentity;
        private Coroutine _musicFadeRoutine;

        /// <summary>AudioIdentity của nhạc nền đang phát, null nếu không có.</summary>
        public static AudioIdentity CurrentMusic => Instance ? Instance._currentMusicIdentity : null;

        protected override void Awake()
        {
            base.Awake();

            musicSource ??= GetComponent<AudioSource>();
            SetupMusicSources();

            var prewarm = Mathf.Max(1, maxSource / 2);
            for (int i = 0; i < prewarm; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                _sources.Add(s);
            }

            if (preloadIdentityOnAwake)
            {
                PreloadIdentities();
            }
        }

        private void SetupMusicSources()
        {
            musicSource.playOnAwake = false;
            musicSource.loop = true;

            // Source thứ hai để crossfade: nhạc mới fade in trong khi nhạc cũ fade out.
            var secondary = gameObject.AddComponent<AudioSource>();
            secondary.playOnAwake = false;
            secondary.loop = true;

            _activeMusic = new PlayingEntry { Source = musicSource, Category = AudioCategory.Music };
            _idleMusic = new PlayingEntry { Source = secondary, Category = AudioCategory.Music };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (musicSource == null)
            {
                musicSource = GetComponent<AudioSource>();
                GameUtils.SaveAssets(this);
            }
        }
#endif

        private void OnEnable()
        {
            var setting = AudioSetting.Instance;
            setting.IsMusicOn.OnValueChange.AddListener(OnToggleChanged);
            setting.IsSoundOn.OnValueChange.AddListener(OnToggleChanged);
            setting.MusicVolume.OnValueChange.AddListener(OnVolumeChanged);
            setting.SoundVolume.OnValueChange.AddListener(OnVolumeChanged);
        }

        private void OnDisable()
        {
            var setting = AudioSetting.Instance;
            setting.IsMusicOn.OnValueChange.RemoveListener(OnToggleChanged);
            setting.IsSoundOn.OnValueChange.RemoveListener(OnToggleChanged);
            setting.MusicVolume.OnValueChange.RemoveListener(OnVolumeChanged);
            setting.SoundVolume.OnValueChange.RemoveListener(OnVolumeChanged);
        }

        private void OnToggleChanged(bool _) => RefreshVolumes();
        private void OnVolumeChanged(float _) => RefreshVolumes();

        /// <summary>Áp lại volume cho mọi source đang phát — gọi khi người chơi đổi cài đặt.</summary>
        public void RefreshVolumes()
        {
            var setting = AudioSetting.Instance;

            SweepFinished();
            for (var i = 0; i < _playing.Count; i++) _playing[i].Apply(setting);

            _activeMusic?.Apply(setting);
            _idleMusic?.Apply(setting);
        }

        private void OnDestroy()
        {
            foreach (var handle in _identityHandles.Values)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            _identityHandles.Clear();
            _identityByName.Clear();

            foreach (var handle in _clipHandles.Values)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            _clipHandles.Clear();
            _playing.Clear();
        }

        /// <summary>
        /// Trả về một AudioSource rảnh. Không chọn source đang isPlaying hoặc đang được reserve (busy) bởi clip đang load.
        /// </summary>
        private AudioSource GetSource()
        {
            // Ưu tiên source đã tạo sẵn
            for (int i = 0; i < _sources.Count; i++)
            {
                var s = _sources[i];
                if (!s.isPlaying && !_busySources.Contains(s))
                    return s;
            }

            // Nếu còn quota thì tạo mới
            if (_sources.Count < maxSource)
            {
                var sNew = gameObject.AddComponent<AudioSource>();
                sNew.playOnAwake = false;
                _sources.Add(sNew);
                return sNew;
            }

            return null; // hết nguồn phát
        }

        /// <summary>Bỏ khỏi danh sách theo dõi những source đã phát xong.</summary>
        private void SweepFinished()
        {
            for (var i = _playing.Count - 1; i >= 0; i--)
            {
                var entry = _playing[i];
                if (!entry.Source || (!entry.Source.isPlaying && !_busySources.Contains(entry.Source)))
                {
                    _playing.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Ghi nhận một lần phát ngay tại thời điểm gọi Play — trước cả khi clip load xong —
        /// để Stop gọi liền sau Play vẫn hủy được.
        /// </summary>
        private PlayingEntry Track(AudioSource source, AudioIdentity identity)
        {
            SweepFinished();

            var entry = new PlayingEntry
            {
                Id = _nextHandleId++,
                Identity = identity,
                Source = source,
                Category = identity.category,
                BaseVolume = identity.volume
            };

            entry.Apply(AudioSetting.Instance);
            _playing.Add(entry);
            return entry;
        }

        private PlayingEntry FindEntry(int id)
        {
            if (id <= 0) return null;

            for (var i = 0; i < _playing.Count; i++)
            {
                if (_playing[i].Id == id) return _playing[i];
            }

            return null;
        }

        /// <summary>Dừng hẳn một lần phát: hủy cả khi clip còn đang load.</summary>
        private void StopPlayingEntry(PlayingEntry entry)
        {
            if (entry == null) return;

            entry.Cancelled = true;

            if (entry.Source)
            {
                if (entry.Source.isPlaying) entry.Source.Stop();
                entry.Source.clip = null;
                ReleaseBusy(entry.Source);
            }

            _playing.Remove(entry);
        }

        private IEnumerator FadeOutEntryRoutine(PlayingEntry entry, float duration)
        {
            var setting = AudioSetting.Instance;
            var start = entry.FadeScale;
            var elapsed = 0f;

            while (elapsed < duration && !entry.Cancelled)
            {
                elapsed += Time.unscaledDeltaTime;
                entry.FadeScale = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / duration));
                entry.Apply(setting);
                yield return null;
            }

            StopPlayingEntry(entry);
        }

        private void StopEntryWithFade(PlayingEntry entry, float fadeDuration)
        {
            if (entry == null) return;

            // Chưa phát thật (clip đang load) thì fade vô nghĩa — hủy luôn.
            if (fadeDuration <= 0f || !entry.Source || !entry.Source.isPlaying)
            {
                StopPlayingEntry(entry);
                return;
            }

            StartCoroutine(FadeOutEntryRoutine(entry, fadeDuration));
        }

        #region Busy helpers

        internal static void MarkBusy(AudioSource source)
        {
            if (source && Instance) Instance._busySources.Add(source);
        }

        internal static void ReleaseBusy(AudioSource source)
        {
            if (source && Instance) Instance._busySources.Remove(source);
        }

        /// <summary>
        /// Giải phóng cờ busy vào frame kế tiếp (đảm bảo isPlaying kịp chuyển sang true).
        /// </summary>
        internal static IEnumerator ReleaseBusyNextFrame(AudioSource source)
        {
            yield return null; // chờ sang frame sau
            ReleaseBusy(source);
        }

        #endregion

        #region Loading

        public static void PreloadIdentities()
        {
            if (!Instance) return;
            Instance.PreloadIdentitiesInternal();
        }

        private void PreloadIdentitiesInternal()
        {
            var refs = database ? database.identityReferences : null;
            if (refs == null || refs.Count == 0)
                return;

            for (int i = 0; i < refs.Count; i++)
            {
                LoadIdentity(refs[i], null);
            }
        }

        /// <summary>Load (và cache) AudioClip theo reference — mọi chỗ phát âm thanh đều đi qua đây.</summary>
        private void LoadClip(AudioClipReference clipRef, Action<AudioClip> onLoaded, Action onFailed = null)
        {
            if (clipRef == null || !clipRef.RuntimeKeyIsValid())
            {
                onFailed?.Invoke();
                return;
            }

            var cacheKey = clipRef.RuntimeKey;
            if (!_clipHandles.TryGetValue(cacheKey, out var handle) || !handle.IsValid())
            {
                handle = clipRef.LoadAssetAsync<AudioClip>();
                _clipHandles[cacheKey] = handle;
            }

            AddressableLoad.WhenReady(handle, onLoaded, "AudioManager", cacheKey?.ToString(), onFailed);
        }

        /// <summary>Load (và cache) AudioIdentity theo reference, đồng thời đăng ký vào bảng tra theo tên.</summary>
        private void LoadIdentity(AudioIdentityReference identityReference, Action<AudioIdentity> onLoaded)
        {
            if (identityReference == null || !identityReference.RuntimeKeyIsValid()) return;

            if (!_identityHandles.TryGetValue(identityReference, out var handle) || !handle.IsValid())
            {
                handle = identityReference.LoadAssetAsync<AudioIdentity>();
                _identityHandles[identityReference] = handle;
            }

            AddressableLoad.WhenReady(handle, identity =>
            {
                _identityByName[identity.name] = identity;
                onLoaded?.Invoke(identity);
            }, "AudioManager", identityReference.RuntimeKey?.ToString());
        }

        public static bool TryGetIdentity(string identityName, out AudioIdentity identity)
        {
            identity = null;
            if (!Instance) return false;
            if (string.IsNullOrEmpty(identityName)) return false;
            return Instance._identityByName.TryGetValue(identityName, out identity) && identity;
        }

        #endregion

        #region SFX

        /// <summary>
        /// Phát audio theo AudioIdentity (one-shot hoặc loop theo cấu hình). Mặc định phát clip đầu list;
        /// isRandomClip = true thì chọn ngẫu nhiên.
        /// </summary>
        /// <returns>
        /// Handle của đúng lần phát này để dừng riêng nó — cần cho SFX loop.
        /// Trả về <see cref="AudioHandle.None"/> nếu không phát được (tắt tiếng, hết source, identity là nhạc nền).
        /// </returns>
        public AudioHandle Play(AudioIdentity identity, bool isRandomClip = false)
        {
            if (!identity) return AudioHandle.None;

            // Nhạc nền có đường riêng để còn crossfade.
            if (identity.category == AudioCategory.Music)
            {
                PlayMusic(identity);
                return AudioHandle.None;
            }

            if (!AudioSetting.Instance.IsOn(identity.category)) return AudioHandle.None;

            if (identity.clipRefs == null || identity.clipRefs.Count == 0)
                return AudioHandle.None;

            var clipRef = isRandomClip && identity.clipRefs.Count > 1
                ? identity.clipRefs.GetRandom()
                : identity.clipRefs[0];

            if (clipRef == null || !clipRef.RuntimeKeyIsValid())
                return AudioHandle.None;

            var source = GetSource();
            if (!source) return AudioHandle.None;

            GULogger.Log("AudioManager", "PlayAudio: " + identity.name + " - " + source.name);
            MarkBusy(source);

            // Ghi nhận trước khi load để Stop gọi ngay sau Play vẫn hủy được lần phát này.
            var entry = Track(source, identity);

            LoadClip(clipRef, clip =>
            {
                if (entry.Cancelled)
                {
                    ReleaseBusy(source);
                    return;
                }

                source.clip = clip;
                source.loop = identity.isLoop;
                entry.Apply(AudioSetting.Instance);
                source.Play();
                StartCoroutine(ReleaseBusyNextFrame(source));
            }, () =>
            {
                ReleaseBusy(source);
                _playing.Remove(entry);
            });

            return new AudioHandle(entry.Id);
        }

        public void Play(AudioIdentityReference identityReference)
        {
            LoadIdentity(identityReference, identity => Play(identity));
        }

        /// <summary> API static tiện dụng để gọi từ bất kỳ đâu. isRandomClip = true thì chọn clip ngẫu nhiên từ list. </summary>
        public static AudioHandle PlayAudio(AudioIdentity identity, bool isRandomClip = false)
        {
            if (!Instance) return AudioHandle.None;
            if (!identity)
            {
                GULogger.Log("AudioManager", "PlayAudio: identity is null");
                return AudioHandle.None;
            }

            return Instance.Play(identity, isRandomClip);
        }

        /// <summary>
        /// Phát theo tên identity đã preload (xem <see cref="AudioDatabase"/>).
        /// </summary>
        public static AudioHandle PlayAudio(string identityName, bool isRandomClip = false)
        {
            if (!TryGetIdentity(identityName, out var identity))
            {
                GULogger.Error("AudioManager", $"Không tìm thấy AudioIdentity '{identityName}'. Đã preload qua AudioDatabase chưa?");
                return AudioHandle.None;
            }

            return PlayAudio(identity, isRandomClip);
        }

        public static void PlayAudio(AudioIdentityReference identityReference)
        {
            if (!Instance) return;
            Instance.Play(identityReference);
        }

        /// <summary>Dừng đúng lần phát ứng với handle. Handle đã hết hiệu lực thì không làm gì.</summary>
        public static void StopHandle(int handleId, float fadeDuration = 0f)
        {
            var instance = Instance;
            if (!instance) return;

            instance.StopEntryWithFade(instance.FindEntry(handleId), fadeDuration);
        }

        public static bool IsHandlePlaying(int handleId)
        {
            var instance = Instance;
            if (!instance) return false;

            var entry = instance.FindEntry(handleId);
            return entry != null && !entry.Cancelled;
        }

        /// <summary>
        /// Dừng mọi lần phát của một identity. Dùng khi không giữ handle,
        /// ví dụ tắt tiếng loop môi trường lúc rời màn chơi.
        /// </summary>
        public static void StopAudio(AudioIdentity identity, float fadeDuration = 0f)
        {
            var instance = Instance;
            if (!instance || !identity) return;

            for (var i = instance._playing.Count - 1; i >= 0; i--)
            {
                var entry = instance._playing[i];
                if (entry.Identity != identity) continue;

                instance.StopEntryWithFade(entry, fadeDuration);
            }
        }

        /// <summary>Dừng mọi lần phát của identity theo tên đã preload.</summary>
        public static void StopAudio(string identityName, float fadeDuration = 0f)
        {
            if (TryGetIdentity(identityName, out var identity)) StopAudio(identity, fadeDuration);
        }

        /// <summary>Identity này có source nào đang phát không (kể cả clip còn đang load).</summary>
        public static bool IsPlaying(AudioIdentity identity)
        {
            var instance = Instance;
            if (!instance || !identity) return false;

            for (var i = 0; i < instance._playing.Count; i++)
            {
                var entry = instance._playing[i];
                if (entry.Identity == identity && !entry.Cancelled) return true;
            }

            return false;
        }

        public static bool IsPlaying(string identityName)
        {
            return TryGetIdentity(identityName, out var identity) && IsPlaying(identity);
        }

        /// <summary>Dừng mọi SFX đang phát (không đụng tới nhạc nền).</summary>
        public static void StopAllSfx()
        {
            var instance = Instance;
            if (!instance) return;

            for (var i = instance._playing.Count - 1; i >= 0; i--)
            {
                instance.StopPlayingEntry(instance._playing[i]);
            }

            instance._playing.Clear();
        }

        #endregion

        #region Music

        /// <summary>
        /// Phát nhạc nền. Nếu đang phát đúng identity này thì bỏ qua để nhạc không bị giật lại từ đầu.
        /// </summary>
        /// <param name="fadeDuration">Thời gian crossfade; để &lt; 0 để dùng giá trị mặc định của manager.</param>
        public static void PlayMusic(AudioIdentity identity, float fadeDuration = -1f)
        {
            if (!identity) return;

            var instance = Instance;
            if (!instance || !instance.musicSource) return;
            if (identity.clipRefs == null || identity.clipRefs.Count == 0) return;

            var active = instance._activeMusic;
            if (instance._currentMusicIdentity == identity && active != null && active.Source && active.Source.isPlaying)
                return;

            instance._currentMusicIdentity = identity;
            var duration = fadeDuration < 0f ? instance.defaultMusicFade : fadeDuration;

            instance.LoadClip(identity.clipRefs[0], clip => instance.SwapMusic(clip, identity.volume, duration));
        }

        private void SwapMusic(AudioClip clip, float baseVolume, float duration)
        {
            if (!clip) return;

            var outgoing = _activeMusic;
            var incoming = _idleMusic;

            // Đổi vai trò: source rảnh trở thành source đang phát.
            _activeMusic = incoming;
            _idleMusic = outgoing;

            incoming.Source.clip = clip;
            incoming.Source.loop = true;
            incoming.BaseVolume = baseVolume;
            incoming.FadeScale = duration > 0f ? 0f : 1f;
            incoming.Apply(AudioSetting.Instance);
            incoming.Source.Play();

            StopMusicFade();

            if (duration <= 0f)
            {
                StopEntry(outgoing);
                return;
            }

            _musicFadeRoutine = StartCoroutine(CrossFadeRoutine(incoming, outgoing, duration));
        }

        private IEnumerator CrossFadeRoutine(PlayingEntry incoming, PlayingEntry outgoing, float duration)
        {
            var setting = AudioSetting.Instance;
            var startOut = outgoing != null ? outgoing.FadeScale : 0f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                incoming.FadeScale = t;
                incoming.Apply(setting);

                if (outgoing != null)
                {
                    outgoing.FadeScale = Mathf.Lerp(startOut, 0f, t);
                    outgoing.Apply(setting);
                }

                yield return null;
            }

            incoming.FadeScale = 1f;
            incoming.Apply(setting);

            StopEntry(outgoing);
            _musicFadeRoutine = null;
        }

        /// <summary>Dừng nhạc nền, có thể fade out.</summary>
        public static void StopMusic(float fadeDuration = 0f)
        {
            var instance = Instance;
            if (!instance) return;

            instance._currentMusicIdentity = null;
            instance.StopMusicFade();

            if (fadeDuration <= 0f)
            {
                instance.StopEntry(instance._activeMusic);
                instance.StopEntry(instance._idleMusic);
                return;
            }

            instance._musicFadeRoutine = instance.StartCoroutine(instance.FadeOutRoutine(instance._activeMusic, fadeDuration));
        }

        private IEnumerator FadeOutRoutine(PlayingEntry entry, float duration)
        {
            if (entry == null) yield break;

            var setting = AudioSetting.Instance;
            var start = entry.FadeScale;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                entry.FadeScale = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / duration));
                entry.Apply(setting);
                yield return null;
            }

            StopEntry(entry);
            _musicFadeRoutine = null;
        }

        public static void PauseMusic()
        {
            var instance = Instance;
            if (instance && instance._activeMusic?.Source) instance._activeMusic.Source.Pause();
        }

        public static void ResumeMusic()
        {
            var instance = Instance;
            if (instance && instance._activeMusic?.Source) instance._activeMusic.Source.UnPause();
        }

        private void StopMusicFade()
        {
            if (_musicFadeRoutine == null) return;

            StopCoroutine(_musicFadeRoutine);
            _musicFadeRoutine = null;
        }

        private void StopEntry(PlayingEntry entry)
        {
            if (entry?.Source == null) return;

            entry.Source.Stop();
            entry.Source.clip = null;
            entry.FadeScale = 1f;
        }

        #endregion
    }

    [Serializable]
    public class BaseAudio
    {
        public AudioClipReference clipRef;
        public AudioCategory category = AudioCategory.Sfx;
        [Range(0f, 1f)] public float volume = 1f;
        public bool isLoop;

        private AsyncOperationHandle<AudioClip> _cacheOperation;
        private AudioSource _lastSource; // dùng cho StopAudio()

        public void PlayClip(AudioSource source)
        {
            if (!source) return;
            if (clipRef == null || !clipRef.RuntimeKeyIsValid()) return;
            if (!AudioSetting.Instance.IsOn(category)) return;

            _lastSource = source;
            AudioManager.MarkBusy(source);

            if (!_cacheOperation.IsValid()) _cacheOperation = clipRef.LoadAssetAsync<AudioClip>();

            AddressableLoad.WhenReady(_cacheOperation, clip =>
            {
                source.clip = clip;
                source.volume = volume * AudioSetting.Instance.GetVolume(category);
                source.loop = isLoop;
                source.Play();
                AudioManager.Instance.StartCoroutine(AudioManager.ReleaseBusyNextFrame(source));
            }, "AudioManager", "BaseAudio clip", () => AudioManager.ReleaseBusy(source));
        }

        public void StopAudio()
        {
            if (_lastSource != null && _lastSource.isPlaying)
                _lastSource.Stop();
        }
    }
}
