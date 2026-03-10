using UnityEngine;

namespace GameUp.Extensions
{
    public static class VectorExtensions
    {
        public static Vector2 WithX(this Vector2 v, float x) => new(x, v.y);
        public static Vector2 WithY(this Vector2 v, float y) => new(v.x, y);

        public static Vector3 WithX(this Vector3 v, float x) => new(x, v.y, v.z);
        public static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z);
        public static Vector3 WithZ(this Vector3 v, float z) => new(v.x, v.y, z);

        public static Vector2 ToVector2XY(this Vector3 v) => new(v.x, v.y);
        public static Vector2 ToVector2XZ(this Vector3 v) => new(v.x, v.z);

        public static Vector3 ToVector3XY(this Vector2 v, float z = 0f) => new(v.x, v.y, z);
        public static Vector3 ToVector3XZ(this Vector2 v, float y = 0f) => new(v.x, y, v.y);

        /// <summary>Returns a random point inside a circle of given radius.</summary>
        public static Vector2 RandomInsideCircle(float radius = 1f)
            => Random.insideUnitCircle * radius;

        /// <summary>Flat distance ignoring Y axis.</summary>
        public static float FlatDistance(this Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static Vector3 DirectionTo(this Vector3 from, Vector3 to)
            => (to - from).normalized;
    }
}
