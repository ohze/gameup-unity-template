using System;
using System.Collections.Generic;
using GameUp.Core;
using UnityEngine;
#if FIREBASE_DEPENDENCIES_INSTALLED
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Extensions;
#endif

namespace GameUp.SDK
{
    public class FirebaseUtils : MonoSingleton<FirebaseUtils>
    {
        private bool _initialized;
        public Action<bool> onInitialized;
        /// <summary>True khi Firebase đã init xong (dùng để RemoteConfig init sau).</summary>
        public bool FirebaseInitialized => _initialized;

#if FIREBASE_DEPENDENCIES_INSTALLED
        protected override void Awake()
        {
            base.Awake();
            FirebaseInit();
        }

        private void FirebaseInit()
        {
            if (!IsEditor())
            {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsCanceled || task.IsFaulted)
                    {
                        Debug.LogError("[Firebase] Init failed: " + (task.Exception?.Message ?? "Unknown"));
                        onInitialized?.Invoke(false);
                        return;
                    }

                    var dependencyStatus = task.Result;
                    if (dependencyStatus == DependencyStatus.Available)
                    {
                        Initialized();
                    }
                    else
                    {
                        Debug.LogError("[Firebase] Could not resolve dependencies: " + dependencyStatus);
                        onInitialized?.Invoke(false);
                    }
                });
            }
            else
            {
                onInitialized?.Invoke(true);
            }
        }

        private void Initialized()
        {
            _initialized = true;
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
            Crashlytics.IsCrashlyticsCollectionEnabled = true;
            onInitialized?.Invoke(true);
        }

        private bool IsEditor()
        {
            return Application.platform == RuntimePlatform.OSXEditor ||
                Application.platform == RuntimePlatform.WindowsEditor;
        }

        public static void LogEventsAPI(string eventId, Dictionary<object, object> param = null)
        {
            Instance._LogEvents(eventId, param);
        }

        /// <summary>
        /// Logs a single-parameter event. Pass null or empty paramName/paramValue for no params.
        /// </summary>
        public static void LogEvent(string eventName, string paramName, string paramValue)
        {
            if (string.IsNullOrEmpty(paramName) || paramValue == null)
            {
                Instance._LogEvents(eventName, null);
                return;
            }
            var param = new Dictionary<object, object> { { paramName, paramValue } };
            Instance._LogEvents(eventName, param);
        }

        public static void LogEvent(string eventName, Parameter[] parameters)
        {
            Instance._LogEventWithParameters(eventName, parameters);
        }

        private void _LogEventWithParameters(string eventId, Parameter[] parameters)
        {
            if (!_initialized) return;
            if (IsEditor())
            {
                Debug.Log("[Firebase] " + eventId);
                return;
            }
            if (parameters == null || parameters.Length == 0)
                FirebaseAnalytics.LogEvent(eventId);
            else
                FirebaseAnalytics.LogEvent(eventId, parameters);
        }

        #region Log Events

        private void _LogEvents(string eventId, Dictionary<object, object> param = null)
        {
            if (!_initialized) return;
            if (IsEditor())
            {
                Debug.Log("[Firebase] " + eventId);
                return;
            }

            if (param == null)
            {
                FirebaseAnalytics.LogEvent(eventId.ToString());
            }
            else
            {
                var parameters = new List<Parameter>();
                foreach (var p in param)
                {
                    if (p.Value != null)
                        parameters.Add(new Parameter(p.Key.ToString(), p.Value.ToString()));
                }
                FirebaseAnalytics.LogEvent(eventId.ToString(), parameters.ToArray());
            }
        }

        public void LogError(string error)
        {
            if (!_initialized) return;
            if (IsEditor()) { Debug.Log("[Firebase] " + error); return; }
            Crashlytics.Log(error);
        }

        public void LogException(Exception e)
        {
            if (!_initialized) return;
            if (IsEditor()) { Debug.Log("[Firebase] " + e.Message); return; }
            Crashlytics.LogException(e);
        }

        #endregion
#else
        protected override void Awake()
        {
            base.Awake();
            _initialized = true;
            onInitialized?.Invoke(true);
        }

        public static void LogEventsAPI(string eventId, Dictionary<object, object> param = null) { }
        public static void LogEvent(string eventName, string paramName, string paramValue) { }
        public void LogError(string error) { }
        public void LogException(Exception e) { }
#endif
    }
}
