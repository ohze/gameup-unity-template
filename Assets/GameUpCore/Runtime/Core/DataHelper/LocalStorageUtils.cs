using UnityEngine;

namespace GameUp.Core
{
    public static class LocalStorageUtils
    {
        private const string DEVICE_ID = "dv";

        public static bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public static string GetString(string key, string defaultStr = "")
        {
            var value = PlayerPrefs.GetString(key);
            if (!string.IsNullOrEmpty(value)) return EncryptUtils.Decrypt(value);

            return defaultStr;
        }

        public static void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, EncryptUtils.Encrypt(value));
        }

        public static void SetInt(string key, int value)
        {
            PlayerPrefs.SetString(key, EncryptUtils.Encrypt(value.ToString()));
        }

        public static int GetInt(string key, int d = 0)
        {
            var value = PlayerPrefs.GetString(key);
            if (!string.IsNullOrEmpty(value)) return int.Parse(EncryptUtils.Decrypt(value));

            return d;
        }

        public static void SetLong(string key, long value)
        {
            PlayerPrefs.SetString(key, EncryptUtils.Encrypt(value.ToString()));
        }

        public static long GetLong(string key, long d = 0)
        {
            var value = PlayerPrefs.GetString(key);
            if (!string.IsNullOrEmpty(value)) return long.Parse(EncryptUtils.Decrypt(value));

            return d;
        }

        public static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetString(key, EncryptUtils.Encrypt(value.ToString()));
        }

        public static float GetFloat(string key, float d = 0)
        {
            var value = PlayerPrefs.GetString(key);
            if (!string.IsNullOrEmpty(value)) return float.Parse(EncryptUtils.Decrypt(value));

            return d;
        }

        public static string GetDeviceID()
        {
            return GetString(DEVICE_ID);
        }

        public static void SetDeviceID(string id)
        {
            SetString(DEVICE_ID, id);
        }

        public static void SetBoolean(string key, bool v)
        {
            SetInt(key, v ? 1 : 0);
        }

        public static bool GetBoolean(string key, bool v = default)
        {
            return GetInt(key, v ? 1 : 0) == 1;
        }
    }
}