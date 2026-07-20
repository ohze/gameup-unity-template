using GameUp.Core;
using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace GameUp.SDK
{
    public class FullScreenNativeAdManager : MonoSingleton<FullScreenNativeAdManager>
    {
        private AndroidJavaClass bridgeClass;
        private AndroidJavaObject currentActivity;
        private bool _initialized;

        // Lưu vết vị trí và ID đang hiển thị trên màn hình
        private string _currentShowingUnitId;
        private string _currentShowingWhere;

        // Các event giờ đây mang theo tham số unitId (và where) để phân biệt quảng cáo
        public event Action<string> OnAdLoadedEvent;
        public event Action<string, string> OnAdLoadFailedEvent;
        public event Action<string, string> OnAdClosedEvent;
        public event Action<string, string> OnAdDisplayedEvent;
        public event Action<string, string, double> OnAdPaidEvent; 

#if UNITY_IOS && !UNITY_EDITOR
        public delegate void NativeAdLoadedDelegate(string unitId);
        public delegate void NativeAdFailedDelegate(string unitId, string error);
        public delegate void NativeAdClosedDelegate(string unitId);
        public delegate void NativeAdPaidDelegate(string unitId, double value);

        [DllImport("__Internal")]
        private static extern void _iosLoadNativeAd(string adUnitId, NativeAdLoadedDelegate onLoaded, NativeAdFailedDelegate onFailed, NativeAdClosedDelegate onClosed, NativeAdPaidDelegate onPaid);

        [DllImport("__Internal")]
        private static extern bool _iosIsNativeAdReady(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _iosShowNativeAd(string adUnitId);

        [DllImport("__Internal")]
        private static extern void _iosHideNativeAd();

        [AOT.MonoPInvokeCallback(typeof(NativeAdLoadedDelegate))]
        private static void OnIosAdLoaded(string unitId) { if (Instance != null) Instance.HandleAdLoaded(unitId); }

        [AOT.MonoPInvokeCallback(typeof(NativeAdFailedDelegate))]
        private static void OnIosAdFailed(string unitId, string error) { if (Instance != null) Instance.HandleAdFailedToLoad(unitId, error); }

        [AOT.MonoPInvokeCallback(typeof(NativeAdClosedDelegate))]
        private static void OnIosAdClosed(string unitId) { if (Instance != null) Instance.HandleAdClosed(unitId); }

        [AOT.MonoPInvokeCallback(typeof(NativeAdPaidDelegate))]
        private static void OnIosAdPaid(string unitId, double value) { if (Instance != null) Instance.HandleAdPaid(unitId, value); }
#endif

        private class NativeAdCallbackProxy : AndroidJavaProxy
        {
            private readonly FullScreenNativeAdManager _manager;
            private readonly string _unitId;

            // Proxy nay lưu trữ UnitId để gọi ngược về đúng ID
            public NativeAdCallbackProxy(FullScreenNativeAdManager manager, string unitId) 
                : base("com.plugins.nativebridge.UnityNativeFullScreen$INativeAdCallback")
            {
                _manager = manager;
                _unitId = unitId;
            }

            public void onAdLoaded() => _manager.HandleAdLoaded(_unitId);
            public void onAdFailedToLoad(string error) => _manager.HandleAdFailedToLoad(_unitId, error);
            public void onAdClosed() => _manager.HandleAdClosed(_unitId);
            public void onAdPaid(double value) => _manager.HandleAdPaid(_unitId, value);
        }

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        public void RequestAd(string adUnit)
        {
            if (!_initialized)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                bridgeClass = new AndroidJavaClass("com.plugins.nativebridge.UnityNativeFullScreen");
#endif
                _initialized = true;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (bridgeClass != null && currentActivity != null)
            {
                var proxy = new NativeAdCallbackProxy(this, adUnit);
                bridgeClass.CallStatic("loadAd", currentActivity, adUnit, proxy);
            }
#elif UNITY_IOS && !UNITY_EDITOR
            _iosLoadNativeAd(adUnit, OnIosAdLoaded, OnIosAdFailed, OnIosAdClosed, OnIosAdPaid);
#endif
        }

        public bool IsAdReady(string adUnit)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (bridgeClass != null) return bridgeClass.CallStatic<bool>("isAdLoaded", adUnit);
#elif UNITY_IOS && !UNITY_EDITOR
            return _iosIsNativeAdReady(adUnit);
#endif
            return false;
        }

        public void ShowFullScreenAd(string adUnit, string where)
        {
            if (IsAdReady(adUnit))
            {
                _currentShowingUnitId = adUnit;
                _currentShowingWhere = where;
                HandleAdDisplayed(adUnit, where);

#if UNITY_ANDROID && !UNITY_EDITOR
                bridgeClass.CallStatic("showAd", currentActivity, adUnit);
#elif UNITY_IOS && !UNITY_EDITOR
                _iosShowNativeAd(adUnit);
#endif
            }
            else RequestAd(adUnit);
        }

        public void ForceCloseAd()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (bridgeClass != null && currentActivity != null) bridgeClass.CallStatic("hideAd", currentActivity);
#elif UNITY_IOS && !UNITY_EDITOR
            _iosHideNativeAd();
#endif
        }

        internal void HandleAdLoaded(string unitId) => MainThreadDispatcher.Enqueue(() => OnAdLoadedEvent?.Invoke(unitId));
        internal void HandleAdFailedToLoad(string unitId, string error) => MainThreadDispatcher.Enqueue(() => OnAdLoadFailedEvent?.Invoke(unitId, error));
        internal void HandleAdDisplayed(string unitId, string where) => MainThreadDispatcher.Enqueue(() => OnAdDisplayedEvent?.Invoke(unitId, where));
        internal void HandleAdClosed(string unitId) => MainThreadDispatcher.Enqueue(() => OnAdClosedEvent?.Invoke(unitId, _currentShowingWhere));
        internal void HandleAdPaid(string unitId, double value) => MainThreadDispatcher.Enqueue(() => OnAdPaidEvent?.Invoke(unitId, _currentShowingWhere, value));
    }
}