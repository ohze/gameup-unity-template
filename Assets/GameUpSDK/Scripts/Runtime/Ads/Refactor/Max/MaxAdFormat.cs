using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameUp.SDK
{
    public class MaxInterstitialAd : BaseAdFormat, IInterstitialAd
    {
        public MaxInterstitialAd(AdUnitConfig config) : base(config, AdUnitType.Interstitial, "MAX")
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            // Đăng ký MỘT lần cho cả vòng đời. Trước đây handler revenue được += trong mỗi lần
            // RequestAdInternal nhưng không bao giờ -=, nên sau N lần load, một impression bắn
            // N sự kiện revenue → doanh thu trên Firebase/AppsFlyer bị nhân lên.
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnRevenuePaid;
#endif
        }

#if MAXSDK_DEPENDENCIES_INSTALLED
        private void OnRevenuePaid(string id, MaxSdkBase.AdInfo info) =>
            TrackRevenue(id, info.NetworkPlacement, $"Interstitial_{FloorOf(id)}", info.Revenue);
#endif

        public override bool IsAvailable(string where = null)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            foreach (EcpmFloor floor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, floor);
                if (!string.IsNullOrEmpty(unitId) && MaxSdk.IsInterstitialReady(unitId)) return true;
            }
#endif
            return false;
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            Action<string, MaxSdkBase.AdInfo> onLoaded = null;
            Action<string, MaxSdkBase.ErrorInfo> onFailed = null;

            onLoaded = (id, info) => { if (id == unitId) { Unsubscribe(); HandleLoadSuccess(unitId, where); } };
            onFailed = (id, err) => { if (id == unitId) { Unsubscribe(); HandleLoadFailed(unitId, where, floor, err.Message); } };

            void Unsubscribe() { MaxSdkCallbacks.Interstitial.OnAdLoadedEvent -= onLoaded; MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent -= onFailed; }

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += onLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += onFailed;
            MaxSdk.LoadInterstitial(unitId);
#endif
        }

        public void Show(string where, Action onSuccess, Action onFail)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            foreach (var currentFloor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, currentFloor);
                if (string.IsNullOrEmpty(unitId)) continue;

                if (MaxSdk.IsInterstitialReady(unitId))
                {
                    // MAX ghi rõ trong MaxSdkCallbacks: OnAdDisplayedEvent "may not be received by Unity
                    // until the interstitial ad closes" → không dùng được để ẩn banner/pause capping đúng
                    // lúc. Vẫn báo displayed ngay trước khi show, và bù bằng nhánh display-failed bên dưới.
                    NotifyAdDisplayed(where);

                    Action<string, MaxSdkBase.AdInfo> onHidden = null;
                    Action<string, MaxSdkBase.ErrorInfo, MaxSdkBase.AdInfo> onDisplayFailed = null;

                    void Unsubscribe()
                    {
                        MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= onHidden;
                        MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= onDisplayFailed;
                    }

                    onHidden = (id, info) =>
                    {
                        if (id != unitId) return;
                        Unsubscribe();
                        NotifyAdClosed(where);
                        MainThreadDispatcher.Enqueue(() => { onSuccess?.Invoke(); LoadByFloor(where, currentFloor); });
                    };
                    // Thiếu nhánh này thì khi MAX không present được: onSuccess/onFail không bao giờ
                    // chạy (game treo ở màn chờ), ad không được load lại, và handler onHidden rò mãi.
                    onDisplayFailed = (id, err, info) =>
                    {
                        if (id != unitId) return;
                        Unsubscribe();
                        NotifyAdDisplayFailed(where, err.Message);
                        MainThreadDispatcher.Enqueue(() => { onFail?.Invoke(); LoadByFloor(where, currentFloor); });
                    };

                    MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += onHidden;
                    MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += onDisplayFailed;
                    MaxSdk.ShowInterstitial(unitId, where);
                    return;
                }
            }
            NotifyAdDisplayFailed(where, "not_ready");
            onFail?.Invoke();
            Load(where);
#endif
        }
    }

    public class MaxRewardedAd : BaseAdFormat, IRewardedAd
    {
        public MaxRewardedAd(AdUnitConfig config) : base(config, AdUnitType.RewardedVideo, "MAX")
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            // Xem ghi chú ở MaxInterstitialAd: đăng ký một lần để không nhân bản sự kiện revenue.
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRevenuePaid;
#endif
        }

#if MAXSDK_DEPENDENCIES_INSTALLED
        private void OnRevenuePaid(string id, MaxSdkBase.AdInfo info) =>
            TrackRevenue(id, info.NetworkPlacement, $"Rewarded_{FloorOf(id)}", info.Revenue);
#endif

        public override bool IsAvailable(string where = null)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            foreach (EcpmFloor floor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, floor);
                if (!string.IsNullOrEmpty(unitId) && MaxSdk.IsRewardedAdReady(unitId)) return true;
            }
#endif
            return false;
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            Action<string, MaxSdkBase.AdInfo> onLoaded = null;
            Action<string, MaxSdkBase.ErrorInfo> onFailed = null;

            onLoaded = (id, info) => { if (id == unitId) { Unsubscribe(); HandleLoadSuccess(unitId, where); } };
            onFailed = (id, err) => { if (id == unitId) { Unsubscribe(); HandleLoadFailed(unitId, where, floor, err.Message); } };

            void Unsubscribe() { MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= onLoaded; MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= onFailed; }

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += onLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += onFailed;
            MaxSdk.LoadRewardedAd(unitId);
#endif
        }

        public void Show(string where, Action onSuccess, Action onFail)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            foreach (var currentFloor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, currentFloor);
                if (string.IsNullOrEmpty(unitId)) continue;

                if (MaxSdk.IsRewardedAdReady(unitId))
                {
                    // Xem ghi chú ở MaxInterstitialAd.Show về thời điểm OnAdDisplayedEvent.
                    NotifyAdDisplayed(where);

                    bool earned = false;
                    Action<string, MaxSdkBase.Reward, MaxSdkBase.AdInfo> onReward = null;
                    Action<string, MaxSdkBase.AdInfo> onHidden = null;
                    Action<string, MaxSdkBase.ErrorInfo, MaxSdkBase.AdInfo> onDisplayFailed = null;

                    void Unsubscribe()
                    {
                        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= onReward;
                        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= onHidden;
                        MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= onDisplayFailed;
                    }

                    onReward = (id, reward, info) => { if (id == unitId) earned = true; };
                    onHidden = (id, info) =>
                    {
                        if (id != unitId) return;
                        Unsubscribe();
                        NotifyAdClosed(where);
                        MainThreadDispatcher.Enqueue(() => { if (earned) onSuccess?.Invoke(); else onFail?.Invoke(); LoadByFloor(where, currentFloor); });
                    };
                    onDisplayFailed = (id, err, info) =>
                    {
                        if (id != unitId) return;
                        Unsubscribe();
                        NotifyAdDisplayFailed(where, err.Message);
                        MainThreadDispatcher.Enqueue(() => { onFail?.Invoke(); LoadByFloor(where, currentFloor); });
                    };

                    MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += onReward;
                    MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += onHidden;
                    MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += onDisplayFailed;
                    MaxSdk.ShowRewardedAd(unitId, where);
                    return;
                }
            }
            NotifyAdDisplayFailed(where, "not_ready");
            onFail?.Invoke();
            Load(where);
#endif
        }
    }

    public class MaxAppOpenAd : BaseAdFormat, IAppOpenAd
    {
        public MaxAppOpenAd(AdUnitConfig config) : base(config, AdUnitType.AppOpen, "MAX") { }

        public override bool IsAvailable(string where = null)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            foreach (EcpmFloor floor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, floor);
                if (!string.IsNullOrEmpty(unitId) && MaxSdk.IsAppOpenAdReady(unitId)) return true;
            }
#endif
            return false;
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            Action<string, MaxSdkBase.AdInfo> onLoaded = null;
            Action<string, MaxSdkBase.ErrorInfo> onFailed = null;
            onLoaded = (id, info) => { if (id == unitId) { MaxSdkCallbacks.AppOpen.OnAdLoadedEvent -= onLoaded; MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent -= onFailed; HandleLoadSuccess(unitId, where); } };
            onFailed = (id, err) => { if (id == unitId) { MaxSdkCallbacks.AppOpen.OnAdLoadedEvent -= onLoaded; MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent -= onFailed; HandleLoadFailed(unitId, where, floor, err.Message); } };
            MaxSdkCallbacks.AppOpen.OnAdLoadedEvent += onLoaded;
            MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent += onFailed;
            MaxSdk.LoadAppOpenAd(unitId);
#endif
        }

        public void Show(string where, Action onSuccess, Action onFail)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            foreach (var currentFloor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, currentFloor);
                if (string.IsNullOrEmpty(unitId)) continue;

                if (MaxSdk.IsAppOpenAdReady(unitId))
                {
                    // Xem ghi chú ở MaxInterstitialAd.Show về thời điểm OnAdDisplayedEvent.
                    NotifyAdDisplayed(where);

                    Action<string, MaxSdkBase.AdInfo> onHidden = null;
                    Action<string, MaxSdkBase.ErrorInfo, MaxSdkBase.AdInfo> onDisplayFailed = null;

                    void Unsubscribe()
                    {
                        MaxSdkCallbacks.AppOpen.OnAdHiddenEvent -= onHidden;
                        MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent -= onDisplayFailed;
                    }

                    onHidden = (id, info) =>
                    {
                        if (id != unitId) return;
                        Unsubscribe();
                        NotifyAdClosed(where);
                        MainThreadDispatcher.Enqueue(() => { onSuccess?.Invoke(); LoadByFloor(where, currentFloor); });
                    };
                    onDisplayFailed = (id, err, info) =>
                    {
                        if (id != unitId) return;
                        Unsubscribe();
                        NotifyAdDisplayFailed(where, err.Message);
                        MainThreadDispatcher.Enqueue(() => { onFail?.Invoke(); LoadByFloor(where, currentFloor); });
                    };

                    MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += onHidden;
                    MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent += onDisplayFailed;
                    MaxSdk.ShowAppOpenAd(unitId, where);
                    return;
                }
            }
            NotifyAdDisplayFailed(where, "not_ready");
            onFail?.Invoke();
            Load(where);
#endif
        }
    }

    public class MaxBannerAd : BaseAdFormat, IBannerAd
    {
        private readonly Dictionary<string, bool> _isLoaded = new Dictionary<string, bool>();

        public MaxBannerAd(AdUnitConfig config) : base(config, AdUnitType.Banner, "MAX") { }

        // Tắt Waterfall cho Banner MAX
        public override void Load(string where = null) => LoadByFloor(where, EcpmFloor.All);

        public override bool IsAvailable(string where = null)
        {
            // Trước đây chỉ kiểm tra "có cấu hình unit id hay không" → LUÔN trả true khi MAX được
            // cấu hình, kể cả lúc chưa có banner nào load xong. AdsManager.GetAvailableProvider dùng
            // hàm này để chọn mạng, nên MAX che mất AdMob và banner không bao giờ lên.
            string unitId = _config.ResolveUnitId(_adType, where, EcpmFloor.All);
            return !string.IsNullOrEmpty(unitId) && _isLoaded.TryGetValue(unitId, out var loaded) && loaded;
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            var entry = _config.GetEntry(_adType, where, floor);

            MainThreadDispatcher.Enqueue(() =>
            {
                _isLoaded[unitId] = false;
                var pos = entry.CollapsiblePlacement == CollapsibleBannerPlacement.Top ? MaxSdkBase.AdViewPosition.TopCenter : MaxSdkBase.AdViewPosition.BottomCenter;
                MaxSdk.CreateBanner(unitId, new MaxSdkBase.AdViewConfiguration(pos) { IsAdaptive = entry.BannerSize == BannerSize.Adaptive });
                MaxSdk.HideBanner(unitId);

                Action<string, MaxSdkBase.AdInfo> onLoaded = null;
                Action<string, MaxSdkBase.ErrorInfo> onFailed = null;

                onLoaded = (id, info) => { if (id == unitId) { _isLoaded[unitId] = true; MaxSdkCallbacks.Banner.OnAdLoadedEvent -= onLoaded; MaxSdkCallbacks.Banner.OnAdLoadFailedEvent -= onFailed; HandleLoadSuccess(unitId, where); } };
                onFailed = (id, err) => { if (id == unitId) { _isLoaded[unitId] = false; MaxSdkCallbacks.Banner.OnAdLoadedEvent -= onLoaded; MaxSdkCallbacks.Banner.OnAdLoadFailedEvent -= onFailed; HandleLoadFailed(unitId, where, floor, err.Message); } };

                MaxSdkCallbacks.Banner.OnAdLoadedEvent += onLoaded;
                MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += onFailed;
                MaxSdk.LoadBanner(unitId);
            });
#endif
        }

        public void Show(string where)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            MainThreadDispatcher.Enqueue(() =>
            {
                string unitId = _config.ResolveUnitId(_adType, where, EcpmFloor.All);
                if (string.IsNullOrEmpty(unitId)) return;

                if (_isLoaded.TryGetValue(unitId, out bool loaded) && loaded)
                {
                    NotifyAdDisplayed(where);
                    MaxSdk.ShowBanner(unitId);
                }
                else Load(where);
            });
#endif
        }

        public void Hide(string where)
        {
#if MAXSDK_DEPENDENCIES_INSTALLED
            string unitId = _config.ResolveUnitId(_adType, where, EcpmFloor.All);
            if (!string.IsNullOrEmpty(unitId)) MaxSdk.HideBanner(unitId);
#endif
        }
        public void Restore(string where) => Show(where);
    }
}