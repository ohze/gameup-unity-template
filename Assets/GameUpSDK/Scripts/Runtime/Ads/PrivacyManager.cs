using System;
using System.Collections;
using System.Collections.Generic;
using GameUp.SDK;
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
    /// Kết quả luồng privacy. Hai tín hiệu này ĐỘC LẬP với nhau, đừng gộp làm một:
    /// một bên quyết định có được bắn request hay không, bên kia chỉ ảnh hưởng chất lượng ad.
    /// </summary>
    public readonly struct PrivacyResult
    {
        /// <summary>
        /// Có được phép bắn ad request không (UMP <c>CanRequestAds</c>).
        /// false = KHÔNG request ở bất kỳ mạng nào — chuỗi TCF do UMP ghi ra cũng ràng buộc
        /// MAX/LevelPlay, nên đẩy sang mạng khác không làm request hợp lệ hơn.
        /// Trường hợp này HIẾM: user từ chối personalized ads vẫn cho true.
        /// </summary>
        public readonly bool CanRequestAds;

        /// <summary>
        /// Được phép dùng định danh cho quảng cáo cá nhân hoá / attribution (ATT trên iOS + UMP).
        /// false chỉ làm ad rơi về non-personalized (eCPM thấp hơn) — KHÔNG phải lý do chặn ads.
        /// </summary>
        public readonly bool TrackingAllowed;

        public PrivacyResult(bool canRequestAds, bool trackingAllowed)
        {
            CanRequestAds = canRequestAds;
            TrackingAllowed = trackingAllowed;
        }
    }

    /// <summary>
    /// Luồng privacy: ATT (iOS 14.5+) rồi tới UMP consent form.
    /// ATT và GDPR là hai thứ độc lập — từ chối ATT KHÔNG miễn nghĩa vụ hiện form GDPR,
    /// nên UMP luôn được chạy bất kể kết quả ATT.
    /// </summary>
    public class PrivacyManager : MonoSingleton<PrivacyManager>
    {
        private bool _started;
        private bool _completed;
        private bool _canRequestAds = true;
        private bool _attAuthorized = true; // Android/Editor không có ATT → coi như cho phép
        private Action<PrivacyResult> _onCompleted;

        public bool IsCompleted => _completed;

        /// <summary>Xem <see cref="PrivacyResult.CanRequestAds"/>.</summary>
        public bool CanRequestAds => _canRequestAds;

        /// <summary>Xem <see cref="PrivacyResult.TrackingAllowed"/>.</summary>
        public bool TrackingAllowed => _attAuthorized && _canRequestAds;

        [Obsolete("Cờ cũ gộp ATT + lỗi mạng + CanRequestAds làm một. Dùng CanRequestAds (cổng chặn request) hoặc TrackingAllowed (personalized ads).")]
        public bool ConsentGranted => TrackingAllowed;

        public PrivacyResult Result => new PrivacyResult(CanRequestAds, TrackingAllowed);

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        public void BeginPrivacyFlow(Action<PrivacyResult> onCompleted = null)
        {
            if (onCompleted != null)
                _onCompleted += onCompleted;

            if (_completed)
            {
                _onCompleted?.Invoke(Result);
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

            // Luôn chạy UMP, kể cả khi ATT bị từ chối: user EEA từ chối ATT vẫn phải được
            // thấy form GDPR. Trước đây nhánh ATT-denied thoát sớm nên form không bao giờ hiện.
            yield return RequestUmpCoroutine();

            _completed = true;

            GULogger.Log("GameUp",
                $"PrivacyManager xong. canRequestAds={_canRequestAds}, trackingAllowed={TrackingAllowed}");

            _onCompleted?.Invoke(Result);
            _onCompleted = null;
        }

#if UNITY_IOS && !UNITY_EDITOR
        private IEnumerator RequestAttCoroutine()
        {
            // ATT dialog ổn định hơn khi app đã active và qua được một frame.
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

            // ATT bị từ chối = không có IDFA cho attribution/personalization.
            // KHÔNG phải lý do chặn ads: ad contextual vẫn hợp lệ và vẫn ra tiền.
            _attAuthorized = status == ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED;
            GULogger.Log("GameUp", $"PrivacyManager ATT xong. status={status}, attAuthorized={_attAuthorized}");
        }
#endif

        private IEnumerator RequestUmpCoroutine()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
            bool done = false;

            var request = new ConsentRequestParameters();
            ApplyDebugSettings(request);

            ConsentInformation.Update(request, error =>
            {
                if (error != null)
                {
                    // KHÔNG gán cứng canRequestAds = false ở đây. Update lỗi phần lớn là do mạng
                    // chập chờn lúc cold start, mà consent lưu từ phiên trước vẫn còn hiệu lực —
                    // vẫn phải hỏi CanRequestAds() bên dưới thay vì tự kết luận là không có consent.
                    GULogger.Warning("GameUp", "PrivacyManager UMP update lỗi: " + error.Message);
                    done = true;
                    return;
                }

                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null)
                        GULogger.Warning("GameUp", "PrivacyManager UMP form lỗi: " + formError.Message);

                    done = true;
                });
            });

            while (!done)
                yield return null;

            _canRequestAds = ConsentInformation.CanRequestAds();
#else
            _canRequestAds = true;
            yield break;
#endif
        }

        /// <summary>
        /// true khi Google yêu cầu app phải có mục "Quản lý tuỳ chọn quyền riêng tư"
        /// (bắt buộc với user EEA). Dùng để quyết định hiện/ẩn nút trong Settings.
        /// </summary>
        public bool PrivacyOptionsRequired
        {
            get
            {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
                return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Mở lại form consent để user đổi lựa chọn. Gắn vào nút trong Settings khi
        /// <see cref="PrivacyOptionsRequired"/> là true. <paramref name="onError"/> chỉ được gọi khi lỗi.
        /// </summary>
        public void ShowPrivacyOptionsForm(Action<string> onError = null)
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
            ConsentForm.ShowPrivacyOptionsForm(formError =>
            {
                if (formError != null)
                {
                    GULogger.Warning("GameUp", "PrivacyManager privacy options form lỗi: " + formError.Message);
                    onError?.Invoke(formError.Message);
                    return;
                }

                // User có thể vừa cấp thêm consent — cập nhật lại cổng để AdsManager init được.
                _canRequestAds = ConsentInformation.CanRequestAds();
                GULogger.Log("GameUp", $"PrivacyManager privacy options đóng. canRequestAds={_canRequestAds}");
            });
#else
            onError?.Invoke("UMP không khả dụng (thiếu AdMob dependencies).");
#endif
        }

        /// <summary>Xoá toàn bộ trạng thái consent đã lưu. CHỈ dùng để test lại form.</summary>
        public void ResetConsent()
        {
#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
            ConsentInformation.Reset();
            _completed = false;
            _started = false;
            _canRequestAds = true;
            GULogger.Warning("GameUp", "PrivacyManager: đã reset consent (chỉ dùng khi test).");
#endif
        }

#if ADMOB_DEPENDENCIES_INSTALLED && (UNITY_ANDROID || UNITY_IOS)
        /// <summary>
        /// Giả lập geography EEA để test consent form. Chỉ có tác dụng ở development build —
        /// bật nhầm ở bản release sẽ ép form lên toàn bộ user nên bị chặn cứng ở đây.
        /// </summary>
        private static void ApplyDebugSettings(ConsentRequestParameters request)
        {
            var admob = GameUpAdsConfig.Instance?.admob;
            if (admob == null || !admob.umpDebugForceEea) return;

            if (!Debug.isDebugBuild)
            {
                GULogger.Warning("GameUp",
                    "GameUpAdsConfig.admob.umpDebugForceEea đang bật ở bản release — đã bỏ qua. Nhớ tắt trong config.");
                return;
            }

            request.ConsentDebugSettings = new ConsentDebugSettings
            {
                DebugGeography = DebugGeography.EEA,
                TestDeviceHashedIds = admob.umpTestDeviceHashedIds ?? new List<string>()
            };
            GULogger.Warning("GameUp", "PrivacyManager: UMP đang chạy ở chế độ debug (ép geography = EEA).");
        }
#endif
    }
}
