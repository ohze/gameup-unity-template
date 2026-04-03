using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using GameUp.Core;
using UnityEngine;
#if FIREBASE_DEPENDENCIES_INSTALLED
using Firebase;
using Firebase.Extensions;
using Firebase.RemoteConfig;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// Firebase Remote Config: tên biến trùng với key trên Remote để tự động map (reflection).
    /// Number → int, Boolean → bool.
    /// </summary>
    public class FirebaseRemoteConfigUtils : MonoSingleton<FirebaseRemoteConfigUtils>
    {
        // ---------- Config (tên biến = key trên Remote Config) ----------
        /// <summary>Khoảng thời gian tối thiểu (giây) giữa 2 lần hiển thị Interstitial.</summary>
        public int inter_capping_time = 120;
        /// <summary>Level bắt đầu hiện Interstitial (level tính từ 1).</summary>
        public int inter_start_level = 3;
        /// <summary>Tắt/Bật hiển thị Rate App trong Game.</summary>
        public bool enable_rate_app = false;
        /// <summary>Level hiện Rate App.</summary>
        public int level_start_show_rate_app = 5;
        /// <summary>Tắt/Bật hiển thị Popup yêu cầu Internet.</summary>
        public bool no_internet_popup_enable = true;
        /// <summary>Tắt/Bật hiển thị Banner trong Game. Ưu tiên cao hơn AdsManager.showBannerAfterInit: nếu false thì không show banner (kể cả khi showBannerAfterInit = true).</summary>
        public bool enable_banner = true;

        private bool _remoteConfigReady;
        public bool IsRemoteConfigReady => _remoteConfigReady;
        public Action<bool> OnFetchCompleted;

        private static bool IsEditor()
        {
            return Application.platform == RuntimePlatform.OSXEditor ||
                   Application.platform == RuntimePlatform.WindowsEditor;
        }

        /// <summary>Áp dụng giá trị mặc định lên các field (dùng trong Editor và khi Firebase lỗi).</summary>
        private void ApplyDefaultValues()
        {
            var defaults = GetDefaultValues();
            foreach (var kv in defaults)
            {
                try
                {
                    var field = GetType().GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null) continue;
                    if (field.FieldType == typeof(int) && kv.Value is int i)
                        field.SetValue(this, i);
                    else if (field.FieldType == typeof(bool) && kv.Value is bool b)
                        field.SetValue(this, b);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GameUp] RemoteConfig ApplyDefault " + kv.Key + ": " + ex.Message);
                }
            }
        }

        private static Dictionary<string, object> GetDefaultValues()
        {
            return new Dictionary<string, object>
            {
                { "inter_capping_time", 120 },
                { "inter_start_level", 3 },
                { "enable_rate_app", false },
                { "level_start_show_rate_app", 5 },
                { "no_internet_popup_enable", true },
                { "enable_banner", true }
            };
        }

#if FIREBASE_DEPENDENCIES_INSTALLED
        private FirebaseRemoteConfig _remoteConfig;

        private void Start()
        {
            if (IsEditor())
            {
                ApplyDefaultValues();
                _remoteConfigReady = true;
                OnFetchCompleted?.Invoke(true);
                Debug.Log("[GameUp] FirebaseRemoteConfig: Editor mode - using default values for testing.");
                return;
            }

            if (FirebaseUtils.Instance.FirebaseInitialized)
            {
                InitRemoteConfig();
                return;
            }

            FirebaseUtils.Instance.onInitialized += OnFirebaseInitialized;
        }

        private void OnFirebaseInitialized(bool success)
        {
            FirebaseUtils.Instance.onInitialized -= OnFirebaseInitialized;
            if (!success)
            {
                Debug.LogWarning("[GameUp] FirebaseRemoteConfig: Firebase init failed, using defaults.");
                ApplyDefaultValues();
                _remoteConfigReady = true;
                OnFetchCompleted?.Invoke(false);
                return;
            }
            InitRemoteConfig();
        }

        private async void InitRemoteConfig()
        {
            try
            {
                var app = FirebaseApp.DefaultInstance;
                if (app == null)
                {
                    Debug.LogWarning("[GameUp] FirebaseRemoteConfig: FirebaseApp null, using defaults.");
                    ApplyDefaultValues();
                    _remoteConfigReady = true;
                    OnFetchCompleted?.Invoke(false);
                    return;
                }

                await SetupAndFetchAsync(app);
            }
            catch (Exception e)
            {
                Debug.LogError("[GameUp] FirebaseRemoteConfig init error: " + e);
                ApplyDefaultValues();
                _remoteConfigReady = true;
                OnFetchCompleted?.Invoke(false);
            }
        }

        private async Task SetupAndFetchAsync(FirebaseApp app)
        {
            _remoteConfig = FirebaseRemoteConfig.GetInstance(app);
            await _remoteConfig.SetDefaultsAsync(GetDefaultValues());
            await _remoteConfig.EnsureInitializedAsync();

            bool activated = (await _remoteConfig.FetchAndActivateAsync());
            UpdateKeysFromRemote();
            _remoteConfigReady = true;
            OnFetchCompleted?.Invoke(activated);
        }

        private void UpdateKeysFromRemote()
        {
            if (_remoteConfig == null) return;

            Type type = GetType();
            foreach (string k in _remoteConfig.Keys)
            {
                try
                {
                    FieldInfo field = type.GetField(k, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null) continue;

                    if (field.FieldType == typeof(int))
                        field.SetValue(this, (int)_remoteConfig.GetValue(k).LongValue);
                    else if (field.FieldType == typeof(bool))
                        field.SetValue(this, _remoteConfig.GetValue(k).BooleanValue);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GameUp] RemoteConfig UpdateKeys " + k + ": " + ex.Message);
                }
            }
        }

        /// <summary>Fetch và activate config (gọi lại khi cần refresh).</summary>
        public void FetchAndActivate(Action<bool> onDone = null)
        {
            if (_remoteConfig == null) { onDone?.Invoke(false); return; }
            _remoteConfig.FetchAndActivateAsync().ContinueWithOnMainThread(task =>
            {
                bool ok = task.IsCompletedSuccessfully && task.Result;
                if (task.IsFaulted && task.Exception != null)
                    Debug.LogWarning("[GameUp] RemoteConfig FetchAndActivate: " + task.Exception.Message);
                if (ok) UpdateKeysFromRemote();
                onDone?.Invoke(ok);
            });
        }
#else
        private void Start()
        {
            ApplyDefaultValues();
            _remoteConfigReady = true;
            OnFetchCompleted?.Invoke(true);
        }

        public void FetchAndActivate(Action<bool> onDone = null) => onDone?.Invoke(false);
#endif
    }
}
