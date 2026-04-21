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

        [SerializeField]
        protected ScriptableObject remoteConfigExtraData;
        private bool _remoteConfigReady;
        public bool IsRemoteConfigReady => _remoteConfigReady;
        public Action<bool> OnFetchCompleted;


        private static bool IsEditor()
        {
            return Application.platform == RuntimePlatform.OSXEditor ||
                   Application.platform == RuntimePlatform.WindowsEditor;
        }

        /// <summary>Áp dụng giá trị mặc định lên các field (dùng trong Editor và khi Firebase lỗi).</summary>
        protected void ApplyDefaultValues()
        {
            var defaults = GetDefaultValues();
            foreach (var kv in defaults)
            {
                try
                {
                    foreach (var target in GetRemoteConfigTargets())
                    {
                        BindingFieldsFromDefaults(kv, target);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GameUp] RemoteConfig UpdateKeys " + kv.Key + ": " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Default values dùng cho SetDefaultsAsync và fallback khi Firebase lỗi.
        /// Mặc định sẽ tự lấy tất cả public instance field (int/bool/string/float/double/long) trên các target.
        /// Dự án khác có thể override để thêm/ghi đè key tùy ý.
        /// </summary>
        protected virtual Dictionary<string, object> GetDefaultValues()
        {
            // Use current serialized values so Inspector tweaks are preserved in Play Mode.
            return BuildDefaultsFromTargets();
        }

        protected virtual IEnumerable<object> GetRemoteConfigTargets()
        {
            yield return this;
            if (remoteConfigExtraData != null) yield return remoteConfigExtraData;
        }

        protected virtual bool ShouldIncludeFieldAsDefault(FieldInfo field)
        {
            if (field == null) return false;
            if (!field.IsPublic) return false;
            if (field.IsStatic) return false;

            var t = field.FieldType;
            return t == typeof(int) ||
                   t == typeof(bool) ||
                   t == typeof(string) ||
                   t == typeof(float) ||
                   t == typeof(double) ||
                   t == typeof(long);
        }

        protected virtual Dictionary<string, object> BuildDefaultsFromTargets()
        {
            var defaults = new Dictionary<string, object>();

            foreach (var target in GetRemoteConfigTargets())
            {
                if (target == null) continue;

                var fields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (!ShouldIncludeFieldAsDefault(field)) continue;

                    // If multiple targets have the same key, the first one wins by default.
                    if (!defaults.ContainsKey(field.Name))
                    {
                        defaults[field.Name] = field.GetValue(target);
                    }
                }
            }

            return defaults;
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
            try
            {
                await _remoteConfig.EnsureInitializedAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameUp] FirebaseRemoteConfig EnsureInitializedAsync error: " + e);
            }

            bool activated = (await _remoteConfig.FetchAndActivateAsync());
            Debug.Log("[GameUp] FirebaseRemoteConfig activated: " + activated);
            Debug.Log("[GameUp] FirebaseRemoteConfig activated: " + _remoteConfig.GetValue("enable_rate_app").BooleanValue);
            UpdateKeysFromRemote();
            _remoteConfigReady = true;
            OnFetchCompleted?.Invoke(activated);
        }

        protected void UpdateKeysFromRemote()
        {
            if (_remoteConfig == null) return;

            foreach (string k in _remoteConfig.Keys)
            {
                try
                {
                    foreach (var target in GetRemoteConfigTargets())
                    {
                        BindingFields(k, target);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GameUp] RemoteConfig UpdateKeys " + k + ": " + ex.Message);
                }
            }
        }

        protected void BindingFields(string key, object o)
        {
            if (o == null) return;
            var field = o.GetType().GetField(key, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                if (field.FieldType == typeof(int))
                    field.SetValue(o, (int)_remoteConfig.GetValue(key).LongValue);
                else if (field.FieldType == typeof(long))
                    field.SetValue(o, _remoteConfig.GetValue(key).LongValue);
                else if (field.FieldType == typeof(bool))
                    field.SetValue(o, _remoteConfig.GetValue(key).BooleanValue);
                else if (field.FieldType == typeof(string))
                    field.SetValue(o, _remoteConfig.GetValue(key).StringValue);
                else if (field.FieldType == typeof(float))
                {
                    field.SetValue(o, (float)_remoteConfig.GetValue(key).DoubleValue);
                }
                else if (field.FieldType == typeof(double))
                {
                    field.SetValue(o, _remoteConfig.GetValue(key).DoubleValue);
                }
            }
            Debug.Log($"[GameUp] RemoteConfig UpdateKeys {key}: {_remoteConfig.GetValue(key)}");
        }

        protected void BindingFieldsFromDefaults(KeyValuePair<string, object> kv, object o)
        {
            if (o == null) return;
            var field = o.GetType().GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                Debug.Log(kv.Value);
                if (field.FieldType == typeof(int) && kv.Value is int i)
                    field.SetValue(o, i);
                else if (field.FieldType == typeof(long) && kv.Value is long l)
                    field.SetValue(o, l);
                else if (field.FieldType == typeof(bool) && kv.Value is bool b)
                    field.SetValue(o, b);
                else if (field.FieldType == typeof(string) && kv.Value is string s)
                    field.SetValue(o, s);
                else if (field.FieldType == typeof(float) && kv.Value is float f)
                    field.SetValue(o, f);
                else if (field.FieldType == typeof(double) && kv.Value is double d)
                    field.SetValue(o, d);
                Debug.Log($"[GameUp] RemoteConfig UpdateKeys {kv.Key}: {kv.Value}");
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
