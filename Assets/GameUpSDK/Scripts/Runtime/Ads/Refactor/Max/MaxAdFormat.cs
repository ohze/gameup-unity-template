using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameUp.SDK
{
    public class MaxInterstitialAd : BaseAdFormat, IInterstitialAd
    {
        public MaxInterstitialAd(AdUnitConfig config) : base(config, AdUnitType.Interstitial, "MAX") { }

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
            Action<string, MaxSdkBase.AdInfo> onRevenue = null;

            onLoaded = (id, info) => { if (id == unitId) { Unsubscribe(); HandleLoadSuccess(unitId, where); } };
            onFailed = (id, err) => { if (id == unitId) { Unsubscribe(); HandleLoadFailed(unitId, where, floor, err.Message); } };
            onRevenue = (id, info) => { if (id == unitId) TrackRevenue(id, info.NetworkPlacement, $"Interstitial_{floor}", info.Revenue); };

            void Unsubscribe() { MaxSdkCallbacks.Interstitial.OnAdLoadedEvent -= onLoaded; MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent -= onFailed; }

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += onLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += onFailed;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += onRevenue;
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
                    NotifyAdDisplayed(where);
                    Action<string, MaxSdkBase.AdInfo> onHidden = null;
                    onHidden = (id, info) =>
                    {
                        if (id != unitId) return;
                        MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= onHidden;
                        NotifyAdClosed(where);
                        MainThreadDispatcher.Enqueue(() => { onSuccess?.Invoke(); LoadByFloor(where, currentFloor); });
                    };
                    MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += onHidden;
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
        public MaxRewardedAd(AdUnitConfig config) : base(config, AdUnitType.RewardedVideo, "MAX") { }

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
            Action<string, MaxSdkBase.AdInfo> onRevenue = null;

            onLoaded = (id, info) => { if (id == unitId) { Unsubscribe(); HandleLoadSuccess(unitId, where); } };
            onFailed = (id, err) => { if (id == unitId) { Unsubscribe(); HandleLoadFailed(unitId, where, floor, err.Message); } };
            onRevenue = (id, info) => { if (id == unitId) TrackRevenue(id, info.NetworkPlacement, $"Rewarded_{floor}", info.Revenue); };

            void Unsubscribe() { MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= onLoaded; MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= onFailed; }

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += onLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += onFailed;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += onRevenue;
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
                    NotifyAdDisplayed(where);
                    bool earned = false;
                    Action<string, MaxSdkBase.Reward, MaxSdkBase.AdInfo> onReward = null;
                    Action<string, MaxSdkBase.AdInfo> onHidden = null;

                    onReward = (id, reward, info) => { if (id == unitId) earned = true; };
                    onHidden = (id, info) =>
                    {
                        if (id != unitId) return;
                        MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= onReward;
                        MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= onHidden;
                        NotifyAdClosed(where);
                        MainThreadDispatcher.Enqueue(() => { if (earned) onSuccess?.Invoke(); else onFail?.Invoke(); LoadByFloor(where, currentFloor); });
                    };
                    MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += onReward;
                    MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += onHidden;
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
                    NotifyAdDisplayed(where);
                    Action<string, MaxSdkBase.AdInfo> onHidden = null;
                    onHidden = (id, info) =>
                    {
                        if (id == unitId)
                        {
                            MaxSdkCallbacks.AppOpen.OnAdHiddenEvent -= onHidden;
                            NotifyAdClosed(where);
                            MainThreadDispatcher.Enqueue(() => { onSuccess?.Invoke(); LoadByFloor(where, currentFloor); });
                        }
                    };
                    MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += onHidden;
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
            string unitId = _config.ResolveUnitId(_adType, where, EcpmFloor.All);
            return !string.IsNullOrEmpty(unitId);
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