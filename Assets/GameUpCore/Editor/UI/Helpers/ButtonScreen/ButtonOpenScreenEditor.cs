using System.Collections.Generic;
using GameUp.Core;
using GameUp.Core.UI;
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor.UI
{
    [CustomEditor(typeof(ButtonOpenScreen))]
    public class ButtonOpenScreenEditor : UnityEditor.Editor
    {
        private const string ScreenDataResourcePath = "Data/ScreenData";

        private SerializedProperty _btnProp;
        private SerializedProperty _rememberProp;
        private SerializedProperty _screenTypeNameProp;

        private void OnEnable()
        {
            _btnProp = serializedObject.FindProperty("btn");
            _rememberProp = serializedObject.FindProperty("rememberInHistory");
            _screenTypeNameProp = serializedObject.FindProperty("screenTypeName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_btnProp);
            EditorGUILayout.PropertyField(_rememberProp);

            CollectScreenOptions(out var labels, out var typeNames);

            var current = _screenTypeNameProp.stringValue ?? string.Empty;
            var selectedIndex = 0;
            for (var i = 1; i < typeNames.Length; i++)
            {
                if (string.Equals(typeNames[i], current, System.StringComparison.Ordinal))
                {
                    selectedIndex = i;
                    break;
                }
            }

            var newIndex = EditorGUILayout.Popup("Screen", selectedIndex, labels);
            if (newIndex != selectedIndex && newIndex >= 0 && newIndex < typeNames.Length)
            {
                _screenTypeNameProp.stringValue = typeNames[newIndex];
            }

            if (GUILayout.Button("Ping ScreenData asset"))
            {
                var data = Resources.Load<ScreenData>(ScreenDataResourcePath);
                if (data != null)
                {
                    EditorGUIUtility.PingObject(data);
                }
                else
                {
                    GULogger.Warning("ButtonOpenScreenEditor", $"Resources.Load failed: `{ScreenDataResourcePath}`.");
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void CollectScreenOptions(out string[] labels, out string[] typeNames)
        {
            var data = Resources.Load<ScreenData>(ScreenDataResourcePath);
            if (data == null)
            {
                labels = new[] { "(No ScreenData in Resources)" };
                typeNames = new[] { string.Empty };
                return;
            }

            var so = new SerializedObject(data);
            so.Update();
            var screensProp = so.FindProperty("screens");
            if (screensProp == null || !screensProp.isArray)
            {
                labels = new[] { "(ScreenData.screens missing)" };
                typeNames = new[] { string.Empty };
                return;
            }

            var labelList = new List<string> { "(None)" };
            var typeList = new List<string> { string.Empty };

            for (var i = 0; i < screensProp.arraySize; i++)
            {
                var el = screensProp.GetArrayElementAtIndex(i);
                var typeNameProp = el.FindPropertyRelative("typeName");
                var nameProp = el.FindPropertyRelative("name");
                var typeName = typeNameProp != null ? typeNameProp.stringValue : string.Empty;
                if (string.IsNullOrEmpty(typeName))
                {
                    continue;
                }

                var prefabName = nameProp != null ? nameProp.stringValue : string.Empty;
                var label = string.IsNullOrEmpty(prefabName) ? typeName : $"{prefabName} ({typeName})";
                labelList.Add(label);
                typeList.Add(typeName);
            }

            labels = labelList.ToArray();
            typeNames = typeList.ToArray();
        }
    }
}
