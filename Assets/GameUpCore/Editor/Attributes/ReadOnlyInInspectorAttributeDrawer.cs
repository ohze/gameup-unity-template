#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    [CustomPropertyDrawer(typeof(ReadOnlyInInspectorAttribute))]
    public sealed class ReadOnlyInInspectorAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}
#endif
