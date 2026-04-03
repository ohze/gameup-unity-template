namespace GameUp.SDK
{
    /// <summary>
    /// Contract for requesting/loading ads from a network.
    /// </summary>
    public interface IRequestAds
    {
        void RequestBanner();
        void RequestInterstitial();
        void RequestRewardedVideo();
        void RequestAppOpenAds();
    }
}
