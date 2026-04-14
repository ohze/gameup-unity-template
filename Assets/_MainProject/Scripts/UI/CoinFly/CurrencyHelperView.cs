using System;
using System.Collections;
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

        private float _currentCurrency;
        private float _addStartCurrency;
        private float _addTargetCurrency;
        private int _totalAddCallbacks;
        private int _currentAddCallbackIndex;
        private bool _isAddAnimating;
        private Action<float> _onAddStepCallback;
        private Action<float> _onAddCompletedCallback;

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
            if (!particleImage)
            {
                return;
            }

            particleImage.onAnyParticleFinished.RemoveListener(OnAnyParticleFinished);
            particleImage.onLastParticleFinished.RemoveListener(OnLastParticleFinished);
        }

        #region Currency
        [Button]
        public void SetCurrency(float currency)
        {
            _currentCurrency = currency;
            RefreshCurrencyText();
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
            return _currentCurrency;
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
                onCompleted?.Invoke(_currentCurrency);
                return;
            }

            InitEffect();
            ConfigureEmitterSource(fromTransform);

            if (particleImage)
            {
                _isAddAnimating = false;
                particleImage.Stop(true);
            }

            _isAddAnimating = true;
            _addStartCurrency = _currentCurrency;
            _addTargetCurrency = _currentCurrency + amount;
            _totalAddCallbacks = Mathf.Max(1, effectCount);
            _currentAddCallbackIndex = 0;
            _onAddStepCallback = onStepUpdated;
            _onAddCompletedCallback = onCompleted;

            if (particleImage)
            {
                particleImage.rateOverTime = _totalAddCallbacks;
                particleImage.loop = false;
                particleImage.duration = 1f;
                particleImage.startDelay = 0f;
                particleImage.Play();
            }
            else
            {
                CompleteAddAnimationWithoutParticle();
            }
        }

        [Button]
        public void SubtractCurrency(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _isAddAnimating = false;
            _currentCurrency -= amount;
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

            currencyText.text = $"{_currentCurrency.FormatMoney()}";
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
            if (!_isAddAnimating)
            {
                return;
            }

            _currentAddCallbackIndex++;
            var progress = Mathf.Clamp01(_currentAddCallbackIndex / (float)_totalAddCallbacks);
            var nextCurrency = Mathf.Lerp(_addStartCurrency, _addTargetCurrency, progress);
            var delta = Mathf.Max(0f, nextCurrency - _currentCurrency);
            _currentCurrency = nextCurrency;
            RefreshCurrencyText();

            if (delta > 0f)
            {
                SpawnDeltaText($"+{delta.FormatMoney()}", addDeltaColor);
            }

            _onAddStepCallback?.Invoke(_currentCurrency);
        }

        private void OnLastParticleFinished()
        {
            if (!_isAddAnimating)
            {
                return;
            }

            _currentCurrency = _addTargetCurrency;
            RefreshCurrencyText();
            _onAddCompletedCallback?.Invoke(_currentCurrency);
            ResetAddState();
        }

        private void CompleteAddAnimationWithoutParticle()
        {
            _currentCurrency = _addTargetCurrency;
            RefreshCurrencyText();
            SpawnDeltaText($"+{(_addTargetCurrency - _addStartCurrency).FormatMoney()}", addDeltaColor);
            _onAddStepCallback?.Invoke(_currentCurrency);
            _onAddCompletedCallback?.Invoke(_currentCurrency);
            ResetAddState();
        }

        private void ResetAddState()
        {
            _isAddAnimating = false;
            _addStartCurrency = 0f;
            _addTargetCurrency = 0f;
            _totalAddCallbacks = 0;
            _currentAddCallbackIndex = 0;
            _onAddStepCallback = null;
            _onAddCompletedCallback = null;
        }

        private void SpawnDeltaText(string content, Color color)
        {
            if (!currencyDeltaTextPrefab || !GUPoolers.Instance || !currencyText)
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