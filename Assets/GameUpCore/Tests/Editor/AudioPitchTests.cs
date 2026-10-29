using NUnit.Framework;
using UnityEngine;

namespace GameUp.Core.Tests
{
    public class AudioPitchTests
    {
        private static readonly float[] Randoms = { 0f, 0.01f, 0.25f, 0.5f, 0.73f, 0.99f, 1f };

        [Test]
        public void CalculatePitch_ZeroRange_ReturnsPitch([Values(0.5f, 1f, 1.2f, 2f)] float pitch)
        {
            foreach (var r in Randoms)
            {
                Assert.AreEqual(pitch, AudioIdentity.CalculatePitch(pitch, 0f, r));
            }
        }

        [Test]
        public void CalculatePitch_StaysWithinRange([Values(0.8f, 1f, 1.5f)] float pitch,
            [Values(0.05f, 0.1f, 0.3f)] float range)
        {
            foreach (var r in Randoms)
            {
                var result = AudioIdentity.CalculatePitch(pitch, range, r);
                Assert.GreaterOrEqual(result, pitch - range - 1e-5f);
                Assert.LessOrEqual(result, pitch + range + 1e-5f);
            }
        }

        [Test]
        public void CalculatePitch_RandomEnds_HitRangeEnds()
        {
            Assert.AreEqual(0.9f, AudioIdentity.CalculatePitch(1f, 0.1f, 0f), 1e-5f);
            Assert.AreEqual(1.1f, AudioIdentity.CalculatePitch(1f, 0.1f, 1f), 1e-5f);
            Assert.AreEqual(1f, AudioIdentity.CalculatePitch(1f, 0.1f, 0.5f), 1e-5f);
        }

        [Test]
        public void CalculatePitch_ClampsToSafeRange()
        {
            Assert.AreEqual(AudioIdentity.MinPitch, AudioIdentity.CalculatePitch(0.15f, 1f, 0f));
            Assert.AreEqual(AudioIdentity.MaxPitch, AudioIdentity.CalculatePitch(2.9f, 1f, 1f));
        }

        [Test]
        public void CalculatePitch_OutOfRangeRandom01_IsClamped()
        {
            Assert.AreEqual(0.9f, AudioIdentity.CalculatePitch(1f, 0.1f, -5f), 1e-5f);
            Assert.AreEqual(1.1f, AudioIdentity.CalculatePitch(1f, 0.1f, 5f), 1e-5f);
        }

        [Test]
        public void CalculatePitch_NegativeRange_TreatedAsZero()
        {
            Assert.AreEqual(1f, AudioIdentity.CalculatePitch(1f, -0.2f, 0f));
        }

        [Test]
        public void CalculatePitch_ZeroInitializedPitch_TreatedAsOne()
        {
            Assert.AreEqual(1f, AudioIdentity.CalculatePitch(0f, 0f, 0.3f));
        }

        [Test]
        public void ClampPitch_ClampsOverride()
        {
            Assert.AreEqual(AudioIdentity.MinPitch, AudioIdentity.ClampPitch(-1f));
            Assert.AreEqual(AudioIdentity.MaxPitch, AudioIdentity.ClampPitch(10f));
            Assert.AreEqual(1.3f, AudioIdentity.ClampPitch(1.3f));
        }

        [Test]
        public void DefaultIdentity_PitchIsOne()
        {
            var identity = ScriptableObject.CreateInstance<AudioIdentity>();
            try
            {
                Assert.AreEqual(1f, identity.pitch);
                Assert.AreEqual(0f, identity.randomPitchRange);
                Assert.AreEqual(1f, identity.BasePitch);
                for (var i = 0; i < 20; i++) Assert.AreEqual(1f, identity.EvaluatePitch());
            }
            finally
            {
                Object.DestroyImmediate(identity);
            }
        }

        [Test]
        public void DefaultBaseAudio_PitchIsOne()
        {
            var audio = new BaseAudio();
            Assert.AreEqual(1f, audio.pitch);
            Assert.AreEqual(0f, audio.randomPitchRange);
        }
    }
}
