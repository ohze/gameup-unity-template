#if UNITY_EDITOR
using GameUp.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameUp.Core.Editor
{
    public static class GUPoolersMenu
    {
        private const string MenuPath = "GameUp/Poolers/Setup GUPoolers in Scene";
        private const string SingletonName = "GUPoolersSingleton";

        [MenuItem(MenuPath)]
        public static void SetupGUPoolers()
        {
            var existing = Object.FindObjectOfType<GUPoolers>();
            if (existing)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                GULogger.Log("GUPoolers", $"Scene đã có GUPoolers trên \"{existing.gameObject.name}\", đã chọn.");
                return;
            }

            var go = new GameObject(SingletonName);
            go.AddComponent<GUPoolers>();
            Undo.RegisterCreatedObjectUndo(go, "Create GUPoolers Singleton");

            var scene = SceneManager.GetActiveScene();
            if (scene.isDirty == false)
                EditorSceneManager.MarkSceneDirty(scene);

            Selection.activeGameObject = go;
            GULogger.Log("GUPoolers", $"Đã tạo \"{SingletonName}\" và gắn GUPoolers.");
        }
    }
}
#endif
