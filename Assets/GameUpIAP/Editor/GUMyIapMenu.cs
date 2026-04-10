#if UNITY_EDITOR
using GameUp.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameUp.IAP.Editor
{
    public static class GUMyIapMenu
    {
        private const string CreateMenuPath = "GameUp/IAP/Create MyIAPManager";

        [MenuItem(CreateMenuPath)]
        public static void CreateMyIapManager()
        {
            var existedManager = Object.FindFirstObjectByType<MyIAPManager>();
            if (existedManager != null)
            {
                Selection.activeGameObject = existedManager.gameObject;
                GULogger.Warning("IAP", "MyIAPManager already exists in this scene. Selected existing object.");
                return;
            }

            var managerObject = new GameObject("MyIAPManager");
            Undo.RegisterCreatedObjectUndo(managerObject, "Create MyIAPManager");
            Undo.AddComponent<MyIAPManager>(managerObject);

            Selection.activeGameObject = managerObject;
            EditorSceneManager.MarkSceneDirty(managerObject.scene);
            GULogger.Log("IAP", "Created MyIAPManager GameObject in current scene.");
        }

        [MenuItem(CreateMenuPath, true)]
        private static bool ValidateCreateMyIapManager()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
#endif