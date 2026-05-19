using System;
using System.Collections;
using UnityEngine;
using GameUp.Core;
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
using GoogleMobileAds.Ump.Api;
#endif
#if UNITY_IOS
using Unity.Advertisement.IosSupport;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// Handles privacy workflow: ATT (iOS 14.5+) then UMP consent form.
    /// </summary>
    public class PrivacyManager : MonoSingleton<PrivacyManager>
    {
        private bool _started;
        private bool _completed;
        private bool _consentGranted = true;
        private Action<bool> _onCompleted;

        public bool IsCompleted => _completed;
        public bool ConsentGranted => _consentGranted;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public void BeginPrivacyFlow(Action<bool> onCompleted = null)
        {
            if (onCompleted != null)
                _onCompleted += onCompleted;

            if (_completed)
            {
                _onCompleted?.Invoke(_consentGranted);
                _onCompleted = null;
                return;
            }

            if (_started)
                return;

            _started = true;
            StartCoroutine(RunPrivacyFlowCoroutine());
        }

        private IEnumerator RunPrivacyFlowCoroutine()
        {
#if UNITY_IOS && !UNITY_EDITOR
            yield return RequestAttCoroutine();
#endif

            yield return RequestUmpCoroutine();

            _completed = true;

            // QUAN TRá»ŒNG: Gá»i hÃ m nÃ y Ä‘á»ƒ update tráº¡ng thÃ¡i cho cÃ¡c Ad Network
            // Giáº£ sá»­ AdsManager cá»§a báº¡n lÃ  Singleton
            if (AdsManager.Instance != null)
            {
                AdsManager.Instance.SetAfterCheckGDPR(_consentGranted);
            }

            _onCompleted?.Invoke(_consentGranted);
            _onCompleted = null;
        }

#if UNITY_IOS && !UNITY_EDITOR
        private IEnumerator RequestAttCoroutine()
        {
            // ATT dialog is more reliable when app is active and one frame has passed.
            while (!Application.isFocused)
                yield return null;
            yield return null;

            ATTrackingStatusBinding.AuthorizationTrackingStatus status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
            if (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
            {
                ATTrackingStatusBinding.RequestAuthorizationTracking();
                const float timeout = 30f;
                float elapsed = 0f;
                while (elapsed < timeout)
                {
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                    status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
                    if (status != ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
                        break;
                }
            }

            GULogger.Log("GameUp", "PrivacyManager ATT finished. status=" + status);
        }
#endif

        private IEnumerator RequestUmpCoroutine()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
#if UNITY_IOS && !UNITY_EDITOR
            if(ATTrackingStatusBinding.GetAuthorizationTrackingStatus() == ATTrackingStatusBinding.AuthorizationTrackingStatus.DENIED)
            {
                _consentGranted = false;
                GULogger.Log("GameUp", "PrivacyManager ATT denied -> consent = false");
                yield break;
            }
#endif
            bool done = false;
            bool canRequestAds = true;

            var request = new ConsentRequestParameters();
            ConsentInformation.Update(request, error =>
            {
                if (error != null)
                {
                    GULogger.Warning("GameUp", "PrivacyManager UMP update failed: " + error.Message);
                    canRequestAds = false;
                    done = true;
                    return;
                }

                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null)
                        GULogger.Warning("GameUp", "PrivacyManager UMP form failed: " + formError.Message);

                    canRequestAds = ConsentInformation.CanRequestAds();
                    done = true;
                });
            });

            while (!done)
                yield return null;

            _consentGranted = canRequestAds;
            GULogger.Log("GameUp", "PrivacyManager UMP finished. consent=" + _consentGranted);
#else
            _consentGranted = true;
            yield break;
#endif
        }
    }
}
