using System.Collections;
using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// Global coroutine runner that doesn't require a scene MonoBehaviour.
    /// Auto-creates a DontDestroyOnLoad GameObject.
    /// </summary>
    public sealed class CoroutineRunner : PersistentSingleton<CoroutineRunner>
    {
        /// <summary>Start a coroutine globally without needing a MonoBehaviour reference.</summary>
        public static Coroutine Run(IEnumerator routine) => Instance.StartCoroutine(routine);

        /// <summary>Stop a previously started global coroutine.</summary>
        public static void Stop(Coroutine coroutine)
        {
            if (HasInstance && coroutine != null)
                Instance.StopCoroutine(coroutine);
        }

        /// <summary>Stop a coroutine by its IEnumerator reference.</summary>
        public static void Stop(IEnumerator routine)
        {
            if (HasInstance && routine != null)
                Instance.StopCoroutine(routine);
        }

        /// <summary>Stop all global coroutines.</summary>
        public static void StopAll()
        {
            if (HasInstance)
                Instance.StopAllCoroutines();
        }
    }
}
