using System.IO;
using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// Save/load system using JSON serialization via JsonUtility.
    /// Files are stored in Application.persistentDataPath.
    /// </summary>
    public sealed class JsonSaveSystem : ISaveSystem
    {
        string GetPath(string key) => Path.Combine(Application.persistentDataPath, $"{key}.json");

        public void Save<T>(string key, T data) where T : class
        {
            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetPath(key), json);
            GLogger.Log("SaveSystem", $"Saved JSON: {key}");
        }

        public T Load<T>(string key) where T : class
        {
            var path = GetPath(key);
            if (!File.Exists(path))
            {
                GLogger.Warning("SaveSystem", $"File not found: {key}");
                return null;
            }
            var json = File.ReadAllText(path);
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
