using System.IO;
using UnityEngine;
using System;

namespace GameUp.Core
{
    public static class FileStorageUtils
    {
        private static string GetPath(string key) => Path.Combine(Application.persistentDataPath, key + ".dat");

        public static void SaveData<T>(string key, T data, bool encrypt = true)
        {
            try
            {
                string json = JsonUtility.ToJson(data);
                if (encrypt) json = EncryptUtils.Encrypt(json);
                
                File.WriteAllText(GetPath(key), json);
            }
            catch (Exception e)
            {
                GULogger.Error("FileStorage", $"Save failed: {e.Message}");
            }
        }

        public static T LoadData<T>(string key, bool isEncrypted = true)
        {
            string path = GetPath(key);
            if (!File.Exists(path)) return default;

            try
            {
                string content = File.ReadAllText(path);
                if (isEncrypted) content = EncryptUtils.Decrypt(content);
                
                return JsonUtility.FromJson<T>(content);
            }
            catch (Exception e)
            {
                GULogger.Error("FileStorage", $"Load failed: {e.Message}");
                return default;
            }
        }
    }
}