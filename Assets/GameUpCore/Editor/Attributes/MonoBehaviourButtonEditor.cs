#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Ve nut cho cac method [Button] tren MonoBehaviour.
    /// </summary>
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class MonoBehaviourButtonEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            ButtonMethodDrawer.DrawButtons(target);
        }
    }
}
#endif
