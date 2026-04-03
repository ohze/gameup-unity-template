namespace GameUp.Core
{
    public class RemoveAdsSetting : Singleton<RemoveAdsSetting>
    {
        public const string RemoveInter = "remove_inter";
        public const string RemoveAllAds = "remove_all_ads";
        public readonly BooleanVar IsRemoveInter = new(RemoveInter, false);
        public readonly BooleanVar IsRemoveAllAds = new(RemoveAllAds, false);
    }
}
