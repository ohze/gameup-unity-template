using UnityEngine;

namespace GameUp.Core
{
    /// <summary>
    /// Loads configuration data from ScriptableObjects (Resources) or JSON (TextAsset).
    /// </summary>
    public static class ConfigLoader
    {
        /// <summary>Load a ScriptableObject config from Resources folder by name.</summary>
        public static T LoadSO<T>(string resourcePath) where T : GameConfig
        {
            var config = Resources.Load<T>(resourcePath);
            if (config == null)
            {
                GLogger.Error("ConfigLoader", $"Failed to load SO config at: {resourcePath}");
                return null;
            }
            config.OnLoaded();
            return config;
        }

        /// <summary>Load and deserialize a JSON TextAsset from Resources into type T.</summary>
        public static T LoadJson<T>(string resourcePath) where T : class
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
            {
                GLogger.Error("ConfigLoader", $"Failed to load JSON at: {resourcePath}");
                return null;
            }
            return JsonUtility.FromJson<T>(textAsset.text);
        }

        /// <summary>Deserialize a raw JSON string into type T.</summary>
        public static T FromJsonString<T>(string json) where T : class
        {
            return JsonUtility.FromJson<T>(json);
        }
    }
}
