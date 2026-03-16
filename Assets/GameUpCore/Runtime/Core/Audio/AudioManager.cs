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
        [SerializeField] private List<AudioIdentityReference> identityReferences = new();

        private readonly List<AudioSource> _sources = new();
        private readonly HashSet<AudioSource> _busySources = new(); // nguồn đang "reserve" trong khi loading
        private readonly Dictionary<AudioIdentity, AsyncOperationHandle<AudioClip>> _clipHandles = new();
        private readonly Dictionary<AudioIdentityReference, AsyncOperationHandle<AudioIdentity>> _identityHandles = new();
        private readonly Dictionary<string, AudioIdentity> _identityByName = new(StringComparer.OrdinalIgnoreCase);

        protected override void Awake()
        {
            base.Awake();
            musicSource ??= GetComponent<AudioSource>();
            var prewarm = Mathf.Max(1, maxSource / 2);
            for (int i = 0; i < prewarm; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                _sources.Add(s);
            }

            if (preloadIdentityOnAwake)
            {
                PreloadIdentities();
            }
        }

        private void OnEnable()
        {
            AudioSetting.Instance.IsMusicOn.OnValueChange.AddListener(OnMusicChange);
        }

        private void OnDisable()
        {
            AudioSetting.Instance.IsMusicOn.OnValueChange.RemoveListener(OnMusicChange);
        }

        private void OnMusicChange(bool isMusicOn)
        {
            musicSource.mute = !isMusicOn;
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
        }

        /// <summary>
        /// Trả về một AudioSource rảnh. Không chọn source đang isPlaying hoặc đang được reserve (busy) bởi clip đang load.
        /// </summary>
        private static AudioSource GetSource()
        {
            // Ưu tiên source đã tạo sẵn
            for (int i = 0; i < Instance._sources.Count; i++)
            {
                var s = Instance._sources[i];
                if (!s.isPlaying && !Instance._busySources.Contains(s))
                    return s;
            }

            // Nếu còn quota thì tạo mới
            if (Instance._sources.Count < Instance.maxSource)
            {
                var sNew = Instance.gameObject.AddComponent<AudioSource>();
                Instance._sources.Add(sNew);
                return sNew;
            }

            return null; // hết nguồn phát
        }

        #region Busy helpers

        internal static void MarkBusy(AudioSource source)
        {
            if (source) Instance._busySources.Add(source);
        }

        internal static void ReleaseBusy(AudioSource source)
        {
            if (source) Instance._busySources.Remove(source);
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

        #region Public API

        public static void PreloadIdentities()
        {
            if (!Instance) return;
            Instance.PreloadIdentitiesInternal();
        }

        private void PreloadIdentitiesInternal()
        {
            if (identityReferences == null || identityReferences.Count == 0)
                return;

            for (int i = 0; i < identityReferences.Count; i++)
            {
                var idRef = identityReferences[i];
                if (idRef == null || !idRef.RuntimeKeyIsValid())
                    continue;

                if (_identityHandles.TryGetValue(idRef, out var existing) && existing.IsValid())
                    continue;

                var op = idRef.LoadAssetAsync<AudioIdentity>();
                _identityHandles[idRef] = op;
                op.Completed += h =>
                {
                    if (h.Result)
                    {
                        _identityByName[h.Result.name] = h.Result;
                    }
                };
            }
        }

        public static bool TryGetIdentity(string identityName, out AudioIdentity identity)
        {
            identity = null;
            if (!Instance) return false;
            if (string.IsNullOrEmpty(identityName)) return false;
            return Instance._identityByName.TryGetValue(identityName, out identity) && identity;
        }

        /// <summary> Phát audio theo AudioIdentity (one-shot hoặc loop theo cấu hình). </summary>
        public void Play(AudioIdentity identity)
        {
            if (!identity) return;
            if (!AudioSetting.Instance.IsSoundOn.Value) return;

            var source = GetSource();
            if (!source) return;

            if (identity.clipRef == null)
                return;

            GULogger.Log("AudioManager", "PlayAudio: " + identity.name + " - " + source.name);
            MarkBusy(source);

            void DoPlay(AudioClip clip)
            {
                if (!clip)
                {
                    ReleaseBusy(source);
                    return;
                }

                source.clip = clip;
                source.volume = identity.volume;
                source.loop = identity.isLoop;
                source.Play();
                Instance.StartCoroutine(ReleaseBusyNextFrame(source));
            }

            if (_clipHandles.TryGetValue(identity, out var handle) && handle.IsValid())
            {
                if (handle.IsDone)
                {
                    DoPlay(handle.Result);
                }
                else
                {
                    handle.Completed += h => DoPlay(h.Result);
                }
            }
            else
            {
                var op = identity.clipRef.LoadAssetAsync<AudioClip>();
                _clipHandles[identity] = op;
                op.Completed += h => DoPlay(h.Result);
            }
        }

        public void Play(AudioIdentityReference identityReference)
        {
            if (identityReference == null || !identityReference.RuntimeKeyIsValid()) return;

            if (_identityHandles.TryGetValue(identityReference, out var handle) && handle.IsValid())
            {
                if (handle.IsDone)
                {
                    Play(handle.Result);
                }
                else
                {
                    handle.Completed += h => Play(h.Result);
                }

                return;
            }

            var op = identityReference.LoadAssetAsync<AudioIdentity>();
            _identityHandles[identityReference] = op;
            op.Completed += h =>
            {
                if (h.Result)
                {
                    _identityByName[h.Result.name] = h.Result;
                    Play(h.Result);
                }
            };
        }

        /// <summary> API static tiện dụng để gọi từ bất kỳ đâu. </summary>
        public static void PlayAudio(AudioIdentity identity)
        {
            if (!Instance) return;
            if (!identity)
            {
                GULogger.Log("AudioManager", "PlayAudio: identity is null");
                return;
            }

            GULogger.Log("AudioManager", "PlayAudio: " + identity.name);
            Instance.Play(identity);
        }

        public static void PlayAudio(AudioIdentityReference identityReference)
        {
            if (!Instance) return;
            Instance.Play(identityReference);
        }

        public static void PlayMusic(AudioIdentity identity)
        {
            if (!identity) return;
            if (!Instance.musicSource) return;
            if (identity.clipRef == null) return;

            void DoPlay(AudioClip clip)
            {
                if (!clip) return;
                Instance.musicSource.clip = clip;
                Instance.musicSource.volume = identity.volume;
                Instance.musicSource.loop = true;
                Instance.musicSource.Play();
                // ⚠️ Music phải theo IsMusicOn, không theo IsSoundOn
                Instance.musicSource.mute = !AudioSetting.Instance.IsMusicOn.Value;
            }

            if (Instance._clipHandles.TryGetValue(identity, out var handle) && handle.IsValid())
            {
                if (handle.IsDone)
                {
                    DoPlay(handle.Result);
                }
                else
                {
                    handle.Completed += h => DoPlay(h.Result);
                }
            }
            else
            {
                var op = identity.clipRef.LoadAssetAsync<AudioClip>();
                Instance._clipHandles[identity] = op;
                op.Completed += h => DoPlay(h.Result);
            }
        }

        public static void StopMusic()
        {
            if (Instance.musicSource)
            {
                Instance.musicSource.Stop();
                Instance.musicSource.clip = null;
            }
        }

        #endregion
    }

    [Serializable]
    public class BaseAudio
    {
        public AudioClipReference clipRef;
        [Range(0f, 1f)] public float volume = 1f;
        public bool isLoop;

        private AsyncOperationHandle<AudioClip> _cacheOperation;
        private AudioSource _lastSource; // dùng cho StopAudio()

        public void PlayClip(AudioSource source)
        {
            if (!source) return;
            _lastSource = source;

            AudioManager.MarkBusy(source);

            void DoPlay(AudioClip clip)
            {
                source.clip = clip;
                source.volume = volume;
                source.loop = isLoop;
                source.Play();
                AudioManager.Instance.StartCoroutine(AudioManager.ReleaseBusyNextFrame(source));
            }

            if (_cacheOperation.IsValid())
            {
                if (_cacheOperation.IsDone)
                {
                    DoPlay(_cacheOperation.Result);
                }
                else
                {
                    _cacheOperation.Completed += h => DoPlay(h.Result);
                }
            }
            else
            {
                _cacheOperation = clipRef.LoadAssetAsync<AudioClip>();
                _cacheOperation.Completed += h => DoPlay(h.Result);
            }
        }

        public void StopAudio()
        {
            if (_lastSource != null && _lastSource.isPlaying)
                _lastSource.Stop();
        }
    }
}