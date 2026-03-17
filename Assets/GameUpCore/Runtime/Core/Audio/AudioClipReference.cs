using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace GameUp.Core
{
    [Serializable]
    public class AudioClipReference : AssetReferenceT<AudioClip>
    {
        public AudioClipReference(string guid) : base(guid)
        {
        }
    }
}