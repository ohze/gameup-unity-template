using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameUp.Core
{
    public static class CoroutineExtension
    {
        // Cache để tránh tạo rác (GC)
        private static readonly WaitForEndOfFrame _endOfFrame = new();
        private static readonly Dictionary<float, WaitForSeconds> _timeIntervals = new();

        private static WaitForSeconds GetWaitForSeconds(float seconds)
        {
            if (!_timeIntervals.TryGetValue(seconds, out var waitForSeconds))
            {
                waitForSeconds = new WaitForSeconds(seconds);
                _timeIntervals.Add(seconds, waitForSeconds);
            }
            return waitForSeconds;
        }

        // --- EXTENSIONS ---

        // Chạy ngay frame sau
        public static Coroutine DelayFrame(this MonoBehaviour mono, Action callback)
            => mono.StartCoroutine(IEDelayFrame(callback));

        // Chạy sau N giây (Scaled Time - bị ảnh hưởng bởi Pause/TimeScale)
        public static Coroutine Delay(this MonoBehaviour mono, float seconds, Action callback)
            => mono.StartCoroutine(IEDelay(seconds, callback));

        // Chạy sau N giây (Unscaled Time - dùng cho UI/Menu khi pause game)
        public static Coroutine DelayUnscaled(this MonoBehaviour mono, float seconds, Action callback)
            => mono.StartCoroutine(IEDelayUnscaled(seconds, callback));

        // Đợi đến khi thỏa mãn điều kiện
        public static Coroutine WaitUntil(this MonoBehaviour mono, Func<bool> predicate, Action callback)
            => mono.StartCoroutine(IEWaitUntil(predicate, callback));

        // Đợi đến khi hết frame (sau khi Camera đã render xong)
        public static Coroutine WaitEndOfFrame(this MonoBehaviour mono, Action callback)
            => mono.StartCoroutine(IEWaitEndOfFrame(callback));

        // --- INTERNAL ENUMERATORS ---

        private static IEnumerator IEDelayFrame(Action callback)
        {
            yield return null;
            callback?.Invoke();
        }

        private static IEnumerator IEDelay(float seconds, Action callback)
        {
            yield return GetWaitForSeconds(seconds);
            callback?.Invoke();
        }

        private static IEnumerator IEDelayUnscaled(float seconds, Action callback)
        {
            yield return new WaitForSecondsRealtime(seconds);
            callback?.Invoke();
        }

        private static IEnumerator IEWaitUntil(Func<bool> predicate, Action callback)
        {
            yield return new WaitUntil(predicate);
            callback?.Invoke();
        }

        private static IEnumerator IEWaitEndOfFrame(Action callback)
        {
            yield return _endOfFrame;
            callback?.Invoke();
        }
    }
}