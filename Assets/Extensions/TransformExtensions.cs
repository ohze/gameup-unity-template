using UnityEngine;

namespace GameUp.Extensions
{
    public static class TransformExtensions
    {
        public static void SetPositionX(this Transform t, float x)
        {
            var pos = t.position;
            pos.x = x;
            t.position = pos;
        }

        public static void SetPositionY(this Transform t, float y)
        {
            var pos = t.position;
            pos.y = y;
            t.position = pos;
        }

        public static void SetPositionZ(this Transform t, float z)
        {
            var pos = t.position;
            pos.z = z;
            t.position = pos;
        }

        public static void SetLocalPositionX(this Transform t, float x)
        {
            var pos = t.localPosition;
            pos.x = x;
            t.localPosition = pos;
        }

        public static void SetLocalPositionY(this Transform t, float y)
        {
            var pos = t.localPosition;
            pos.y = y;
            t.localPosition = pos;
        }

        public static void SetLocalScaleUniform(this Transform t, float scale)
        {
            t.localScale = new Vector3(scale, scale, scale);
        }

        public static void ResetLocal(this Transform t)
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        public static void DestroyAllChildren(this Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Object.Destroy(t.GetChild(i).gameObject);
        }

        public static void DestroyAllChildrenImmediate(this Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(t.GetChild(i).gameObject);
        }

        /// <summary>Set active state and return the transform for chaining.</summary>
        public static Transform SetActiveAndReturn(this Transform t, bool active)
        {
            t.gameObject.SetActive(active);
            return t;
        }
    }
}
