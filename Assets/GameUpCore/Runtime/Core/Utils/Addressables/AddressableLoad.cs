using System;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace GameUp.Core
{
    /// <summary>
    /// Gom một chỗ mẫu lặp đi lặp lại khi dùng Addressables:
    /// kiểm tra handle hợp lệ → nếu đã xong thì dùng ngay, chưa xong thì đăng ký Completed,
    /// và luôn kiểm tra kết quả trước khi gọi callback.
    /// </summary>
    public static class AddressableLoad
    {
        /// <summary>
        /// Gọi <paramref name="onLoaded"/> khi handle có kết quả hợp lệ (ngay lập tức nếu đã xong).
        /// Trả về false nếu handle không hợp lệ hoặc đã thất bại — lúc đó callback không bao giờ chạy.
        /// An toàn khi gọi nhiều lần trên cùng một handle đã cache: mỗi lần chỉ thêm một listener.
        /// </summary>
        /// <param name="onFailed">
        /// Gọi khi handle hỏng hoặc load thất bại — dùng để nhả tài nguyên đã giữ trước (source âm thanh, slot UI...).
        /// </param>
        public static bool WhenReady<T>(AsyncOperationHandle<T> handle, Action<T> onLoaded, string tag,
            string context = null, Action onFailed = null)
        {
            if (!handle.IsValid())
            {
                GULogger.Error(tag, $"Addressable handle is invalid{FormatContext(context)}");
                onFailed?.Invoke();
                return false;
            }

            if (handle.IsDone)
            {
                if (!IsUsable(handle))
                {
                    GULogger.Error(tag, $"Addressable load failed{FormatContext(context)}");
                    onFailed?.Invoke();
                    return false;
                }

                onLoaded?.Invoke(handle.Result);
                return true;
            }

            handle.Completed += completed =>
            {
                if (!IsUsable(completed))
                {
                    GULogger.Error(tag, $"Addressable load failed{FormatContext(context)}");
                    onFailed?.Invoke();
                    return;
                }

                onLoaded?.Invoke(completed.Result);
            };

            return true;
        }

        private static bool IsUsable<T>(AsyncOperationHandle<T> handle)
        {
            if (!handle.IsValid()) return false;
            if (handle.Status != AsyncOperationStatus.Succeeded) return false;

            // Với UnityEngine.Object phải dùng toán tử == của Unity để bắt cả object đã bị hủy.
            if (handle.Result is Object unityObject) return unityObject;

            return handle.Result != null;
        }

        private static string FormatContext(string context)
        {
            return string.IsNullOrEmpty(context) ? string.Empty : $" for {context}";
        }
    }
}
