using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting; // Bắt buộc cho [Preserve]

namespace GameUp.SDK
{
    public class AdmobNativeBannerBridge : BaseAdFormat, IBannerAd
    {
        // Chỉ được invoke trong khối Android device (#if UNITY_ANDROID && !UNITY_EDITOR) → tắt CS0067 cho cấu hình Editor/iOS.
#pragma warning disable 0067
        public event Action<string> OnCollapsedNativeBanner;
#pragma warning restore 0067

#if UNITY_ANDROID && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
        private AndroidJavaObject _nativeManager;
        private AndroidJavaObject _currentActivity;
#endif

#if UNITY_IOS && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
        [DllImport("__Internal")] private static extern void NativeBanner_LoadAd(string adUnitId);
        [DllImport("__Internal")] private static extern void NativeBanner_ShowAd(bool isTop);
        [DllImport("__Internal")] private static extern void NativeBanner_HideAd();
        [DllImport("__Internal")] private static extern void NativeBanner_SetCallbacks(
            Action_Unit onLoaded, Action_UnitString onFailed, Action_Unit onDisplayed, Action_Unit onClosed, Action_Unit onClicked, Action_UnitDouble onPaid, Action_String onLog);

        // Mọi callback mang theo adUnitId (khớp typedef trong NativeBannerManager.mm) để không phải
        // đoán bằng biến static "unit đang xử lý" nữa.
        delegate void Action_Unit(string adUnitId);
        delegate void Action_UnitString(string adUnitId, string error);
        delegate void Action_UnitDouble(string adUnitId, double value);
        delegate void Action_String(string message);
#endif

        private Dictionary<string, bool> _isLoaded = new Dictionary<string, bool>();
        private Dictionary<string, bool> _isLoading = new Dictionary<string, bool>();
#if UNITY_ANDROID && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
        private Dictionary<string, NativeAdCallbackProxy> _proxies = new Dictionary<string, NativeAdCallbackProxy>();
#endif 
        private static AdmobNativeBannerBridge _instance;

        /// <summary>unitId → placement. Thay cho cặp biến "_currentActiveUnitId/_currentActiveWhere"
        /// cũ: chúng chỉ đúng khi mỗi lúc có một unit đang xử lý, hai placement chạy song song là
        /// ghi đè state của nhau và callback bị quy nhầm ad.</summary>
        private readonly Dictionary<string, string> _whereByUnitId = new Dictionary<string, string>();

        private string WhereOf(string unitId) =>
            unitId != null && _whereByUnitId.TryGetValue(unitId, out var w) ? w : "default";

        public AdmobNativeBannerBridge(AdUnitConfig config) : base(config, AdUnitType.Banner, "Admob_NativeBridge")
        {
            _instance = this;
#if UNITY_ANDROID && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                _currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using (AndroidJavaClass managerClass = new AndroidJavaClass("com.gameup.ads.NativeBannerManager"))
                _nativeManager = managerClass.CallStatic<AndroidJavaObject>("getInstance");
#elif UNITY_IOS && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
            // Truyền thêm callback OnLog_iOS vào cổng liên kết
            NativeBanner_SetCallbacks(OnLoaded_iOS, OnFailed_iOS, OnDisplayed_iOS, OnClosed_iOS, OnClicked_iOS, OnPaid_iOS, OnLog_iOS);
#endif
        }

        // Tắt Waterfall cho Native Banner Bridge (Chỉ dùng tầng All)
        public override void Load(string where = null) => LoadByFloor(where, EcpmFloor.All);

        public override bool IsAvailable(string where = null)
        {
            string unitId = _config.ResolveUnitId(_adType, where, EcpmFloor.All);
            return _isLoaded.TryGetValue(unitId, out bool loaded) && loaded;
        }

        protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)
        {
            if (_isLoading.TryGetValue(unitId, out bool loading) && loading) return;

            _isLoading[unitId] = true;
            _isLoaded[unitId] = false;
            _whereByUnitId[unitId] = where ?? "default";

#if UNITY_ANDROID && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
            var proxy = new NativeAdCallbackProxy(
                onLoaded: () => { MainThreadDispatcher.Enqueue(() => { _isLoading[unitId] = false; _isLoaded[unitId] = true; HandleLoadSuccess(unitId, where); }); },
                onFailed: (err) => { MainThreadDispatcher.Enqueue(() => { _isLoading[unitId] = false; _isLoaded[unitId] = false; HandleLoadFailed(unitId, where, floor, err); }); },
                onDisplayed: () => { MainThreadDispatcher.Enqueue(() => NotifyAdDisplayed(where)); },
                onClosed: () => { MainThreadDispatcher.Enqueue(() => { _isLoaded[unitId] = false; NotifyAdClosed(where); OnCollapsedNativeBanner?.Invoke(where); Load(where);}); },
                onClicked: () => { MainThreadDispatcher.Enqueue(() => { }); },
                onPaid: (val) => { MainThreadDispatcher.Enqueue(() => TrackRevenue(unitId, where, "NativeBanner_Android", val)); },
                onLog: (msg) => { MainThreadDispatcher.Enqueue(() => Debug.Log($"<color=#00FF00>[GameUp-NativeBanner]</color> {msg}")); }
            );
            _proxies[unitId] = proxy;
            _nativeManager.Call("loadAd", _currentActivity, unitId, proxy);
#elif UNITY_IOS && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
            NativeBanner_LoadAd(unitId);
#endif
        }

        public void Show(string where)
        {
            string unitId = _config.ResolveUnitId(_adType, where, EcpmFloor.All);
            var entry = _config.GetEntry(_adType, where, EcpmFloor.All);
            bool isTop = entry.CollapsiblePlacement == CollapsibleBannerPlacement.Top;
            _whereByUnitId[unitId] = where ?? "default";

            if (IsAvailable(where))
            {
#if UNITY_ANDROID && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
                if (_proxies.TryGetValue(unitId, out var proxy))
                {
                    _nativeManager.Call("showAd", _currentActivity, isTop, proxy);
                    _isLoaded[unitId] = false; 
                }
#elif UNITY_IOS && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
                NativeBanner_ShowAd(isTop);
                _isLoaded[unitId] = false;
#endif
            }
            else Load(where);
        }

        public void Hide(string where)
        {
            string unitId = _config.ResolveUnitId(_adType, where, EcpmFloor.All);
            _isLoaded[unitId] = false;
            _isLoading[unitId] = false;
#if UNITY_ANDROID && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
            _nativeManager.Call("hideAd", _currentActivity);
#elif UNITY_IOS && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
            NativeBanner_HideAd();
#endif
        }
        
        public void Restore(string where) => Show(where);
        
#if UNITY_ANDROID && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
        [Preserve]
        public class NativeAdCallbackProxy : AndroidJavaProxy
        {
            private readonly Action _onLoaded;
            private readonly Action<string> _onFailed;
            private readonly Action _onDisplayed;
            private readonly Action _onClosed;
            private readonly Action _onClicked;
            private readonly Action<double> _onPaid;
            private readonly Action<string> _onLog;

            public NativeAdCallbackProxy(Action onLoaded, Action<string> onFailed, Action onDisplayed, Action onClosed, Action onClicked, Action<double> onPaid, Action<string> onLog)
                : base("com.gameup.ads.NativeBannerManager$AdCallback")
            {
                _onLoaded = onLoaded;
                _onFailed = onFailed;
                _onDisplayed = onDisplayed;
                _onClosed = onClosed;
                _onClicked = onClicked;
                _onPaid = onPaid;
                _onLog = onLog;
            }

            [Preserve] public void onLoaded() => _onLoaded?.Invoke();
            [Preserve] public void onFailed(string error) => _onFailed?.Invoke(error);
            [Preserve] public void onDisplayed() => _onDisplayed?.Invoke();
            [Preserve] public void onClosed() => _onClosed?.Invoke();
            [Preserve] public void onClicked() => _onClicked?.Invoke();
            [Preserve] public void onPaid(double value) => _onPaid?.Invoke(value);
            [Preserve] public void onLog(string message) => _onLog?.Invoke(message);
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
        [AOT.MonoPInvokeCallback(typeof(Action_Unit))]
        private static void OnLoaded_iOS(string unitId) => MainThreadDispatcher.Enqueue(() => {
            if (_instance == null) return;
            _instance._isLoading[unitId] = false;
            _instance._isLoaded[unitId] = true;
            _instance.HandleLoadSuccess(unitId, _instance.WhereOf(unitId));
        });

        [AOT.MonoPInvokeCallback(typeof(Action_UnitString))]
        private static void OnFailed_iOS(string unitId, string error) => MainThreadDispatcher.Enqueue(() => {
            if (_instance == null) return;
            _instance._isLoading[unitId] = false;
            _instance._isLoaded[unitId] = false;
            _instance.HandleLoadFailed(unitId, _instance.WhereOf(unitId), EcpmFloor.All, error);
        });

        [AOT.MonoPInvokeCallback(typeof(Action_Unit))]
        private static void OnDisplayed_iOS(string unitId) => MainThreadDispatcher.Enqueue(() => {
            if (_instance != null) _instance.NotifyAdDisplayed(_instance.WhereOf(unitId));
        });

        [AOT.MonoPInvokeCallback(typeof(Action_Unit))]
        private static void OnClosed_iOS(string unitId) => MainThreadDispatcher.Enqueue(() => {
            if (_instance == null) return;
            string where = _instance.WhereOf(unitId);
            _instance._isLoaded[unitId] = false;
            _instance.NotifyAdClosed(where);
            _instance.OnCollapsedNativeBanner?.Invoke(where);
            _instance.Load(where);
        });

        [AOT.MonoPInvokeCallback(typeof(Action_Unit))]
        private static void OnClicked_iOS(string unitId) => MainThreadDispatcher.Enqueue(() => { });

        [AOT.MonoPInvokeCallback(typeof(Action_UnitDouble))]
        private static void OnPaid_iOS(string unitId, double value) => MainThreadDispatcher.Enqueue(() => {
            if (_instance != null) _instance.TrackRevenue(unitId, _instance.WhereOf(unitId), "NativeBanner_iOS", value);
        });

        [AOT.MonoPInvokeCallback(typeof(Action_String))]
        private static void OnLog_iOS(string message) => MainThreadDispatcher.Enqueue(() => {
            Debug.Log($"<color=#00FF00>[GameUp-NativeBanner iOS]</color> {message}");
        });
#endif
    }
}