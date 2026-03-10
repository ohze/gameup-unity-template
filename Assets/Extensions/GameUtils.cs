using System.Collections.Generic;
using UnityEngine;

namespace GameUp.Extensions
{
    /// <summary>
    /// Common game logic utilities: weighted random, math helpers, distance checks.
    /// </summary>
    public static class GameUtils
    {
        /// <summary>Pick a random index from a list of weights.</summary>
        public static int WeightedRandom(IList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += weights[i];

            float roll = Random.Range(0f, total);
            float cumulative = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative) return i;
            }
            return weights.Count - 1;
        }

        /// <summary>Remap a value from one range to another.</summary>
        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float t = Mathf.InverseLerp(fromMin, fromMax, value);
            return Mathf.Lerp(toMin, toMax, t);
        }

        /// <summary>Check if two positions are within range (2D, ignoring Z).</summary>
        public static bool IsInRange2D(Vector2 a, Vector2 b, float range)
            => (a - b).sqrMagnitude <= range * range;

        /// <summary>Check if two positions are within range (3D).</summary>
        public static bool IsInRange3D(Vector3 a, Vector3 b, float range)
            => (a - b).sqrMagnitude <= range * range;

        /// <summary>Smooth damp angle that wraps correctly around 360 degrees.</summary>
        public static float SmoothDampAngle(float current, float target, ref float velocity, float smoothTime)
            => Mathf.SmoothDampAngle(current, target, ref velocity, smoothTime);

        /// <summary>Returns a random point on the edge of a circle.</summary>
        public static Vector2 RandomOnCircle(float radius = 1f)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        /// <summary>Chance check: returns true with given probability (0-1).</summary>
        public static bool Chance(float probability)
            => Random.value < probability;

        /// <summary>Snap value to nearest increment.</summary>
        public static float Snap(float value, float increment)
            => Mathf.Round(value / increment) * increment;
    }
}
