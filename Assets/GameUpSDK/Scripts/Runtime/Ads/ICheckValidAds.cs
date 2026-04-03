namespace GameUp.SDK
{
    /// <summary>
    /// Contract for checking ad availability before showing.
    /// </summary>
    public interface ICheckValidAds
    {
        bool IsBannerAvailable();
        bool IsInterstitialAvailable();
        bool IsRewardedVideoAvailable();
        bool IsAppOpenAdsAvailable();
    }
}
