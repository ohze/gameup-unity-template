using System.Collections;
using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// Manages game speed independently of Time.timeScale.
    /// </summary>
    public class TimeManager : MonoSingleton<TimeManager>
    {
        #region Constants

        private const string SaveKey = "TimeSpeedUp";
        private const float SpeedBase = 1f;
        private const float SpeedX1_5 = 1.5f;
        private const float SpeedX2 = 2f;
        private const float DefaultBoostDuration = 20f * 60f; // 20 minutes

        #endregion

        #region Serialized Fields

        [SerializeField] private float baseSpeed = SpeedBase;
        [SerializeField] private float boostDuration = DefaultBoostDuration;

        #endregion

        #region Signals

        /// <summary>Dispatched when boost countdown changes. Parameter = remaining seconds.</summary>
        public static readonly Signal<float> OnBoostCountdownChanged = new Signal<float>();

        /// <summary>Dispatched when game speed value changes.</summary>
        public static readonly Signal OnGameSpeedChanged = new Signal();

        #endregion

        #region State

        private Coroutine _countdownCoroutine;
        private float _remainingBoostTime;
        private bool _isX2Boost;

        #endregion

        #region Properties

        /// <summary>Current game speed multiplier. Consumers should multiply their delta by this.</summary>
        public static float GameSpeed
        {
            get => Instance._currentSpeed;
            set
            {
                if (Mathf.Approximately(Instance._currentSpeed, value)) return;
                Instance._currentSpeed = value;
                OnGameSpeedChanged.Dispatch();
            }
        }

        private float _currentSpeed = SpeedBase;

        /// <summary>Remaining seconds of the active boost. 0 if no boost active.</summary>
        public static float BoostTimeRemaining => Instance._remainingBoostTime;

        /// <summary>Whether a speed boost is currently active.</summary>
        public static bool IsBoostActive => Instance._remainingBoostTime > 0;

        /// <summary>The configured boost duration in seconds.</summary>
        public float BoostDuration => boostDuration;

        /// <summary>Current boost speed multiplier based on boost tier.</summary>
        private float BoostSpeed => _isX2Boost ? SpeedX2 : SpeedX1_5;

        #endregion

        #region Lifecycle

        private void Start()
        {
            LoadState();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sets the base game speed (non-boosted speed).
        /// </summary>
        public static void SetBaseSpeed(float speed)
        {
            Instance.baseSpeed = speed;
            if (!IsBoostActive)
                GameSpeed = speed;
        }

        /// <summary>
        /// Activates a x2 speed boost for the configured duration.
        /// Restarts the timer if a boost is already active.
        /// </summary>
        public static void ActivateBoost()
        {
            Instance.StartBoostInternal(SpeedX2);
        }

        /// <summary>
        /// Activates a speed boost with a custom multiplier for the configured duration.
        /// </summary>
        public static void ActivateBoost(float multiplier)
        {
            Instance.StartBoostInternal(multiplier);
        }

        /// <summary>
        /// Toggles x2 speed on/off. If currently at x2, resets to base.
        /// </summary>
        public static void ToggleX2Speed()
        {
            if (Mathf.Approximately(GameSpeed, SpeedX2))
            {
                StopBoost();
            }
            else
            {
                ActivateBoost();
            }
        }

        /// <summary>
        /// Immediately stops any active boost and resets to base speed.
        /// </summary>
        public static void StopBoost()
        {
            Instance.StopBoostInternal();
        }

        /// <summary>
        /// Pauses the boost countdown without resetting it.
        /// Speed returns to base while paused.
        /// </summary>
        public static void PauseBoost()
        {
            Instance.StopCountdown();
            GameSpeed = Instance.baseSpeed;
        }

        /// <summary>
        /// Resumes a paused boost if time is remaining.
        /// </summary>
        public static void ResumeBoost()
        {
            if (Instance._remainingBoostTime > 0)
                Instance.StartCountdown();
        }

        #endregion

        #region Internal

        private void StartBoostInternal(float multiplier)
        {
            _isX2Boost = Mathf.Approximately(multiplier, SpeedX2);
            _remainingBoostTime = boostDuration;

            GameSpeed = multiplier;
            OnBoostCountdownChanged.Dispatch(_remainingBoostTime);
            StartCountdown();
        }

        private void StopBoostInternal()
        {
            StopCountdown();
            ResetBoostState();
            SaveState();
        }

        private void ResetBoostState()
        {
            _remainingBoostTime = 0;
            _isX2Boost = false;
            GameSpeed = baseSpeed;
            OnBoostCountdownChanged.Dispatch(0);
        }

        private void StartCountdown()
        {
            StopCountdown();

            if (_remainingBoostTime > 0)
            {
                GameSpeed = BoostSpeed;
                _countdownCoroutine = StartCoroutine(CountdownRoutine());
            }
            else
            {
                GameSpeed = baseSpeed;
            }
        }

        private void StopCountdown()
        {
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }
        }

        private IEnumerator CountdownRoutine()
        {
            var wait = new WaitForSecondsRealtime(1f);

            while (_remainingBoostTime > 0)
            {
                yield return wait;
                _remainingBoostTime = Mathf.Max(0, _remainingBoostTime - 1f);
                OnBoostCountdownChanged.Dispatch(_remainingBoostTime);
                SaveState();
            }

            // Boost expired
            _isX2Boost = false;
            GameSpeed = baseSpeed;
            _countdownCoroutine = null;
        }

        #endregion

        #region Persistence

        private void LoadState()
        {
            _remainingBoostTime = LocalStorageUtils.GetFloat($"{SaveKey}_Time", 0f);
            _isX2Boost = LocalStorageUtils.GetBoolean($"{SaveKey}_IsX2", false);

            if (_remainingBoostTime > 0)
            {
                GameSpeed = BoostSpeed;
                OnBoostCountdownChanged.Dispatch(_remainingBoostTime);
                StartCountdown();
            }
            else
            {
                ResetBoostState();
                SaveState();
            }
        }

        private void SaveState()
        {
            LocalStorageUtils.SetFloat($"{SaveKey}_Time", _remainingBoostTime);
            LocalStorageUtils.SetBoolean($"{SaveKey}_IsX2", _isX2Boost);
        }

        #endregion
    }
}