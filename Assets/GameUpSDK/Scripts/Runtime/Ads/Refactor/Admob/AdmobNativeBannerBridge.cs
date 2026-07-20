using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

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
            Action_Void onLoaded, Action_String onFailed, Action_Void onDisplayed, Action_Void onClosed, Action_Void onClicked, Action_Double onPaid);
        delegate void Action_Void(); delegate void Action_String(string error); delegate void Action_Double(double value);
#endif

        private Dictionary<string, bool> _isLoaded = new Dictionary<string, bool>();
        private Dictionary<string, bool> _isLoading = new Dictionary<string, bool>();
#if UNITY_ANDROID && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
        private Dictionary<string, NativeAdCallbackProxy> _proxies = new Dictionary<string, NativeAdCallbackProxy>();
#endif 
        private static AdmobNativeBannerBridge _instance;
        private string _currentActiveWhere;
        private string _currentActiveUnitId; // Lưu lại ID đang xử lý cho iOS callback

        public AdmobNativeBannerBridge(AdUnitConfig config) : base(config, AdUnitType.Banner, "Admob_NativeBridge")
        {
            _instance = this;
#if UNITY_ANDROID && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                _currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using (AndroidJavaClass managerClass = new AndroidJavaClass("com.gameup.ads.NativeBannerManager"))
                _nativeManager = managerClass.CallStatic<AndroidJavaObject>("getInstance");
#elif UNITY_IOS && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
            NativeBanner_SetCallbacks(OnLoaded_iOS, OnFailed_iOS, OnDisplayed_iOS, OnClosed_iOS, OnClicked_iOS, OnPaid_iOS);
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
            _currentActiveWhere = where;
            _currentActiveUnitId = unitId;

#if UNITY_ANDROID && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
            var proxy = new NativeAdCallbackProxy(
                onLoaded: () => { MainThreadDispatcher.Enqueue(() => { _isLoading[unitId] = false; _isLoaded[unitId] = true; HandleLoadSuccess(unitId, where); }); },
                onFailed: (err) => { MainThreadDispatcher.Enqueue(() => { _isLoading[unitId] = false; _isLoaded[unitId] = false; HandleLoadFailed(unitId, where, floor, err); }); },
                onDisplayed: () => { MainThreadDispatcher.Enqueue(() => NotifyAdDisplayed(where)); },
                onClosed: () => { MainThreadDispatcher.Enqueue(() => { _isLoaded[unitId] = false; NotifyAdClosed(where); OnCollapsedNativeBanner?.Invoke(where); Load(where);}); },
                onClicked: () => { MainThreadDispatcher.Enqueue(() => { }); },
                onPaid: (val) => { MainThreadDispatcher.Enqueue(() => TrackRevenue(unitId, where, "NativeBanner_Android", val)); }
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
            _currentActiveWhere = where;
            _currentActiveUnitId = unitId;

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
        // ==========================================
        // PROXY CLASS CHO ANDROID
        // ==========================================
        public class NativeAdCallbackProxy : AndroidJavaProxy
        {
            private readonly Action _onLoaded;
            private readonly Action<string> _onFailed;
            private readonly Action _onDisplayed;
            private readonly Action _onClosed;
            private readonly Action _onClicked;
            private readonly Action<double> _onPaid;

            public NativeAdCallbackProxy(Action onLoaded, Action<string> onFailed, Action onDisplayed, Action onClosed, Action onClicked, Action<double> onPaid) 
                : base("com.gameup.ads.NativeBannerManager$AdCallback")
            {
                _onLoaded = onLoaded; 
                _onFailed = onFailed; 
                _onDisplayed = onDisplayed; 
                _onClosed = onClosed; 
                _onClicked = onClicked; 
                _onPaid = onPaid;
            }

            public void onLoaded() => _onLoaded?.Invoke();
            public void onFailed(string error) => _onFailed?.Invoke(error);
            public void onDisplayed() => _onDisplayed?.Invoke();
            public void onClosed() => _onClosed?.Invoke();
            public void onClicked() => _onClicked?.Invoke();
            public void onPaid(double value) => _onPaid?.Invoke(value);
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR && ADMOB_DEPENDENCIES_INSTALLED
        // ==========================================
        // STATIC CALLBACKS CHO IOS
        // ==========================================
        [AOT.MonoPInvokeCallback(typeof(Action_Void))]
        private static void OnLoaded_iOS() => MainThreadDispatcher.Enqueue(() => { 
            if (_instance != null) {
                _instance._isLoading[_instance._currentActiveUnitId] = false;
                _instance._isLoaded[_instance._currentActiveUnitId] = true;
                _instance.HandleLoadSuccess(_instance._currentActiveUnitId, _instance._currentActiveWhere);
            }
        });

        [AOT.MonoPInvokeCallback(typeof(Action_String))]
        private static void OnFailed_iOS(string error) => MainThreadDispatcher.Enqueue(() => {
            if (_instance != null) {
                _instance._isLoading[_instance._currentActiveUnitId] = false;
                _instance._isLoaded[_instance._currentActiveUnitId] = false;
                _instance.HandleLoadFailed(_instance._currentActiveUnitId, _instance._currentActiveWhere, EcpmFloor.All, error);
            }
        });

        [AOT.MonoPInvokeCallback(typeof(Action_Void))]
        private static void OnDisplayed_iOS() => MainThreadDispatcher.Enqueue(() => {
            if (_instance != null) _instance.NotifyAdDisplayed(_instance._currentActiveWhere);
        });

        [AOT.MonoPInvokeCallback(typeof(Action_Void))]
        private static void OnClosed_iOS() => MainThreadDispatcher.Enqueue(() => {
            if (_instance != null) {
                _instance._isLoaded[_instance._currentActiveUnitId] = false;
                _instance.NotifyAdClosed(_instance._currentActiveWhere);
                _instance.OnCollapsedNativeBanner?.Invoke(_instance._currentActiveWhere);
                _instance.Load(_instance._currentActiveWhere);
            }
        });

        [AOT.MonoPInvokeCallback(typeof(Action_Void))]
        private static void OnClicked_iOS() => MainThreadDispatcher.Enqueue(() => { });

        [AOT.MonoPInvokeCallback(typeof(Action_Double))]
        private static void OnPaid_iOS(double value) => MainThreadDispatcher.Enqueue(() => {
            if (_instance != null) _instance.TrackRevenue(_instance._currentActiveUnitId, _instance._currentActiveWhere, "NativeBanner_iOS", value);
        });
#endif
    }
}