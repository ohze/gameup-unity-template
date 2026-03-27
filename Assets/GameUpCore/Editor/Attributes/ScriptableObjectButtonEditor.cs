#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Ve nut cho cac method [Button] tren ScriptableObject.
    /// </summary>
    [CustomEditor(typeof(ScriptableObject), true)]
    public class ScriptableObjectButtonEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            ButtonMethodDrawer.DrawButtons(target);
        }
    }
}
#endif
