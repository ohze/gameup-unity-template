using System;
using System.Collections.Generic;
using System.Linq;
using GameUp.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GameUpCore.Runtime.LocalTracking
{
    public class LocalLevelTracking : Singleton<LocalLevelTracking>, ILevelTracking
    {
        private const string PlayerLevelKey = "p_Level";
        [NonSerialized]
        private List<PlayerLevelTracking> _playerLevels;
        private float _timeStartLevel;
        private PlayerLevelTracking _currentPlayerLevel;

        private void Initialize()
        {
            _playerLevels ??= LocalStorageUtils.HasKey(PlayerLevelKey)
                ? LocalStorageUtils.GetObject<List<PlayerLevelTracking>>(PlayerLevelKey)
                : new List<PlayerLevelTracking>();
        }

        private void SavePlayerLevels()
        {
            LocalStorageUtils.SetObject(PlayerLevelKey, _playerLevels);
        }
        
        public void StartLevel(int level)
        {
            Initialize();
            _timeStartLevel = Time.realtimeSinceStartup;
            if (_playerLevels.Any(s => s.level == level))
            {
                _currentPlayerLevel = _playerLevels.First(s => s.level == level);
                _currentPlayerLevel.startAttempt++;
                SavePlayerLevels();
            }
            else
            {
                _currentPlayerLevel = new PlayerLevelTracking
                {
                    level = level,
                    startAttempt = 1,
                    levelDuration = -1,
                    reason = ""
                };
                _playerLevels.Add(_currentPlayerLevel);
                SavePlayerLevels();
            }
        }

        public void WinLevel(int level)
        {
            var currentTime = Time.realtimeSinceStartup;
            var duration = currentTime - _timeStartLevel;
            _currentPlayerLevel.levelDuration = duration;
            SavePlayerLevels();
        }

        public void LoseLevel(int level, string reason)
        {
            var currentTime = Time.realtimeSinceStartup;
            var duration = currentTime - _timeStartLevel;
            if (duration > _currentPlayerLevel.levelDuration)
            {
                _currentPlayerLevel.levelDuration = duration;
            }
            _currentPlayerLevel.reason = reason;
            SavePlayerLevels();
        }

        public void GenerateFakeData()
        {
            _playerLevels = new List<PlayerLevelTracking>();
            for (int i = 0; i < 100; i++)
            {
                _playerLevels.Add(new PlayerLevelTracking
                {
                    level = i + 1,
                    startAttempt = Random.Range(1,8),
                    levelDuration = Random.Range(80f, 800f),
                });
            }
            SavePlayerLevels();
        }
    }

    [Serializable]
    public class PlayerLevelTracking
    {
        public int level;
        public int startAttempt;
        public string reason;
        public float levelDuration;
    }
}