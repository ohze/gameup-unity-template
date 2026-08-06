using System;
using System.Collections.Generic;
#if LEVELPLAY_DEPENDENCIES_INSTALLED
using Unity.Services.LevelPlay;
#endif

namespace GameUp.SDK
{
    public class IronSourceInterstitialAd : BaseAdFormat, IInterstitialAd
    {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private Dictionary<string, LevelPlayInterstitialAd> _ads = new Dictionary<string, LevelPlayInterstitialAd>();
#endif
        public IronSourceInterstitialAd(AdUnitConfig config) : base(config, AdUnitType.Interstitial, "LevelPlay") { }

        public override bool IsAvailable(string where = null)
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            foreach (EcpmFloor floor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, floor);
                if (!string.IsNullOrEmpty(unitId) && _ads.TryGetValue(unitId, out var ad) && ad != null && ad.IsAdReady())
                    return true;
            }
#endif
            return false;
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            if (!_ads.ContainsKey(unitId))
            {
                var newAd = new LevelPlayInterstitialAd(unitId);
                newAd.OnAdLoaded += (_) => HandleLoadSuccess(unitId, where);
                newAd.OnAdLoadFailed += (err) => HandleLoadFailed(unitId, where, floor, err.ErrorMessage);
                _ads[unitId] = newAd;
            }
            _ads[unitId].LoadAd();
#endif
        }

        public void Show(string where, Action onSuccess, Action onFail)
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            foreach (var currentFloor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, currentFloor);
                if (string.IsNullOrEmpty(unitId)) continue;

                if (_ads.TryGetValue(unitId, out var ad) && ad != null && ad.IsAdReady())
                {
                    Action<LevelPlayAdInfo> onDisplayed = null;
                    Action<LevelPlayAdInfo> onClosed = null;
                    Action<LevelPlayAdInfo, LevelPlayAdError> onFailed = null;

                    void Unsubscribe()
                    {
                        ad.OnAdDisplayed -= onDisplayed;
                        ad.OnAdClosed -= onClosed;
                        ad.OnAdDisplayFailed -= onFailed;
                    }

                    // Báo displayed từ callback thật thay vì ngay trước ShowAd: present lỗi thì
                    // không còn log impression giả và không kẹt PauseAllCapping.
                    onDisplayed = (_) => NotifyAdDisplayed(where);
                    onClosed = (_) =>
                    {
                        Unsubscribe();
                        NotifyAdClosed(where);
                        MainThreadDispatcher.Enqueue(() => { onSuccess?.Invoke(); Load(where); });
                    };
                    onFailed = (_, err) =>
                    {
                        Unsubscribe();
                        NotifyAdDisplayFailed(where, err.ErrorMessage);
                        MainThreadDispatcher.Enqueue(() => { onFail?.Invoke(); Load(where); });
                    };

                    ad.OnAdDisplayed += onDisplayed;
                    ad.OnAdClosed += onClosed;
                    ad.OnAdDisplayFailed += onFailed;
                    ad.ShowAd(where);
                    return;
                }
            }
            NotifyAdDisplayFailed(where, "not_ready");
            onFail?.Invoke();
            Load(where);
#endif
        }
    }

    public class IronSourceRewardedAd : BaseAdFormat, IRewardedAd
    {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private Dictionary<string, LevelPlayRewardedAd> _ads = new Dictionary<string, LevelPlayRewardedAd>();
#endif
        public IronSourceRewardedAd(AdUnitConfig config) : base(config, AdUnitType.RewardedVideo, "LevelPlay") { }

        public override bool IsAvailable(string where = null)
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            foreach (EcpmFloor floor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, floor);
                if (!string.IsNullOrEmpty(unitId) && _ads.TryGetValue(unitId, out var ad) && ad != null && ad.IsAdReady())
                    return true;
            }
#endif
            return false;
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            if (!_ads.ContainsKey(unitId))
            {
                var newAd = new LevelPlayRewardedAd(unitId);
                newAd.OnAdLoaded += (_) => HandleLoadSuccess(unitId, where);
                newAd.OnAdLoadFailed += (err) => HandleLoadFailed(unitId, where, floor, err.ErrorMessage);
                _ads[unitId] = newAd;
            }
            _ads[unitId].LoadAd();
#endif
        }

        public void Show(string where, Action onSuccess, Action onFail)
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            foreach (var currentFloor in _config.GetActiveFloors())
            {
                string unitId = _config.ResolveUnitId(_adType, where, currentFloor);
                if (string.IsNullOrEmpty(unitId)) continue;

                if (_ads.TryGetValue(unitId, out var ad) && ad != null && ad.IsAdReady())
                {
                    bool earned = false;
                    Action<LevelPlayAdInfo> onDisplayed = null;
                    Action<LevelPlayAdInfo> onClosed = null;
                    Action<LevelPlayAdInfo, LevelPlayReward> onReward = null;
                    Action<LevelPlayAdInfo, LevelPlayAdError> onFailed = null;

                    void Unsubscribe()
                    {
                        ad.OnAdDisplayed -= onDisplayed;
                        ad.OnAdClosed -= onClosed;
                        ad.OnAdRewarded -= onReward;
                        ad.OnAdDisplayFailed -= onFailed;
                    }

                    // Xem ghi chú ở IronSourceInterstitialAd.Show.
                    onDisplayed = (_) => NotifyAdDisplayed(where);
                    onClosed = (_) =>
                    {
                        Unsubscribe();
                        NotifyAdClosed(where);
                        MainThreadDispatcher.Enqueue(() => { if (earned) onSuccess?.Invoke(); else onFail?.Invoke(); Load(where); });
                    };
                    onReward = (_, reward) => { earned = true; };
                    onFailed = (_, err) =>
                    {
                        Unsubscribe();
                        NotifyAdDisplayFailed(where, err.ErrorMessage);
                        MainThreadDispatcher.Enqueue(() => { onFail?.Invoke(); Load(where); });
                    };

                    ad.OnAdDisplayed += onDisplayed;
                    ad.OnAdClosed += onClosed;
                    ad.OnAdRewarded += onReward;
                    ad.OnAdDisplayFailed += onFailed;
                    ad.ShowAd(where);
                    return;
                }
            }
            NotifyAdDisplayFailed(where, "not_ready");
            onFail?.Invoke();
            Load(where);
#endif
        }
    }

    public class IronSourceBannerAd : BaseAdFormat, IBannerAd
    {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private Dictionary<string, LevelPlayBannerAd> _ads = new Dictionary<string, LevelPlayBannerAd>();
#endif
        private readonly Dictionary<string, bool> _isLoaded = new Dictionary<string, bool>();

        public IronSourceBannerAd(AdUnitConfig config) : base(config, AdUnitType.Banner, "LevelPlay") { }

        // Tắt Waterfall cho Banner
        public override void Load(string where = null) => LoadByFloor(where, EcpmFloor.All);

        public override bool IsAvailable(string where = null)
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            string unitId = _config.ResolveUnitId(_adType, where, EcpmFloor.All);
            return _isLoaded.TryGetValue(unitId, out var loaded) && loaded;
#else
            return false;
#endif
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            var entry = _config.GetEntry(_adType, where, floor);
            MainThreadDispatcher.Enqueue(() =>
            {
                if (_ads.ContainsKey(unitId)) { _ads[unitId].DestroyAd(); _ads.Remove(unitId); }
                _isLoaded[unitId] = false;

                var pos = entry.CollapsiblePlacement == CollapsibleBannerPlacement.Top ? LevelPlayBannerPosition.TopCenter : LevelPlayBannerPosition.BottomCenter;
                var bannerConfig = new LevelPlayBannerAd.Config.Builder()
                    .SetSize(GetLevelPlayAdSize(entry.BannerSize)).SetPosition(pos).SetDisplayOnLoad(false).Build();

                var banner = new LevelPlayBannerAd(unitId, bannerConfig);
                banner.OnAdLoaded += (_) => MainThreadDispatcher.Enqueue(() => { _isLoaded[unitId] = true; HandleLoadSuccess(unitId, where); });
                banner.OnAdLoadFailed += (err) => MainThreadDispatcher.Enqueue(() => { _isLoaded[unitId] = false; HandleLoadFailed(unitId, where, floor, err.ErrorMessage); });
                
                _ads[unitId] = banner;
                banner.LoadAd();
            });
#endif
        }

        public void Show(string where)
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            MainThreadDispatcher.Enqueue(() =>
            {
                string unitId = _config.ResolveUnitId(_adType, where, EcpmFloor.All);
                if (string.IsNullOrEmpty(unitId)) return;

                if (_isLoaded.TryGetValue(unitId, out bool loaded) && loaded)
                {
                    NotifyAdDisplayed(where);
                    if (_ads.TryGetValue(unitId, out var ad) && ad != null) ad.ShowAd();
                }
                else Load(where);
            });
#endif
        }

        public void Hide(string where)
        {
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            string unitId = _config.ResolveUnitId(_adType, where, EcpmFloor.All);
            if (_ads.TryGetValue(unitId, out var ad) && ad != null) ad.HideAd();
#endif
        }
        public void Restore(string where) => Show(where);

#if LEVELPLAY_DEPENDENCIES_INSTALLED
        private static LevelPlayAdSize GetLevelPlayAdSize(BannerSize size)
        {
            switch (size) {
                case BannerSize.Banner: return LevelPlayAdSize.BANNER;
                case BannerSize.Adaptive: return LevelPlayAdSize.CreateAdaptiveAdSize();
                case BannerSize.MediumRectangle: return LevelPlayAdSize.MEDIUM_RECTANGLE;
                case BannerSize.Leaderboard: return LevelPlayAdSize.LEADERBOARD;
                default: return LevelPlayAdSize.LARGE;
            }
        }
#endif
    }
}