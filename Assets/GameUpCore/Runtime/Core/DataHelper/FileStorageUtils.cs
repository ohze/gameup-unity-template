using System;
using System.IO;
using UnityEngine;
using GameUp.Core.Serializer;

namespace GameUp.Core
{
    public static class FileStorageUtils
    {
        private static string GetPath(string key) => Path.Combine(Application.persistentDataPath, key + ".dat");

        /// <summary>
        /// Lưu object (Dictionary, List, class...) dùng FullSerializer — hỗ trợ Dictionary, polymorphism.
        /// </summary>
        public static void SaveData<T>(string key, T data, bool encrypt = true)
        {
            try
            {
                string json = data != null ? data.Serialize() : string.Empty;
                if (encrypt) json = EncryptUtils.Encrypt(json);

                File.WriteAllText(GetPath(key), json);
            }
            catch (Exception e)
            {
                GULogger.Error("FileStorage", $"Save failed: {e.Message}");
            }
        }

        /// <summary>
        /// Đọc object (Dictionary, List, class...) đã lưu bằng SaveData.
        /// </summary>
        public static T LoadData<T>(string key, bool isEncrypted = true)
        {
            string path = GetPath(key);
            if (!File.Exists(path)) return default;

            try
            {
                string content = File.ReadAllText(path);
                if (isEncrypted) content = EncryptUtils.Decrypt(content);

                return content.Deserialize<T>();
            }
            catch (Exception e)
            {
                GULogger.Error("FileStorage", $"Load failed: {e.Message}");
                return default;
            }
        }
    }
}