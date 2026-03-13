using UnityEngine;

namespace GameUp.Core
{
    public static class MonoExtension
    {
        // --- HIỂN THỊ / ẨN ---
        public static void Hide(this GameObject go) => go.SetActive(false);
        public static void Show(this GameObject go) => go.SetActive(true);
        
        // Chỉ cần Component là bao quát được cả MonoBehaviour và Transform
        public static void Hide(this Component c) => c.gameObject.SetActive(false);
        public static void Show(this Component c) => c.gameObject.SetActive(true);

        // --- TRANSFORM & VỊ TRÍ ---
        public static void Reset(this Transform t, Transform parent = null)
        {
            t.SetParent(parent);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        // Đặt vị trí X, Y hoặc Z độc lập (tránh tạo Vector3 mới thủ công)
        public static void SetPosX(this Transform t, float x) => t.position = new Vector3(x, t.position.y, t.position.z);
        public static void SetPosY(this Transform t, float y) => t.position = new Vector3(t.position.x, y, t.position.z);

        // --- QUẢN LÝ COMPONENT ---
        public static T GetOrAdd<T>(this GameObject go) where T : Component 
        {
            return go.TryGetComponent<T>(out var comp) ? comp : go.AddComponent<T>();
        }

        // --- LỚP & VẬT LÝ ---
        public static void SetLayerRecursive(this GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                child.gameObject.SetLayerRecursive(layer);
            }
        }

        // --- ROTATION ---
        public static void RotateTarget(this Transform a, Transform target, float speed)
        {
            if (target == null) return;
            Vector3 direction = target.position - a.position;
            if (direction == Vector3.zero) return;
            
            Quaternion rotation = Quaternion.LookRotation(direction);
            rotation.x = 0;
            rotation.z = 0;
            a.rotation = Quaternion.Slerp(a.rotation, rotation, Time.deltaTime * speed);
        }
    }
}