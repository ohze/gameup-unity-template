using System;

namespace GameUp.SDK
{
    public enum AdUnitType
    {
        Banner,
        Interstitial,
        RewardedVideo,
        AppOpen,
        NativeAd
    }

    public enum EcpmFloor
    {
        High,
        Medium,
        All
    }
    
    public enum CollapsibleBannerPlacement
    {
        None,
        Top,
        Bottom
    }

    [Serializable]
    public class AdUnitIdEntry
    {
        public AdUnitType AdType;
        public string NameId; // placement "where"
        public string Id;     // Unit ID thực tế
        public int intId;
        public BannerFormatType BannerFormat = BannerFormatType.StandardBanner;
        public BannerSize BannerSize = BannerSize.Adaptive;
        public CollapsibleBannerPlacement CollapsiblePlacement = CollapsibleBannerPlacement.None;
        
        // BỔ SUNG: Xác định tầng eCPM floor cho ID này
        public EcpmFloor Floor = EcpmFloor.All; 

        public bool IsValid() => !string.IsNullOrEmpty(Id);
    }
}