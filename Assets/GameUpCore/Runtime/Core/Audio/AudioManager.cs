using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GameUp.Core
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioManager : MonoSingleton<AudioManager>
    {
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private List<AudioInfoWithType> audioInfos = new();
        [SerializeField, Min(1)] private int maxSource = 16;

        private readonly List<AudioSource> _sources = new();
        private readonly HashSet<AudioSource> _busySources = new(); // nguồn đang "reserve" trong khi loading
        private readonly Dictionary<AudioClipType, AudioInfoWithType> _audioInfoLookup = new();

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

            RebuildAudioInfoLookup();
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

        private void OnValidate()
        {
            foreach (var info in audioInfos) info.SetName();
            RebuildAudioInfoLookup();
            musicSource ??= GetComponent<AudioSource>();
            GameUtils.SaveAssets(this);
        }

        private void RebuildAudioInfoLookup()
        {
            _audioInfoLookup.Clear();
            for (int i = 0; i < audioInfos.Count; i++)
            {
                var info = audioInfos[i];
                if (info == null) continue;
                var type = info.type;
                if (type == AudioClipType.None) continue;
                if (_audioInfoLookup.ContainsKey(type)) continue;
                _audioInfoLookup.Add(type, info);
            }
        }

        private static bool TryGetAudioInfo(AudioClipType type, out AudioInfoWithType info)
        {
            return Instance._audioInfoLookup.TryGetValue(type, out info);
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

        /// <summary> Phát one-shot, không loop. </summary>
        public static void PlayAudio(AudioClipType type)
        {
            if (type == AudioClipType.None) return;
            if (!AudioSetting.Instance.IsSoundOn.Value) return;

            var source = GetSource();
            if (!source) return;

            if (!TryGetAudioInfo(type, out var info)) return;
            if (info.clipReferences.Count == 0) return;

            info.PlayClip(source, forceLoop: false);
        }

        /// <summary> Phát loop, chỉ dừng khi gọi StopAudio(type). </summary>
        public static void PlayAudioLoop(AudioClipType type)
        {
            if (type == AudioClipType.None) return;
            if (!AudioSetting.Instance.IsSoundOn.Value) return;

            if (!TryGetAudioInfo(type, out var info)) return;
            if (info.clipReferences.Count == 0) return;

            StopAudio(type);
            var source = GetSource();
            if (!source) return;

            info.PlayClip(source, forceLoop: true);
        }

        public static void PlayAudio(BaseAudio clip)
        {
            if (!AudioSetting.Instance.IsSoundOn.Value) return;
            var source = GetSource();
            if (!source) return;
            clip.PlayClip(source);
        }

        public static void PlayMusic(AudioClipType type)
        {
            if (type == AudioClipType.None) return;

            if (!TryGetAudioInfo(type, out var info)) return;
            if (info.clipReferences.Count == 0) return;

            // Music source mặc định loop
            info.PlayClip(Instance.musicSource, forceLoop: true);
            // ⚠️ Music phải theo IsMusicOn, không theo IsSoundOn
            Instance.musicSource.mute = !AudioSetting.Instance.IsMusicOn.Value;
        }

        public static void StopAudio(AudioClipType type)
        {
            if (!TryGetAudioInfo(type, out var info)) return;
            info.StopAudio();
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
    public class AudioInfoWithType
    {
        public string name;
        public AudioClipType type;
        public List<AudioClipReference> clipReferences = new();
        [Range(0f, 1f)] public float volume = 1f;
        public bool isLoop;

        private readonly Dictionary<AudioClipReference, AsyncOperationHandle<AudioClip>> _cacheOperations = new();
        private AudioSource _lastSource;
        private AudioSource _loopSource; // source đang phát loop, dùng cho StopAudio()

        /// <summary>
        /// Phát một clip random từ danh sách. Giữ "busy" source trong lúc chờ load xong.
        /// </summary>
        public void PlayClip(AudioSource source, bool? forceLoop = null)
        {
            if (!source) return;
            _lastSource = source;
            if (forceLoop == true)
                _loopSource = source;

            // Reserve source trong lúc loading để GetSource() không cấp phát lại cho request khác
            AudioManager.MarkBusy(source);

            var clipRef = clipReferences.GetRandom();

            void DoPlay(AudioClip clip)
            {
                source.clip = clip;
                source.volume = volume;
                source.loop = forceLoop ?? isLoop;
                source.Play();
                // Nhả busy sang frame kế tiếp để đảm bảo isPlaying đã bật
                AudioManager.Instance.StartCoroutine(AudioManager.ReleaseBusyNextFrame(source));
            }

            if (_cacheOperations.TryGetValue(clipRef, out var handle))
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
                var req = clipRef.LoadAssetAsync<AudioClip>();
                _cacheOperations.Add(clipRef, req);
                req.Completed += h => DoPlay(h.Result);
            }
        }

        public void StopAudio()
        {
            if (_loopSource != null && _loopSource.isPlaying)
            {
                _loopSource.Stop();
                _loopSource.clip = null;
                AudioManager.ReleaseBusy(_loopSource);
                _loopSource = null;
            }
        }

        public void SetName() => name = type.ToString();
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