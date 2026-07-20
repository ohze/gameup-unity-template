using System;
using System.Collections.Generic;
using GameUp.Core;
using UnityEngine;

namespace GameUp.SDK
{
    public class AdCappingManager : MonoSingleton<AdCappingManager>
    {
        private readonly Dictionary<AdUnitType, float> _cappingLimits = new Dictionary<AdUnitType, float>();
        private readonly Dictionary<AdUnitType, float> _currentTimers = new Dictionary<AdUnitType, float>();
        
        private int _pauseRequests = 0;

        [SerializeField] private float defaultCappingTime = 45f;

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }
        
        private void Update()
        {
            if (_pauseRequests > 0) return;

            float dt = Time.unscaledDeltaTime;
            var keys = new List<AdUnitType>(_currentTimers.Keys);
            foreach (var key in keys)
            {
                _currentTimers[key] += dt;
            }
        }
        
        public void SetCappingLimit(AdUnitType groupId, float limit, float seconds)
        {
            _cappingLimits[groupId] = limit;
            _currentTimers.TryAdd(groupId, seconds);
        }

        public bool IsCappingReady(AdUnitType groupId = AdUnitType.Interstitial)
        {
            float limit = _cappingLimits.GetValueOrDefault(groupId, defaultCappingTime);
            float current = _currentTimers.GetValueOrDefault(groupId, 0f);
            GULogger.Error("GameUp", $"AdUnit: {groupId} current: {current} - limit: {limit}");
            return current >= limit;
        }

        public void ResetCapping(AdUnitType adUnitType)
        {
            var keys = new List<AdUnitType>(_currentTimers.Keys);
            foreach (var key in keys)
            {
                if (key == adUnitType)
                {
                    _currentTimers[key] = 0;
                }
            }
        }

        public void PauseAllCapping() => _pauseRequests++;
        public void ResumeAllCapping() { _pauseRequests--; if (_pauseRequests < 0) _pauseRequests = 0; }
        public bool IsAnyAdShowing => _pauseRequests > 0;
    }
}