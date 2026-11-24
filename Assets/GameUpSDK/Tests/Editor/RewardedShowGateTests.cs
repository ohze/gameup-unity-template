using System;
using NUnit.Framework;

namespace GameUp.SDK.Tests
{
    public class RewardedShowGateTests
    {
        private int _successCount;
        private int _failCount;
        private int _resolvedCount;
        private Action _pendingTimer;
        private float _scheduledDelay;

        [SetUp]
        public void SetUp()
        {
            _successCount = 0;
            _failCount = 0;
            _resolvedCount = 0;
            _pendingTimer = null;
            _scheduledDelay = -1f;
        }

        private RewardedShowGate CreateGate() =>
            new RewardedShowGate("test", OnSuccess, OnFail, OnResolved, CaptureSchedule);

        private void OnSuccess() => _successCount++;
        private void OnFail() => _failCount++;
        private void OnResolved() => _resolvedCount++;

        private void CaptureSchedule(float delay, Action callback)
        {
            _scheduledDelay = delay;
            _pendingTimer = callback;
        }

        private void FireTimer()
        {
            var timer = _pendingTimer;
            _pendingTimer = null;
            timer?.Invoke();
        }

        [Test]
        public void RewardBeforeClose_ResolvesSuccessOnClose()
        {
            var gate = CreateGate();

            gate.MarkEarned();
            Assert.AreEqual(0, _successCount, "Chưa đóng ad thì chưa được chốt");

            gate.MarkClosed();
            Assert.AreEqual(1, _successCount);
            Assert.AreEqual(0, _failCount);
            Assert.IsNull(_pendingTimer, "Đã có reward thì không cần chờ");
        }

        // Regression: trước fix, close tới trước reward → onFail ("Ads not available") dù đã xem hết.
        [Test]
        public void RewardAfterClose_WithinGrace_ResolvesSuccessOnly()
        {
            var gate = CreateGate();

            gate.MarkClosed();
            Assert.AreEqual(0, _failCount, "Close chưa có reward không được báo fail ngay");
            Assert.AreEqual(RewardedShowGate.LateRewardGraceSeconds, _scheduledDelay);

            gate.MarkEarned();
            FireTimer();

            Assert.AreEqual(1, _successCount);
            Assert.AreEqual(0, _failCount);
            Assert.AreEqual(1, _resolvedCount);
        }

        [Test]
        public void CloseWithoutReward_GraceExpires_ResolvesFailOnly()
        {
            var gate = CreateGate();

            gate.MarkClosed();
            FireTimer();

            Assert.AreEqual(0, _successCount);
            Assert.AreEqual(1, _failCount);
            Assert.AreEqual(1, _resolvedCount);
        }

        // Regression AdMob: trước fix, reward muộn gọi onSuccess SAU khi đã gọi onFail.
        [Test]
        public void RewardAfterGraceExpired_IsIgnored()
        {
            var gate = CreateGate();

            gate.MarkClosed();
            FireTimer();
            gate.MarkEarned();

            Assert.AreEqual(0, _successCount);
            Assert.AreEqual(1, _failCount);
        }

        [Test]
        public void DisplayFailed_ResolvesFailImmediately_AndIgnoresLaterEvents()
        {
            var gate = CreateGate();

            gate.MarkDisplayFailed();
            gate.MarkEarned();
            gate.MarkClosed();

            Assert.AreEqual(0, _successCount);
            Assert.AreEqual(1, _failCount);
            Assert.AreEqual(1, _resolvedCount);
            Assert.IsNull(_pendingTimer);
        }

        [Test]
        public void DuplicateCloseAndReward_ResolveOnce()
        {
            var gate = CreateGate();

            gate.MarkEarned();
            gate.MarkEarned();
            gate.MarkClosed();
            gate.MarkClosed();

            Assert.AreEqual(1, _successCount);
            Assert.AreEqual(0, _failCount);
            Assert.AreEqual(1, _resolvedCount);
            Assert.IsTrue(gate.IsResolved);
        }
    }
}
