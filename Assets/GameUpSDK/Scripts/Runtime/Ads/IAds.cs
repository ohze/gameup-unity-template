using System;

namespace GameUp.SDK
{
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
