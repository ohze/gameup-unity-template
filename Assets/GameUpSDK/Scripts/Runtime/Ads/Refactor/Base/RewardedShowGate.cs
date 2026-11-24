using System;
using GameUp.Core;

namespace GameUp.SDK
{
    /// <summary>
    /// Chốt kết quả của MỘT lần show rewarded: gọi đúng một trong <c>onSuccess</c> / <c>onFail</c>,
    /// đúng một lần, bất kể SDK bắn "nhận thưởng" và "đóng ad" theo thứ tự nào.
    ///
    /// LevelPlay ghi rõ OnAdRewarded và OnAdClosed bất đồng bộ, không đảm bảo thứ tự; MAX và adapter
    /// mediation của AdMob cũng có lúc bắn reward SAU close. Trước đây kết quả được quyết ngay lúc close:
    /// MAX/LevelPlay gọi onFail và nuốt mất reward tới muộn (người chơi xem hết mà thấy toast lỗi),
    /// AdMob thì gọi onFail rồi sau đó lại gọi onSuccess (vừa báo lỗi vừa cộng thưởng).
    /// Giờ: close mà chưa có reward thì chờ thêm <see cref="LateRewardGraceSeconds"/> rồi mới kết luận.
    ///
    /// Mọi method phải được gọi trên main thread (đi qua <see cref="MainThreadDispatcher"/>).
    /// </summary>
    public sealed class RewardedShowGate
    {
        /// <summary>Thời gian (realtime) chờ reward tới muộn sau khi ad đã đóng.</summary>
        public const float LateRewardGraceSeconds = 2f;

        private const string Tag = "GameUp.Rewarded";

        private readonly string _where;
        private readonly Action _onSuccess;
        private readonly Action _onFail;
        private readonly Action _onResolved;
        private readonly Action<float, Action> _schedule;

        private bool _earned;
        private bool _closed;

        public bool IsResolved { get; private set; }

        /// <param name="onResolved">Chạy ngay sau khi đã chốt kết quả — chỗ để gỡ handler SDK.</param>
        public RewardedShowGate(string where, Action onSuccess, Action onFail, Action onResolved = null)
            : this(where, onSuccess, onFail, onResolved, TimerHelper.Schedule)
        {
        }

        /// <param name="schedule">Hẹn giờ (giây, callback). Tách ra để test không cần coroutine.</param>
        public RewardedShowGate(string where, Action onSuccess, Action onFail, Action onResolved,
            Action<float, Action> schedule)
        {
            _where = where;
            _onSuccess = onSuccess;
            _onFail = onFail;
            _onResolved = onResolved;
            _schedule = schedule;
        }

        public void MarkEarned()
        {
            if (IsResolved)
            {
                GULogger.Warning(Tag, $"Reward tới sau khi đã chốt kết quả, bỏ qua | where={_where}");
                return;
            }

            _earned = true;
            if (_closed) Resolve(true);
        }

        public void MarkClosed()
        {
            if (IsResolved || _closed) return;
            _closed = true;

            if (_earned)
            {
                Resolve(true);
                return;
            }

            _schedule(LateRewardGraceSeconds, ExpireGrace);
        }

        public void MarkDisplayFailed()
        {
            if (IsResolved) return;
            Resolve(false);
        }

        private void ExpireGrace()
        {
            if (IsResolved) return;
            Resolve(false);
        }

        private void Resolve(bool success)
        {
            IsResolved = true;
            _onResolved?.Invoke();
            if (success) _onSuccess?.Invoke();
            else _onFail?.Invoke();
        }
    }
}
