using System;

namespace GameUp.SDK
{
    public class DummyNativeFullscreenAd : BaseAdFormat, INativeFullScreenAd
    {
        public override void Load(string where = null)
        {
        }

        public override bool IsAvailable(string where = null)
        {
            return false;
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)
        {
        }

        public void Show(string where, Action onSuccess, Action onFail)
        {
        }

        public void Hide()
        {
        }
    }
}