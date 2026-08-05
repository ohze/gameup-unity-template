using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameUp.Core.Tests
{
    /// <summary>
    /// PlayMode test cho phần điều khiển phát/dừng. Không load clip thật (cần Addressables),
    /// chỉ kiểm tra vòng đời handle và bảng theo dõi lần phát.
    /// </summary>
    public class AudioManagerTests
    {
        private AudioIdentity _identity;

        [SetUp]
        public void SetUp()
        {
            _identity = ScriptableObject.CreateInstance<AudioIdentity>();
            _identity.name = "__gu_test_identity";
            _identity.category = AudioCategory.Sfx;
            _identity.volume = 1f;

            AudioSetting.Instance.IsSoundOn.Value = true;
        }

        [TearDown]
        public void TearDown()
        {
            AudioManager.StopAllSfx();
            if (_identity) Object.DestroyImmediate(_identity);
        }

        [UnityTest]
        public IEnumerator Play_WithoutClips_ReturnsInvalidHandle()
        {
            var handle = AudioManager.PlayAudio(_identity);
            yield return null;

            Assert.IsFalse(handle.IsValid, "Identity không có clip thì không được coi là đang phát");
        }

        [UnityTest]
        public IEnumerator Play_WhenSoundOff_ReturnsInvalidHandle()
        {
            AudioSetting.Instance.IsSoundOn.Value = false;

            var handle = AudioManager.PlayAudio(_identity);
            yield return null;

            Assert.IsFalse(handle.IsValid);

            AudioSetting.Instance.IsSoundOn.Value = true;
        }

        [UnityTest]
        public IEnumerator StopAudio_ByIdentity_ClearsPlayingState()
        {
            AudioManager.StopAudio(_identity);
            yield return null;

            Assert.IsFalse(AudioManager.IsPlaying(_identity));
        }

        [UnityTest]
        public IEnumerator InvalidHandle_StopIsNoOp()
        {
            var handle = AudioHandle.None;

            Assert.IsFalse(handle.IsValid);
            Assert.IsFalse(handle.IsPlaying);
            Assert.DoesNotThrow(() => handle.Stop());

            yield return null;
        }

        [UnityTest]
        public IEnumerator StopAudio_ByUnknownName_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AudioManager.StopAudio("__khong_ton_tai__"));
            Assert.IsFalse(AudioManager.IsPlaying("__khong_ton_tai__"));

            yield return null;
        }
    }
}
