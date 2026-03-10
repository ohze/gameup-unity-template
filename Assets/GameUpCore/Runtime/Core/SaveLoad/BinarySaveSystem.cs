using System.IO;
using System.Text;
using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// Save/load using JSON serialized to binary (UTF-8 bytes).
    /// Provides basic obfuscation — not encryption. For AES, extend this class.
    /// Files are stored in Application.persistentDataPath.
    /// </summary>
    public sealed class BinarySaveSystem : ISaveSystem
    {
        string GetPath(string key) => Path.Combine(Application.persistentDataPath, $"{key}.dat");

        public void Save<T>(string key, T data) where T : class
        {
            var json = JsonUtility.ToJson(data);
            var bytes = Encoding.UTF8.GetBytes(json);
            File.WriteAllBytes(GetPath(key), bytes);
            GLogger.Log("SaveSystem", $"Saved binary: {key}");
        }

        public T Load<T>(string key) where T : class
        {
            var path = GetPath(key);
            if (!File.Exists(path))
            {
                GLogger.Warning("SaveSystem", $"Binary file not found: {key}");
                return null;
            }
            var bytes = File.ReadAllBytes(path);
            var json = Encoding.UTF8.GetString(bytes);
            return JsonUtility.FromJson<T>(json);
        }

        public bool Exists(string key) => File.Exists(GetPath(key));

        public void Delete(string key)
        {
            var path = GetPath(key);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
