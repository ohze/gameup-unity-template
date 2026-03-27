using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace GameUp.Core.UI
{
    public class FlashPanelMediator : MonoSingleton<FlashPanelMediator>
    {
        private const string LogTag = nameof(FlashPanelMediator);

        [SerializeField] private bool isUseFlash;
        [SerializeField] private List<Material> imMats = new List<Material>();
        [SerializeField] private Image flashIm = null!;
        [SerializeField] private float timeFadeIn = 0.25f;
        private Material _currentMat;
        private Sequence _seq;
        private Coroutine _waitCoroutine;
        private TweenCallback _callback;
        private TweenCallback _completeCallback;
        private static readonly int _Progress = Shader.PropertyToID("_Progress");

        private void RunFlash()
        {
            if (!CanUseFlash())
            {
                InvokeAndClearCompleteCallback();
                flashIm?.Hide();
                gameObject.Hide();
            }
            else
            {
                flashIm.Show();
                _seq?.Kill();
                SetMatProgress(0);
                _seq = DOTween.Sequence()
                    .Append(DoMatProgress(1, timeFadeIn))
                    .OnComplete(() =>
                    {
                        flashIm.Hide();
                        InvokeAndClearCompleteCallback();
                        gameObject.Hide();
                    }).SetEase(Ease.Linear);
            }
        }

        private IEnumerator IEWaitWithPredicate(Func<bool> predicate)
        {
            if (CanUseFlash())
            {
                SetMatProgress(0);
                yield return null;
            }

            yield return null;
            InvokeAndClearCallback();
            while (!predicate())
            {
                yield return null;
            }

            RunFlash();
        }

        private FlashPanelMediator ShowFlash(Func<bool> predicate)
        {
            gameObject.Show();
            if (CanUseFlash())
            {
                flashIm.Show();
                _currentMat = imMats[Random.Range(0, imMats.Count)];
                flashIm.material = _currentMat;
                _seq?.Kill();
            }

            if (_waitCoroutine != null)
            {
                StopCoroutine(_waitCoroutine);
            }

            _waitCoroutine = StartCoroutine(IEWaitWithPredicate(predicate));
            return this;
        }

        private void SetMatProgress(float v)
        {
            _currentMat?.SetFloat(_Progress, v);
        }

        private Tween DoMatProgress(float destination, float duration)
        {
            var current = _currentMat != null ? _currentMat.GetFloat(_Progress) : 0f;
            return DOVirtual.Float(current, destination, duration, SetMatProgress).SetEase(Ease.Linear);
        }

        private void OnDisable()
        {
            _seq?.Kill();
            if (_waitCoroutine != null)
            {
                StopCoroutine(_waitCoroutine);
                _waitCoroutine = null;
            }

            _callback = null;
            _completeCallback = null;
        }


        public static FlashPanelMediator OpenWithPredicate(
            Func<bool> predicate,
            TweenCallback callBack = null,
            TweenCallback completeCallBack = null,
            bool isUseFlash = false)
        {
            Instance._callback = callBack;
            Instance._completeCallback = completeCallBack;
            Instance.isUseFlash = isUseFlash;
            return Instance.ShowFlash(predicate ?? (() => true));
        }

        [Button]
        public void Test()
        {
            OpenWithPredicate(() => true, () => GULogger.Log(LogTag, $"Test"));
        }

        private bool CanUseFlash()
        {
            return isUseFlash && flashIm != null && imMats != null && imMats.Count > 0;
        }

        private void InvokeAndClearCallback()
        {
            _callback?.Invoke();
            _callback = null;
        }

        private void InvokeAndClearCompleteCallback()
        {
            _completeCallback?.Invoke();
            _completeCallback = null;
        }
    }
}