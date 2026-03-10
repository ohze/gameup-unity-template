using System;
using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// Utility timer supporting countdown and stopwatch modes.
    /// Call <see cref="Tick"/> each frame with deltaTime.
    /// </summary>
    [Serializable]
    public sealed class Timer
    {
        public enum Mode { Countdown, Stopwatch }

        [SerializeField] Mode _mode;
        [SerializeField] float _duration;

        float _elapsed;
        bool _isRunning;

        public float Duration => _duration;
        public float Elapsed => _elapsed;
        public float Remaining => Mathf.Max(0f, _duration - _elapsed);
        public float NormalizedProgress => _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 0f;
        public bool IsRunning => _isRunning;
        public bool IsCompleted => _mode == Mode.Countdown && _elapsed >= _duration;

        public event Action OnCompleted;
        public event Action<float> OnTick;

        public Timer(float duration, Mode mode = Mode.Countdown)
        {
            _duration = duration;
            _mode = mode;
        }

        public void Start()
        {
            _elapsed = 0f;
            _isRunning = true;
        }

        public void Stop()
        {
            _isRunning = false;
        }

        public void Pause() => _isRunning = false;

        public void Resume() => _isRunning = true;

        public void Reset()
        {
            _elapsed = 0f;
            _isRunning = false;
        }

        /// <summary>Call each frame. Pass deltaTime from TimeManager or Time.deltaTime.</summary>
        public void Tick(float deltaTime)
        {
            if (!_isRunning) return;

            _elapsed += deltaTime;
            OnTick?.Invoke(_elapsed);

            if (_mode != Mode.Countdown || !(_elapsed >= _duration)) return;

            _elapsed = _duration;
            _isRunning = false;
            OnCompleted?.Invoke();
        }
    }
}
