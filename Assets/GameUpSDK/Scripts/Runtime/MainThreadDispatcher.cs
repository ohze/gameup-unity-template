using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameUp.SDK
{
    /// <summary>
    /// Ensures SDK callbacks that may run off the main thread are invoked on the Unity main thread.
    /// </summary>
    public static class MainThreadDispatcher
    {
        private static readonly object Lock = new object();
        private static readonly List<Action> Pending = new List<Action>();

        public static void Enqueue(Action action)
        {
            if (action == null) return;
            lock (Lock)
            {
                Pending.Add(action);
            }
        }

        /// <summary>
        /// Call from a MonoBehaviour Update() to drain the queue. AdsManager does this.
        /// </summary>
        public static void ProcessQueue()
        {
            List<Action> toRun;
            lock (Lock)
            {
                if (Pending.Count == 0) return;
                toRun = new List<Action>(Pending);
                Pending.Clear();
            }

            foreach (var a in toRun)
            {
                try
                {
                    a?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError("[CtySDK] MainThreadDispatcher: " + e);
                }
            }
        }
    }
}
