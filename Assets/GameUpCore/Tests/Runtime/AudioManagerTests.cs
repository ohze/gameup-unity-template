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
        public IEnumerator PreloadAudio_IdentityWithoutClips_CompletesOnceImmediately()
        {
            var completedCount = 0;
            AudioManager.PreloadAudio(_identity, () => completedCount++);

            Assert.AreEqual(1, completedCount, "Không có clip thì callback phải chạy ngay, đúng một lần");
            Assert.IsFalse(AudioManager.IsAudioReady(_identity), "Identity không có clip không được coi là sẵn sàng phát");

            yield return null;
            Assert.AreEqual(1, completedCount);
        }

        [UnityTest]
        public IEnumerator PreloadAudio_ListWithNullEntries_CompletesOnce()
        {
            var completedCount = 0;
            AudioManager.PreloadAudio(new[] { _identity, null, _identity }, () => completedCount++);

            yield return null;
            Assert.AreEqual(1, completedCount);
        }

        [UnityTest]
        public IEnumerator PreloadAudio_UnknownName_StillInvokesCompleted()
        {
            var completed = false;
            // Error có thể bị tắt theo cấu hình GULogger nên không Expect cụ thể.
            LogAssert.ignoreFailingMessages = true;

            AudioManager.PreloadAudio("__khong_ton_tai__", () => completed = true);

            Assert.IsTrue(completed, "Preload lỗi vẫn phải gọi callback để bootstrap không treo");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreloadAudio_NullInputs_InvokeCompleted()
        {
            var completedCount = 0;
            AudioManager.PreloadAudio((AudioIdentity)null, () => completedCount++);
            AudioManager.PreloadAudio((AudioIdentity[])null, () => completedCount++);

            Assert.AreEqual(2, completedCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReleaseAudio_NotPreloaded_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AudioManager.ReleaseAudio(_identity));
            Assert.DoesNotThrow(() => AudioManager.ReleaseAudio((AudioIdentity)null));
            Assert.DoesNotThrow(() => AudioManager.ReleaseAudio("__khong_ton_tai__"));

            yield return null;
        }

        [UnityTest]
        public IEnumerator UnloadAudioData_NotPreloaded_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AudioManager.UnloadAudioData(_identity));
            Assert.DoesNotThrow(() => AudioManager.UnloadAudioData((AudioIdentity)null));
            Assert.DoesNotThrow(() => AudioManager.UnloadAudioData("__khong_ton_tai__"));

            yield return null;
        }

        [UnityTest]
        public IEnumerator ReleaseUnusedAudio_NothingCached_ReleasesNothingAndIsRepeatable()
        {
            AudioManager.StopAllSfx();

            Assert.AreEqual(0, AudioManager.ReleaseUnusedAudio());
            Assert.AreEqual(0, AudioManager.ReleaseUnusedAudio(), "Gọi lần hai không được lỗi hay nhả trùng");

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
