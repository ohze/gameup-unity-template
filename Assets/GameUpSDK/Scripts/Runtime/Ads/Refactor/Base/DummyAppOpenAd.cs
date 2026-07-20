using System;

namespace GameUp.SDK
{
    public class DummyAppOpenAd : BaseAdFormat, IAppOpenAd
    {
        public DummyAppOpenAd() : base()
        {
            
        }

        public override void Load(string where = null)
        {
        }

        public DummyAppOpenAd(AdUnitConfig config, AdUnitType adType, string networkName) : base(config, adType,
            networkName)
        {
        }

        public override bool IsAvailable(string where = null)
        {
            return false;
        }

        public void Show(string where, Action onSuccess, Action onFail)
        {
            
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor epmFloor)
        {
        }
    }
}