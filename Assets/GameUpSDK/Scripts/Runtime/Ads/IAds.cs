using System;

namespace GameUp.SDK
{
    public enum AdUnitType
    {
        Banner,
        Interstitial,
        RewardedVideo,
        AppOpen
    }

    /// <summary>
    /// Placement-based Ad Unit ID entry.
    /// - adType: format of the ad
    /// - nameId: placement key (also used as 'where' in AdsManager)
    /// - id: network ad unit id / placement id
    /// </summary>
    [Serializable]
    public class AdUnitIdEntry
    {
        public AdUnitType adType;
        public int intId;
        public string nameId;
        public string id;

        public AdUnitType AdType => adType;
        public string NameId => nameId;
        public string Id => id;

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(nameId) && !string.IsNullOrEmpty(id);
        }
    }

    /// <summary>
    /// Optional extension for networks that support multiple ad unit IDs by placement key (where).
    /// AdsManager will prefer these APIs when available.
    /// </summary>
    public interface IPlacementAwareAds
    {
        bool IsBannerAvailable(string where);
        bool IsInterstitialAvailable(string where);
        bool IsRewardedVideoAvailable(string where);
        bool IsAppOpenAdsAvailable(string where);
    }

    /// <summary>
    /// Optional helper: resolve an ad placement by integer ID.
    /// Used by AdsManager for "call by id" APIs.
    /// </summary>
    public interface IAdUnitIdResolver
    {
        bool TryResolve(int intId, out AdUnitType adType, out string nameId);
    }

    /// <summary>
    /// Full ads contract: init, request, show, and availability. OrderExecute controls waterfall priority.
    /// Load events notify AdsManager for centralized logging.
    /// </summary>
    public interface IAds : IInitialAds, ICheckValidAds, IShowAds, IRequestAds
    {
        /// <summary>
        /// Lower value = higher priority in AdsManager waterfall.
        /// </summary>
        int OrderExecute { get; set; }

        /// <summary>
        /// Call after GDPR/consent check so the network can apply user consent.
        /// </summary>
        void SetAfterCheckGDPR();

        /// <summary>Raised when an interstitial ad has finished loading successfully.</summary>
        event Action OnInterstitialLoaded;

        /// <summary>Raised when an interstitial ad fails to load. Parameter is the error message.</summary>
        event Action<string> OnInterstitialLoadFailed;

        /// <summary>Raised when a rewarded ad has finished loading successfully.</summary>
        event Action OnRewardedLoaded;

        /// <summary>Raised when a rewarded ad fails to load. Parameter is the error message.</summary>
        event Action<string> OnRewardedLoadFailed;
    }
}
