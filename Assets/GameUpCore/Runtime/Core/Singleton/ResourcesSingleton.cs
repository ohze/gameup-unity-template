using UnityEngine;

namespace GameUp.Core
{
    public class ResourcesSingleton<T> : ScriptableObject where T : ScriptableObject
    {
        private static T _instance;

        private static readonly string[] FallbackResourcePaths =
        {
            "Data/Singletons",
            "Data/Singleton"
        };

        public static T Instance
        {
            get
            {
                if (_instance) return _instance;

                for (var i = 0; i < FallbackResourcePaths.Length && !_instance; i++)
                {
                    var all = Resources.LoadAll<T>(FallbackResourcePaths[i]);
                    if (all != null && all.Length > 0) _instance = all[0];
                }

                return _instance;
            }
        }
    }
}