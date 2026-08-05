using UnityEngine;

namespace GameUp.Core
{
    public static class GUPool
    {
        /// <summary>
        /// Tạo sẵn instance để lần Spawn đầu không khựng. Nên gọi ở màn Loading
        /// cho các prefab sinh ra nhiều (đạn, hiệu ứng, item danh sách...).
        /// </summary>
        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab) GUPoolers.Instance.Prewarm(prefab, count);
        }

        /// <inheritdoc cref="Prewarm(GameObject,int)"/>
        public static void Prewarm<T>(T prefab, int count) where T : Component
        {
            if (prefab) GUPoolers.Instance.Prewarm(prefab.gameObject, count);
        }

        public static T Spawn<T>(T prefab, Transform parent = null, bool worldPositionStays = false)
            where T : Component
        {
            return GUPoolers.Instance.Spawn(prefab, parent, worldPositionStays);
        }

        /// <summary>This allows you to spawn a prefab via Component.</summary>
        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            where T : Component
        {
            // Clone this component's GameObject
            return GUPoolers.Instance.Spawn(prefab, position, rotation, parent);
        }

        /// <summary>This allows you to spawn a prefab via GameObject.</summary>
        public static GameObject Spawn(GameObject prefab, Transform parent = null, bool worldPositionStays = false)
        {
            if (prefab) return GUPoolers.Instance.Spawn(prefab, parent, worldPositionStays);

            GULogger.Error("GUPool", "Attempting to spawn a null prefab.");

            return null;
        }

        /// <summary>This allows you to spawn a prefab via GameObject.</summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null,
            bool worldPositionStays = true)
        {
            if (prefab) return GUPoolers.Instance.Spawn(prefab, position, rotation, parent);

            GULogger.Error("GUPool", "Attempting to spawn a null prefab.");

            return null;
        }

        /// <summary>This allows you to despawn a clone via Component, with optional delay.</summary>
        public static void DeSpawn(Component clone, float delay = 0.0f)
        {
            if (clone) DeSpawn(clone.gameObject, delay);
        }

        /// <summary>This allows you to despawn a clone via GameObject, with optional delay.</summary>
        public static void DeSpawn(GameObject clone, float delay)
        {
            if (clone) GUPoolers.Instance.DeSpawn(clone, delay);
        }

        /// <summary>This allows you to despawn a clone via GameObject, with optional delay.</summary>
        public static void DeSpawn(GameObject clone)
        {
            if (clone) GUPoolers.Instance.DeSpawn(clone);
        }

        public static void DeSpawnAll(GameObject prefab)
        {
            if (prefab) GUPoolers.Instance.DeSpawnAll(prefab);
        }

        public static void DeSpawnAll<T>(T prefab) where T : Component
        {
            if (prefab) GUPoolers.Instance.DeSpawnAll(prefab);
        }
    }
}