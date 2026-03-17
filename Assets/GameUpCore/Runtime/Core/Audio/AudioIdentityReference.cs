using System;
using UnityEngine.AddressableAssets;

namespace GameUp.Core
{
    [Serializable]
    public class AudioIdentityReference : AssetReferenceT<AudioIdentity>
    {
        public AudioIdentityReference(string guid) : base(guid)
        {
        }
    }
}

