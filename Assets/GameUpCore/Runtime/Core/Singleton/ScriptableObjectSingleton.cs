using UnityEngine;

namespace GameUp.Core
{
    public abstract class ScriptableObjectSingleton<T> : ScriptableObject where T : ScriptableObject
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance) return _instance;

                var holder = AddressableDataHolder.Instance;
                if (!holder) return null;

                _instance = holder.GetData<T>();
                return _instance;
            }
        }
    }
}