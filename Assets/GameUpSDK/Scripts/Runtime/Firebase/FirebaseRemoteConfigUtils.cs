using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using GameUp.Core;
using UnityEngine.Serialization;
#if FIREBASE_DEPENDENCIES_INSTALLED
using Firebase;
using Firebase.Extensions;
using Firebase.RemoteConfig;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// Firebase Remote Config: tÃªn biáº¿n trÃ¹ng vá»›i key trÃªn Remote Ä‘á»ƒ tá»± Ä‘á»™ng map (reflection).
    /// Number â†’ int, Boolean â†’ bool.
    /// </summary>
    public class FirebaseRemoteConfigUtils : MonoSingleton<FirebaseRemoteConfigUtils>
    {
        // ---------- Config (tÃªn biáº¿n = key trÃªn Remote Config) ----------
        /// <summary>Khoáº£ng thá»i gian tá»‘i thiá»ƒu (giÃ¢y) giá»¯a 2 láº§n hiá»ƒn thá»‹ Interstitial.</summary>
        public int inter_capping_time = 120;
        /// <summary>Level báº¯t Ä‘áº§u hiá»‡n Interstitial (level tÃ­nh tá»« 1).</summary>
        public int inter_start_level = 3;
        /// <summary>Táº¯t/Báº­t hiá»ƒn thá»‹ Rate App trong Game.</summary>
        public bool enable_rate_app = false;
        /// <summary>Level hiá»‡n Rate App.</summary>
        public int level_start_show_rate_app = 5;
        /// <summary>Táº¯t/Báº­t hiá»ƒn thá»‹ Popup yÃªu cáº§u Internet.</summary>
        public bool no_internet_popup_enable = true;
        /// <summary>Táº¯t/Báº­t hiá»ƒn thá»‹ Banner trong Game. Æ¯u tiÃªn cao hÆ¡n AdsManager.showBannerAfterInit: náº¿u false thÃ¬ khÃ´ng show banner (ká»ƒ cáº£ khi showBannerAfterInit = true).</summary>
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

        /// <summary>Ãp dá»¥ng giÃ¡ trá»‹ máº·c Ä‘á»‹nh lÃªn cÃ¡c field (dÃ¹ng trong Editor vÃ  khi Firebase lá»—i).</summary>
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
                    GULogger.Warning("GameUp", "RemoteConfig UpdateKeys " + kv.Key + ": " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Default values dÃ¹ng cho SetDefaultsAsync vÃ  fallback khi Firebase lá»—i.
        /// Máº·c Ä‘á»‹nh sáº½ tá»± láº¥y táº¥t cáº£ public instance field (int/bool/string/float/double/long) trÃªn cÃ¡c target.
        /// Dá»± Ã¡n khÃ¡c cÃ³ thá»ƒ override Ä‘á»ƒ thÃªm/ghi Ä‘Ã¨ key tÃ¹y Ã½.
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

        protected void BindingFieldsFromDefaults(KeyValuePair<string, object> kv, object o)
        {
            if (o == null) return;
            var field = o.GetType().GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance);
            if (field == null) return;

            try
            {
                object value = ConvertDefaultValue(kv.Value, field.FieldType);
                if (value == null && field.FieldType.IsValueType)
                {
                    // Value types cannot be assigned null.
                    return;
                }

                field.SetValue(o, value);
                GULogger.Log("GameUp", $"RemoteConfig UpdateKeys {kv.Key}: {kv.Value}");
            }
            catch (Exception ex)
            {
                GULogger.Warning("GameUp", $"RemoteConfig BindingDefaults {kv.Key} failed: {ex.Message}");
            }
        }

        private static object ConvertDefaultValue(object value, Type targetType)
        {
            if (targetType == null) return null;
            if (value == null) return null;

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            try
            {
                if (targetType == typeof(int))
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                if (targetType == typeof(long))
                    return Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (targetType == typeof(bool))
                {
                    if (value is string s)
                    {
                        if (bool.TryParse(s, out var parsedBool)) return parsedBool;
                        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt)) return parsedInt != 0;
                    }
                    if (value is IConvertible)
                        return Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
                }
                if (targetType == typeof(string))
                    return value.ToString();
                if (targetType == typeof(float))
                    return Convert.ToSingle(value, CultureInfo.InvariantCulture);
                if (targetType == typeof(double))
                    return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                // Let caller handle the fallback path.
            }

            return null;
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
                GULogger.Log("GameUp", "FirebaseRemoteConfig: Editor mode - using default values for testing.");
                return;
            }

            if (FirebaseUtils.Instance.IsInitialized)
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
                GULogger.Warning("GameUp", "FirebaseRemoteConfig: Firebase init failed, using defaults.");
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
                    GULogger.Warning("GameUp", "FirebaseRemoteConfig: FirebaseApp null, using defaults.");
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
                GULogger.Warning("GameUp", "FirebaseRemoteConfig EnsureInitializedAsync error: " + e);
            }

            bool activated = (await _remoteConfig.FetchAndActivateAsync());
            GULogger.Log("GameUp", "FirebaseRemoteConfig activated: " + activated);
            GULogger.Log("GameUp", "FirebaseRemoteConfig activated: " + _remoteConfig.GetValue("enable_rate_app").BooleanValue);
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
                    GULogger.Warning("GameUp", "RemoteConfig UpdateKeys " + k + ": " + ex.Message);
                }
            }
        }

        protected void BindingFields(string key, object o)
        {
            if (o == null) return;

            var field = o.GetType().GetField(key, BindingFlags.Public | BindingFlags.Instance);
            if (field == null) return;

            var configValue = _remoteConfig.GetValue(key);
            object assignedValue;

            switch (Type.GetTypeCode(field.FieldType))
            {
                case TypeCode.Int32:
                    assignedValue = (int)configValue.LongValue;
                    break;
                case TypeCode.Int64:
                    assignedValue = configValue.LongValue;
                    break;
                case TypeCode.Boolean:
                    assignedValue = configValue.BooleanValue;
                    break;
                case TypeCode.String:
                    assignedValue = configValue.StringValue;
                    break;
                case TypeCode.Single: // float
                    assignedValue = (float)configValue.DoubleValue;
                    break;
                case TypeCode.Double:
                    assignedValue = configValue.DoubleValue;
                    break;
                default:
                    GULogger.Warning("GameUp", $"RemoteConfig key '{key}' unsupported type: {field.FieldType}");
                    return;
            }

            field.SetValue(o, assignedValue);
            GULogger.Log("GameUp", $"RemoteConfig UpdateKeys {key}: {assignedValue} (source={configValue.Source})");
        }

        /// <summary>Fetch vÃ  activate config (gá»i láº¡i khi cáº§n refresh).</summary>
        public void FetchAndActivate(Action<bool> onDone = null)
        {
            if (_remoteConfig == null) { onDone?.Invoke(false); return; }
            _remoteConfig.FetchAndActivateAsync().ContinueWithOnMainThread(task =>
            {
                bool ok = task.IsCompletedSuccessfully && task.Result;
                if (task.IsFaulted && task.Exception != null)
                    GULogger.Warning("GameUp", "RemoteConfig FetchAndActivate: " + task.Exception.Message);
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
