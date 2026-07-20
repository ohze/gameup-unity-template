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
        private readonly Dictionary<string, int> _retryAttemptsByUnitId = new Dictionary<string, int>();

        protected const int LoadRetryExponentCap = 6;

        protected BaseAdFormat() { }
        
        protected BaseAdFormat(AdUnitConfig config, AdUnitType adType, string networkName)
        {
            _config = config;
            _adType = adType;
            _networkName = networkName;
        }

        public virtual void Load(string where = null)
        {
            foreach (EcpmFloor floor in _config.GetActiveFloors())
            {
                LoadByFloor(where, floor);
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

            int currentRetry = _retryAttemptsByUnitId.TryGetValue(unitId, out int attempts) ? attempts : 0;
            currentRetry++;
            _retryAttemptsByUnitId[unitId] = currentRetry;

            float retryDelay = (float)Math.Pow(2, Math.Min(LoadRetryExponentCap, currentRetry));
            OnAdLoadFailed?.Invoke(unitId, where);
            
            LogTrace($"load_failed_retry_{floor}", unitId, where, $"delay={retryDelay}s, error={error}");
            MainThreadDispatcher.Enqueue(() =>
            {
                TimerHelper.Schedule(retryDelay, () => LoadByFloor(where, floor)); 
            });
        }

        protected void HandleLoadSuccess(string unitId, string where)
        {
            _isLoadingByUnitId[unitId] = false;
            _retryAttemptsByUnitId[unitId] = 0;
            OnAdLoaded?.Invoke(where);
            LogTrace("load_success", unitId, where);
        }
        
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
    }
}