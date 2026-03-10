using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// Generic MonoBehaviour singleton. Auto-creates if not found in scene.
    /// Destroyed when scene unloads. Use <see cref="PersistentSingleton{T}"/> for cross-scene persistence.
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        static T _instance;
        static readonly object _lock = new();
        static bool _applicationIsQuitting;

        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning($"[Singleton] Instance of {typeof(T)} already destroyed on application quit.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance != null) return _instance;

                    _instance = FindObjectOfType<T>();
                    if (_instance != null) return _instance;

                    var go = new GameObject($"[{typeof(T).Name}]");
                    _instance = go.AddComponent<T>();
                    return _instance;
                }
            }
        }

        public static bool HasInstance => _instance != null;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this as T;
            OnSingletonAwake();
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        protected virtual void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }

        /// <summary>Called once when this singleton initializes. Override instead of Awake.</summary>
        protected virtual void OnSingletonAwake() { }
    }
}
