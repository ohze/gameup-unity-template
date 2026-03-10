using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// Base ScriptableObject for game configuration data.
    /// Derive from this to create typed configs loaded via ConfigLoader.
    /// </summary>
    public abstract class GameConfig : ScriptableObject
    {
        /// <summary>Called after the config is loaded. Override to validate or process data.</summary>
        public virtual void OnLoaded() { }
    }
}
