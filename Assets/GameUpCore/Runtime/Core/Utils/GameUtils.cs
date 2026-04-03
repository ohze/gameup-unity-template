
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameUp.Core
{
    public static class GameUtils
    {
        #region Platform &  Device Info

        public static bool IsAndroid => Application.platform == RuntimePlatform.Android;
        public static bool IsIOS => Application.platform == RuntimePlatform.IPhonePlayer;
        public static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;
        public static bool IsEditor => Application.isEditor;

        public static string GetPlatform()
        {
#if UNITY_IOS
            return "iOS";
#elif UNITY_ANDROID
            return "Android";
#elif UNITY_WEBGL
            return "Web";
#else
            return "PC";
#endif
        }

        public static string GetDeviceId()
        {
            // Nếu là WebGL, SystemInfo.deviceUniqueIdentifier có thể không ổn định, dùng ID tự tạo
            if (IsWeb)
            {
                var s = LocalStorageUtils.GetDeviceID();
                if (string.IsNullOrEmpty(s))
                {
                    s = CreateID();
                    LocalStorageUtils.SetDeviceID(s);
                }
                return s;
            }
            return SystemInfo.deviceUniqueIdentifier;
        }

        public static string CreateID()
        {
            // Dùng Guid của System thay vì tự build thủ công bằng char code cho nhanh và chuẩn
            return Guid.NewGuid().ToString().ToUpper();
        }

        public static string GetVersion() => Application.version;
        public static string GetBundleId() => Application.identifier;

        #endregion

        #region Asset & File Management (Editor Only)

#if UNITY_EDITOR
        public static List<T> GetAssetList<T>(string path) where T : Object
        {
            return GetAssetList(path, new List<T>());
        }

        public static List<T> GetAssetList<T>(string path, List<T> result) where T : Object
        {
            if (result == null) result = new List<T>();
            result.Clear();

            if (string.IsNullOrWhiteSpace(path))
                return result;

            var folderPath = NormalizeAssetFolderPath(path);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                return result;

            // Use AssetDatabase indexing; this is significantly faster than System.IO scanning.
            var filter = $"t:{typeof(T).Name}";
            var guids = AssetDatabase.FindAssets(filter, new[] { folderPath });
            if (guids == null || guids.Length == 0)
                return result;

            if (result.Capacity < guids.Length) result.Capacity = guids.Length;

            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(assetPath)) continue;

                var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset) result.Add(asset);
            }

            return result;
        }

        /// <summary>
        /// Find prefab assets under a folder that contain component <typeparamref name="TComponent"/>.
        /// Returns the prefab root GameObject assets (so caller can get GUID/path reliably).
        /// </summary>
        public static List<GameObject> GetPrefabAssetsWithComponent<TComponent>(string path) where TComponent : Component
        {
            return GetPrefabAssetsWithComponent<TComponent>(path, new List<GameObject>());
        }

        public static List<GameObject> GetPrefabAssetsWithComponent<TComponent>(string path, List<GameObject> result)
            where TComponent : Component
        {
            if (result == null) result = new List<GameObject>();
            result.Clear();

            if (string.IsNullOrWhiteSpace(path))
                return result;

            var folderPath = NormalizeAssetFolderPath(path);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                return result;

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            if (guids == null || guids.Length == 0)
                return result;

            for (var i = 0; i < guids.Length; i++)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(prefabPath)) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (!prefab) continue;

                if (prefab.GetComponentInChildren<TComponent>(true) != null)
                    result.Add(prefab);
            }

            return result;
        }

        private static string NormalizeAssetFolderPath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                return string.Empty;

            var p = rawPath.Replace('\\', '/').Trim();
            p = p.TrimEnd('/');

            // Accept either "Assets/..." or paths relative to Assets.
            if (p.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                return "Assets";

            if (p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return p;

            return $"Assets/{p}";
        }

        public static void SaveAssets(Object target)
        {
            if (target == null) return;
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        }

        public static string GetAssetPath(Object o) => o == null ? "" : AssetDatabase.GetAssetPath(o);

        public static string GetGuid(Object o)
        {
            if (o == null) return "";
            var path = AssetDatabase.GetAssetPath(o);
            return AssetDatabase.AssetPathToGUID(path);
        }
#endif

        #endregion

        #region String & Math Utilities

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var res = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                res.Append(chars[Random.Range(0, chars.Length)]);
            }
            return res.ToString();
        }

        public static int GetMaxIndexHasValue(int[] arr, int targetValue)
        {
            if (arr == null) return -1;
            for (var i = arr.Length - 1; i >= 0; i--)
                if (arr[i] >= targetValue) return i;
            return -1;
        }

        public static Vector2 GetIntersectionPointCoordinates(Vector2 p1, Vector2 p2, Vector2 v1, Vector2 v2, out bool found)
        {
            var tmp = (v2.x - v1.x) * (p2.y - p1.y) - (v2.y - v1.y) * (p2.x - p1.x);
            if (Mathf.Approximately(tmp, 0))
            {
                found = false;
                return Vector2.zero;
            }

            var mu = ((p1.x - v1.x) * (p2.y - p1.y) - (p1.y - v1.y) * (p2.x - p1.x)) / tmp;
            found = true;
            return v1 + (v2 - v1) * mu;
        }

        #endregion

        #region Time Formatting

        public static string ConvertTimeSpanStr(TimeSpan timeSpan)
        {
            // Format chuẩn: 1D 05:30:15
            return string.Format("{0}D {1:D2}:{2:D2}:{3:D2}", timeSpan.Days, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
        }

        public static string GetSecondStr(int seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            if (t.TotalHours >= 1)
                return $"{(int)t.TotalHours} giờ {t.Minutes} phút";
            if (t.TotalMinutes >= 1)
                return $"{t.Minutes} phút {t.Seconds} giây";
            return $"{t.Seconds} giây";
        }

        public static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTimeStamp).DateTime.ToLocalTime();
        }

        public static long DateTimeToTimeStamp(DateTime dateTime)
        {
            return new DateTimeOffset(dateTime).ToUnixTimeSeconds();
        }

        public static int GetWeekOfYear(DateTime dateTime)
        {
            var cul = CultureInfo.CurrentCulture;
            return cul.Calendar.GetWeekOfYear(dateTime, CalendarWeekRule.FirstDay, DayOfWeek.Monday) - 1;
        }

        #endregion

        #region Physics & Transforms

        public static Quaternion ToRotationY(Vector3 from, Vector3 to)
        {
            var dir = from - to;
            if (dir == Vector3.zero) return Quaternion.identity;
            var look = Quaternion.LookRotation(dir);
            return Quaternion.Euler(0, look.eulerAngles.y, 0);
        }

        public static Texture2D TakeScreenShot()
        {
            var ss = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            // ReadPixels cần được gọi cuối Frame (nên dùng trong Coroutine WaitForEndOfFrame)
            ss.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            ss.Apply();
            return ss;
        }

        #endregion
    }
}