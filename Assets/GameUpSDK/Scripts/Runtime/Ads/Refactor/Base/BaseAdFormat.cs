using GameUp.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameUp.SDK
{
    public abstract class BaseAdFormat : IAdFormat
    {
        protected readonly AdUnitConfig _config;
        protected readonly AdUnitType _adType;
        protected readonly string _networkName;
        
        public event Action<string> OnAdLoaded;
        public event Action<string, string> OnAdLoadFailed;
        public event Action<string> OnAdDisplayed;
        public event Action<string, string> OnAdDisplayFailed;
        public event Action<string> OnAdClosed;
        
        protected void NotifyAdDisplayed(string where) => OnAdDisplayed?.Invoke(where);
        protected void NotifyAdDisplayFailed(string where, string error) => OnAdDisplayFailed?.Invoke(where, error);
        protected void NotifyAdClosed(string where) => OnAdClosed?.Invoke(where);
        
        private readonly Dictionary<string, bool> _isLoadingByUnitId = new Dictionary<string, bool>();
        private readonly Dictionary<string, EcpmFloor> _floorByUnitId = new Dictionary<string, EcpmFloor>();

        /// <summary>Số lượt waterfall thất bại LIÊN TIẾP của mỗi placement. Đếm theo placement chứ không
        /// theo unitId vì backoff chỉ áp dụng khi đã rơi hết mọi tầng.</summary>
        private readonly Dictionary<string, int> _waterfallAttemptsByWhere = new Dictionary<string, int>();

        protected const int LoadRetryExponentCap = 6;

        protected BaseAdFormat() { }
        
        protected BaseAdFormat(AdUnitConfig config, AdUnitType adType, string networkName)
        {
            _config = config;
            _adType = adType;
            _networkName = networkName;
        }

        /// <summary>
        /// Bắt đầu waterfall: chỉ request tầng CAO NHẤT có ID hợp lệ. No-fill mới rơi xuống tầng dưới
        /// (xem <see cref="HandleLoadFailed"/>).
        ///
        /// Trước đây hàm này bắn cả 3 tầng cùng lúc: 3 request cho 1 impression, 2 ad thừa nằm chờ hết
        /// hạn. AdMob theo dõi match rate và show rate nên tỉ lệ thấp kéo eCPM và ưu tiên phân phối xuống.
        /// </summary>
        public virtual void Load(string where = null)
        {
            var floors = _config.GetActiveFloors();

            // Placement này đang có tầng nào bay dở thì để yên. Không có chốt này, gọi Load() lúc
            // waterfall đang ở tầng giữa (vd Show không sẵn ad nên gọi Load) sẽ mở thêm một request
            // ở tầng cao chạy song song — đúng thứ vừa bỏ đi.
            for (int i = 0; i < floors.Length; i++)
            {
                string id = _config.ResolveUnitId(_adType, where, floors[i]);
                if (!string.IsNullOrEmpty(id) && _isLoadingByUnitId.TryGetValue(id, out bool loading) && loading) return;
            }

            for (int i = 0; i < floors.Length; i++)
            {
                // Bỏ qua tầng chưa điền ID: nếu cứ gọi LoadByFloor thì nó return sớm, không có callback
                // fail nào bắn ra và waterfall đứng im vĩnh viễn.
                if (string.IsNullOrEmpty(_config.ResolveUnitId(_adType, where, floors[i]))) continue;

                LoadByFloor(where, floors[i]);
                return;
            }
        }

        public void LoadByFloor(string where, EcpmFloor floor)
        {
            string unitId = _config.ResolveUnitId(_adType, where, floor);
            if (string.IsNullOrEmpty(unitId)) return;

            if (_isLoadingByUnitId.TryGetValue(unitId, out bool loading) && loading)
            {
                return;
            }

            _isLoadingByUnitId[unitId] = true;
            _floorByUnitId[unitId] = floor;
            LogTrace($"request_{floor}", unitId, where);
            
            RequestAdInternal(unitId, where, floor);
        }

        public void LoadAll()
        {
            if (_config == null) return;
            var placements = _config.GetAllPlacements();
            foreach (var placement in placements)
            {
                Load(placement);
            }
        }

        public abstract bool IsAvailable(string where = null);

        protected abstract void RequestAdInternal(string unitId, string where, EcpmFloor floor);

        protected void HandleLoadFailed(string unitId, string where, EcpmFloor floor, string error)
        {
            _isLoadingByUnitId[unitId] = false;
            OnAdLoadFailed?.Invoke(where, error);

            // Bước RƠI TẦNG của waterfall: còn tầng thấp hơn thì thử ngay, không chờ backoff.
            var floors = _config.GetActiveFloors();
            int index = Array.IndexOf(floors, floor);
            if (index >= 0)
            {
                for (int i = index + 1; i < floors.Length; i++)
                {
                    string nextUnitId = _config.ResolveUnitId(_adType, where, floors[i]);
                    // Bỏ qua tầng trống hoặc trùng ID với tầng vừa lỗi: cả hai đều làm LoadByFloor
                    // return sớm (ID rỗng, hoặc dính cờ đang-load) và waterfall đứng im.
                    if (string.IsNullOrEmpty(nextUnitId) || nextUnitId == unitId) continue;

                    LogTrace($"load_failed_fallthrough_{floor}", unitId, where, $"next={floors[i]}, error={error}");
                    LoadByFloor(where, floors[i]);
                    return;
                }
            }

            // Đã rơi hết tầng → backoff rồi chạy lại waterfall từ đầu.
            string key = WaterfallKey(where);
            int attempts = (_waterfallAttemptsByWhere.TryGetValue(key, out int a) ? a : 0) + 1;
            _waterfallAttemptsByWhere[key] = attempts;

            float retryDelay = (float)Math.Pow(2, Math.Min(LoadRetryExponentCap, attempts));
            LogTrace($"load_failed_retry_{floor}", unitId, where, $"delay={retryDelay}s, attempt={attempts}, error={error}");
            MainThreadDispatcher.Enqueue(() =>
            {
                // Gọi Load() (virtual) chứ không phải floors[0]: banner và native banner override Load
                // để khoá riêng tầng All, restart bằng floors[0] sẽ kéo chúng lên tầng High không có ID.
                TimerHelper.Schedule(retryDelay, () => Load(where));
            });
        }

        protected void HandleLoadSuccess(string unitId, string where)
        {
            _isLoadingByUnitId[unitId] = false;
            _waterfallAttemptsByWhere[WaterfallKey(where)] = 0;
            OnAdLoaded?.Invoke(where);
            LogTrace("load_success", unitId, where);
        }

        private static string WaterfallKey(string where) => string.IsNullOrEmpty(where) ? "default" : where;
        
        protected void LogTrace(string phase, string unitId, string where, string extra = null)
        {
            var msg = $"[GameUp] {_adType} {phase} | where={where ?? "null"} | unitId={unitId ?? "null"}";
            if (!string.IsNullOrEmpty(extra)) msg += $" | {extra}";
            GULogger.Log(msg);
        }
        
        protected void TrackRevenue(string adUnitId, string placement, string adFormat, double revenue)
        {
            var data = new AdImpressionData
            {
                AdNetwork = _networkName,
                AdUnit = adUnitId,
                InstanceName = placement,
                AdFormat = adFormat,
                Revenue = revenue
            };
            MainThreadDispatcher.Enqueue(() => AdsEvent.RaiseImpressionDataReady(data));
        }

        protected string WhereByKey(string key) => _config.WhereByKey(_adType, key);

        /// <summary>Tầng eCPM đã dùng khi request unit này. Cho phép handler đăng ký một lần
        /// (không cần closure theo từng lần load) mà vẫn gắn đúng nhãn floor cho revenue.</summary>
        protected EcpmFloor FloorOf(string unitId) =>
            unitId != null && _floorByUnitId.TryGetValue(unitId, out var floor) ? floor : EcpmFloor.All;
    }
}