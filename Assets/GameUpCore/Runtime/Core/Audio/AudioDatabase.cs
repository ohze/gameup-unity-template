using System.Collections.Generic;
using UnityEngine;

namespace GameUp.Core
{
    [CreateAssetMenu(menuName = "GameUp/Audio/Audio Database", fileName = "AudioDatabase")]
    public class AudioDatabase : ScriptableObject
    {
        public List<AudioIdentityReference> identityReferences = new();
    }
}

