using System;
using System.Collections;
using System.Collections.Generic;
using AssetKits.ParticleImage;
using TMPro;
using GameUp.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GameUp.Core.UI.CoinFly
{
    public class CurrencyHelperView : MonoBehaviour
    {
        [Header("Currency")]
        [SerializeField] private TMP_Text currencyText;
        [SerializeField] private Image currencyIcon;

        [Header("Particle Effect")]
        [SerializeField] private ParticleImage particleImage;
        [SerializeField] private int effectCount = 12;
        [SerializeField] private bool autoInitOnAwake = true;

        [Header("Delta Text Effect")]
        [SerializeField] private TMP_Text currencyDeltaTextPrefab;
        [SerializeField] private Transform deltaTextParent;
        [SerializeField] private Color addDeltaColor = new Color(0.27f, 0.9f, 0.42f, 1f);
        [SerializeField] private Color subtractDeltaColor = new Color(1f, 0.24f, 0.24f, 1f);
        [SerializeField] private Vector2 deltaTextRiseOffset = new Vector2(0f, 45f);
        [SerializeField] private float deltaTextDuration = 0.45f;
        [SerializeField] private float deltaTextSpawnJitterX = 20f;

        /// <summary>Giá trị đang hiển thị trên text — chỉ là trạng thái trung gian của animation.</summary>
        private float _displayCurrency;
        /// <summary>Giá trị đích thật sự. Mọi Add/Subtract cộng dồn vào đây nên luôn khớp với data gốc.</summary>
        private float _targetCurrency;
        private int[] _addStepAmounts;
        private int _currentAddStepIndex;
        private bool _isAddAnimating;
        private readonly List<Action<float>> _addStepCallbacks = new List<Action<float>>();
        private readonly List<Action<float>> _addCompletedCallbacks = new List<Action<float>>();
        private readonly List<Action<float>> _callbackInvokeBuffer = new List<Action<float>>();

        private void Awake()
        {
            if (autoInitOnAwake)
            {
                InitEffect();
            }
        }

        private void OnEnable()
        {
            if (!particleImage)
            {
                return;
            }

            particleImage.onAnyParticleFinished.AddListener(OnAnyParticleFinished);
            particleImage.onLastParticleFinished.AddListener(OnLastParticleFinished);
        }

        private void OnDisable()
        {
            if (particleImage)
            {
                particleImage.onAnyParticleFinished.RemoveListener(OnAnyParticleFinished);
                particleImage.onLastParticleFinished.RemoveListener(OnLastParticleFinished);
            }

            CompleteAddAnimation();
        }

        #region Currency
        [Button]
        public void SetCurrency(float currency)
        {
            StopAddParticle();
            _targetCurrency = currency;
            CompleteAddAnimation();
        }

        public void SetCurrencyIcon(Sprite iconSprite)
        {
            if (!currencyIcon)
            {
                return;
            }

            currencyIcon.sprite = iconSprite;
            ApplyParticleStartSizeFromIcon();

            if (particleImage)
            {
                particleImage.sprite = iconSprite;
            }
        }

        public float GetCurrentCurrency()
        {
            return _targetCurrency;
        }

        public void SetEffectCount(int count)
        {
            effectCount = Mathf.Max(1, count);
        }

        [Button]
        public void AddCurrency(
            float amount,
            Transform fromTransform,
            Action<float> onStepUpdated = null,
            Action<float> onCompleted = null)
        {
            if (amount <= 0f)
            {
                onCompleted?.Invoke(_targetCurrency);
                return;
            }

            _targetCurrency += amount;

            if (onStepUpdated != null)
            {
                _addStepCallbacks.Add(onStepUpdated);
            }

            if (onCompleted != null)
            {
                _addCompletedCallbacks.Add(onCompleted);
            }

            var pending = _targetCurrency - _displayCurrency;
            if (pending <= 0f)
            {
                CompleteAddAnimation();
                return;
            }

            InitEffect();
            ConfigureEmitterSource(fromTransform);

            if (!particleImage || !isActiveAndEnabled)
            {
                SpawnDeltaText($"+{pending.FormatMoney()}", addDeltaColor);
                CompleteAddAnimation();
                return;
            }

            StopAddParticle();

            var steps = CalculateAddStepCount(pending);
            _addStepAmounts = ComputeStepAmounts(pending, steps);
            _currentAddStepIndex = 0;
            _isAddAnimating = true;

            particleImage.rateOverTime = steps;
            particleImage.loop = false;
            particleImage.duration = 1f;
            particleImage.startDelay = 0f;
            particleImage.Play();
        }

        [Button]
        public void SubtractCurrency(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _targetCurrency -= amount;
            _displayCurrency -= amount;
            RefreshCurrencyText();

            SpawnDeltaText($"-{amount.FormatMoney()}", subtractDeltaColor);
        }
        #endregion

        #region Effect

        [Button]
        public void InitEffect()
        {
            if (!particleImage || !currencyIcon)
            {
                return;
            }

            particleImage.sprite = currencyIcon.sprite;
            ApplyParticleStartSizeFromIcon();
            ConfigureAttractorTarget();
        }

        public void ConfigureEmitterSource(Transform fromTransform)
        {
            if (!particleImage)
            {
                return;
            }

            particleImage.emitterConstraintEnabled = fromTransform != null;
            particleImage.emitterConstraintTransform = fromTransform;
        }

        public void ConfigureAttractorTarget()
        {
            if (!particleImage || !currencyIcon)
            {
                return;
            }

            particleImage.attractorEnabled = true;
            particleImage.attractorTarget = currencyIcon.rectTransform;
        }

        public void PlayEffect(Transform fromTransform)
        {
            if (!particleImage)
            {
                return;
            }

            InitEffect();
            ConfigureEmitterSource(fromTransform);
            particleImage.Play();
        }

        public void StopEffect(bool clearParticles = true)
        {
            if (!particleImage)
            {
                return;
            }

            particleImage.Stop(clearParticles);
        }

        #endregion

        private void RefreshCurrencyText()
        {
            if (!currencyText)
            {
                return;
            }

            currencyText.text = $"{_displayCurrency.FormatMoney()}";
        }

        private void ApplyParticleStartSizeFromIcon()
        {
            if (!particleImage || !currencyIcon)
            {
                return;
            }

            var iconRect = currencyIcon.rectTransform.rect;
            var iconSize = Mathf.Max(iconRect.width, iconRect.height);
            particleImage.startSize = new SeparatedMinMaxCurve(iconSize);
        }

        private void OnAnyParticleFinished()
        {
            if (!_isAddAnimating || _addStepAmounts == null || _currentAddStepIndex >= _addStepAmounts.Length)
            {
                return;
            }

            var delta = _addStepAmounts[_currentAddStepIndex];
            _currentAddStepIndex++;
            _displayCurrency = Mathf.Min(_displayCurrency + delta, _targetCurrency);
            RefreshCurrencyText();

            if (delta > 0)
            {
                SpawnDeltaText($"+{((float)delta).FormatMoney()}", addDeltaColor);
            }

            InvokeCallbacks(_addStepCallbacks, false);
        }

        private void OnLastParticleFinished()
        {
            if (!_isAddAnimating)
            {
                return;
            }

            CompleteAddAnimation();
        }

        private int CalculateAddStepCount(float amount)
        {
            var wholeUnits = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(amount)));
            return Mathf.Clamp(wholeUnits, 1, Mathf.Max(1, effectCount));
        }

        private int[] ComputeStepAmounts(float amount, int steps)
        {
            var total = Mathf.Max(1, Mathf.RoundToInt(amount));
            steps = Mathf.Max(1, steps);
            var perStep = Mathf.CeilToInt(total / (float)steps);
            var stepAmounts = new int[steps];
            var allocated = 0;

            for (var i = 0; i < steps - 1; i++)
            {
                stepAmounts[i] = perStep;
                allocated += perStep;
            }

            stepAmounts[steps - 1] = Mathf.Max(0, total - allocated);
            return stepAmounts;
        }

        private void StopAddParticle()
        {
            _isAddAnimating = false;
            if (particleImage)
            {
                particleImage.Stop(true);
            }
        }

        private void CompleteAddAnimation()
        {
            _isAddAnimating = false;
            _addStepAmounts = null;
            _currentAddStepIndex = 0;
            _displayCurrency = _targetCurrency;
            RefreshCurrencyText();

            _addStepCallbacks.Clear();
            InvokeCallbacks(_addCompletedCallbacks, true);
        }

        private void InvokeCallbacks(List<Action<float>> callbacks, bool clearAfterCopy)
        {
            if (callbacks.Count == 0)
            {
                return;
            }

            _callbackInvokeBuffer.Clear();
            _callbackInvokeBuffer.AddRange(callbacks);
            if (clearAfterCopy)
            {
                callbacks.Clear();
            }

            var value = _displayCurrency;
            for (var i = 0; i < _callbackInvokeBuffer.Count; i++)
            {
                _callbackInvokeBuffer[i]?.Invoke(value);
            }

            _callbackInvokeBuffer.Clear();
        }

        private void SpawnDeltaText(string content, Color color)
        {
            if (!currencyDeltaTextPrefab || !GUPoolers.Instance || !currencyText || !isActiveAndEnabled)
            {
                return;
            }

            var parent = deltaTextParent ? deltaTextParent : currencyText.transform.parent;
            var textFx = GUPoolers.Instance.Spawn(currencyDeltaTextPrefab, parent);
            if (!textFx)
            {
                return;
            }

            textFx.text = content;
            textFx.color = color;

            var spawnedRect = textFx.rectTransform;
            spawnedRect.position = currencyText.rectTransform.position;
            spawnedRect.anchoredPosition += new Vector2(UnityEngine.Random.Range(-deltaTextSpawnJitterX, deltaTextSpawnJitterX), 0f);
            StartCoroutine(AnimateAndRecycleDeltaText(textFx));
        }

        private IEnumerator AnimateAndRecycleDeltaText(TMP_Text textFx)
        {
            var elapsed = 0f;
            var rect = textFx.rectTransform;
            var startPos = rect.anchoredPosition;
            var endPos = startPos + deltaTextRiseOffset;
            var startColor = textFx.color;
            var color = startColor;

            while (elapsed < deltaTextDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / deltaTextDuration);
                rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                color.a = Mathf.Lerp(startColor.a, 0f, t);
                textFx.color = color;
                yield return null;
            }

            color.a = startColor.a;
            textFx.color = color;
            GUPoolers.Instance.DeSpawn(textFx);
        }
    }
}
