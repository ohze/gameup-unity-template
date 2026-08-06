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

        // Double buffer: swap hai list thay vì new List mỗi lần drain.
        private static List<Action> _pending = new List<Action>();
        private static List<Action> _draining = new List<Action>();

        public static void Enqueue(Action action)
        {
            if (action == null) return;
            lock (Lock)
            {
                _pending.Add(action);
            }
        }

        /// <summary>
        /// Rút hàng đợi. Runner riêng bên dưới tự gọi mỗi frame; AdsManager.Update cũng gọi và
        /// điều đó vô hại (lần thứ hai chỉ thấy hàng đợi rỗng).
        /// </summary>
        public static void ProcessQueue()
        {
            List<Action> toRun;
            lock (Lock)
            {
                if (_pending.Count == 0) return;
                (_pending, _draining) = (_draining, _pending);
                // Giữ tham chiếu vào biến cục bộ: nếu một action lại gọi ProcessQueue, lần gọi lồng
                // sẽ swap hai buffer và vòng lặp bên ngoài đọc field sẽ nhảy sang list khác giữa chừng.
                toRun = _draining;
            }

            for (int i = 0; i < toRun.Count; i++)
            {
                try
                {
                    toRun[i]?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError("[GameUp] MainThreadDispatcher: " + e);
                }
            }

            toRun.Clear();
        }

        /// <summary>
        /// Trước đây hàng đợi CHỈ được rút trong AdsManager.Update, nên AdsManager bị disable hay
        /// chưa kịp tồn tại là mọi callback ads kẹt lại vĩnh viễn. Runner này bảo đảm hàng đợi luôn
        /// có người rút, độc lập với vòng đời của AdsManager.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallRunner()
        {
            lock (Lock)
            {
                // Domain reload bị tắt thì static còn sót lại giữa các lần Play — dọn hàng đợi cũ.
                _pending.Clear();
                _draining.Clear();
            }

            var go = new GameObject("[GameUp] MainThreadDispatcher");
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<Runner>();
            // Phải viết đủ UnityEngine.Object: file có cả `using System;` nên `Object` trần là mơ hồ.
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private class Runner : MonoBehaviour
        {
            private void Update() => ProcessQueue();
        }
    }
}
