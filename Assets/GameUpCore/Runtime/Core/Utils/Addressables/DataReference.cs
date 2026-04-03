using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace GameUp.Core
{
    [Serializable]
    public class DataReference : AssetReferenceT<ScriptableObject>
    {
        public DataReference(string guid) : base(guid)
        {
        }
    }
}