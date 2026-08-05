using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// Mốc phiên chạy dùng chung cho mọi <see cref="MonoSingleton{T}"/>.
    /// Cần một class non-generic vì [RuntimeInitializeOnLoadMethod] không được gọi trên class generic mở.
    /// </summary>
    internal static class MonoSingletonSession
    {
        /// <summary>Tăng mỗi lần vào Play. Dùng để phát hiện static còn sót khi tắt Domain Reload.</summary>
        internal static int Id { get; private set; }

        /// <summary>Chỉ true khi App thực sự đang thoát — không bật khi unload scene.</summary>
        internal static bool IsQuitting { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BeginSession()
        {
            Id++;
            IsQuitting = false;

            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting()
        {
            IsQuitting = true;
        }
    }

    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static int _sessionId = -1;

        // Unity override toán tử == nên phép kiểm tra này cũng bắt được instance đã bị Destroy.
        public static bool IsInitialized => _instance;

        /// <summary>
        /// Override = true nếu singleton phải sống xuyên scene (AudioManager, Pool...).
        /// Mặc định false: instance thuộc về scene đang chứa nó.
        /// </summary>
        protected virtual bool IsPersistent => false;

        public static T Instance
        {
            get
            {
                if (MonoSingletonSession.IsQuitting)
                {
                    GULogger.Warning("Singleton", $"Instance '{typeof(T)}' không được tạo lại khi App đang thoát.");
                    return null;
                }

                lock (_lock)
                {
                    DiscardStaleSession();

                    // Instance đã bị Destroy (đổi scene) sẽ so sánh == null, nên tự tìm/tạo lại ở dưới.
                    if (_instance) return _instance;

                    _instance = (T)FindFirstObjectByType(typeof(T));
                    if (_instance) return _instance;

                    var singletonObject = new GameObject($"{typeof(T)} (Singleton)");
                    _instance = singletonObject.AddComponent<T>();
                    return _instance;
                }
            }
        }

        protected virtual void Awake()
        {
            DiscardStaleSession();

            if (!_instance)
            {
                _instance = this as T;
                ApplyPersistence();
                return;
            }

            if (_instance != this) Destroy(gameObject);
        }

        private void ApplyPersistence()
        {
            if (!IsPersistent) return;

            if (transform.parent != null)
            {
                GULogger.Warning("Singleton",
                    $"'{typeof(T)}' bật IsPersistent nhưng đang là con của '{transform.parent.name}' — DontDestroyOnLoad chỉ áp dụng cho object gốc.");
                return;
            }

            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Khi tắt Domain Reload, static giữ nguyên giữa các lần Play. Bỏ instance của phiên trước để tránh tham chiếu rác.
        /// </summary>
        private static void DiscardStaleSession()
        {
            if (_sessionId == MonoSingletonSession.Id) return;

            _sessionId = MonoSingletonSession.Id;
            _instance = null;
        }
    }
}
