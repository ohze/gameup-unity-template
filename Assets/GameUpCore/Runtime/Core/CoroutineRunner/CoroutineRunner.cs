using System.Collections;
using UnityEngine;

namespace GameUp.Core
{
    public class CoroutineRunner : MonoSingleton<CoroutineRunner>
    {
        /// <summary>
        /// Chạy Coroutine và trả về đối tượng Coroutine để có thể dừng sau này.
        /// </summary>
        public static Coroutine RunCoroutineWithReturn(IEnumerator ie)
        {
            if (ie == null) return null;

            // Instance tự tạo runner nếu chưa có, và trả null khi App đang thoát.
            var runner = Instance;
            return runner ? runner.StartCoroutine(ie) : null;
        }

        /// <summary>
        /// Chạy Coroutine kiểu "bắn và quên" (Fire and forget).
        /// </summary>
        public static void RunCoroutineWithoutReturn(IEnumerator ie)
        {
            RunCoroutineWithReturn(ie);
        }

        /// <summary>
        /// Dừng một Coroutine cụ thể.
        /// </summary>
        public static void StopIEnumerator(Coroutine ct)
        {
            // Kiểm tra null để tránh lỗi NullReferenceException
            if (IsInitialized && ct != null) Instance.StopCoroutine(ct);
        }

        /// <summary>
        /// Dừng TẤT CẢ các Coroutine đang chạy trên Executors.
        /// </summary>
        public static void StopAll()
        {
            if (IsInitialized) Instance.StopAllCoroutines();
        }
    }
}