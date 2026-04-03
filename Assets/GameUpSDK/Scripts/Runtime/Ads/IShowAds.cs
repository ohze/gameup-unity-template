using System;

namespace GameUp.SDK
{
    /// <summary>
    /// Contract for showing ads. Callbacks allow game logic to react (e.g. grant rewards).
    /// </summary>
    public interface IShowAds
    {
        void ShowBanner(string where);
        void HideBanner(string where);
        void ShowInterstitial(string where, Action onSuccess, Action onFail);
        void ShowRewardedVideo(string where, Action onSuccess, Action onFail);
        void ShowAppOpenAds(string where, Action onSuccess, Action onFail);
    }
}
