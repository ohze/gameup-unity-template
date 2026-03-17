#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    internal static class ButtonAttributeDrawer
    {
        private const BindingFlags MethodFlags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        private static readonly Dictionary<Type, List<(MethodInfo Method, ButtonAttribute Attr)>> _cache = new();
        private static readonly Dictionary<string, object[]> _parameterCache = new();
        private static readonly Dictionary<string, object> _returnValueCache = new();
        private static readonly Dictionary<string, bool> _foldoutCache = new();

        private static string GetMethodKey(UnityEngine.Object target, MethodInfo method)
        {
            var type = target.GetType();
            var sig = string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name));
            return $"{target.GetInstanceID()}_{type.FullName}.{method.Name}({sig})";
        }

        internal static List<(MethodInfo Method, ButtonAttribute Attr)> GetButtonMethods(Type type)
        {
            if (_cache.TryGetValue(type, out var list))
                return list;

            list = new List<(MethodInfo, ButtonAttribute)>();

            foreach (var method in type.GetMethods(MethodFlags))
            {
                var attr = method.GetCustomAttribute<ButtonAttribute>();
                if (attr == null)
                    continue;

                list.Add((method, attr));
            }

            _cache[type] = list;
            return list;
        }

        private static object GetDefaultValue(ParameterInfo param)
        {
            if (param.ParameterType.IsValueType && Nullable.GetUnderlyingType(param.ParameterType) == null)
                return Activator.CreateInstance(param.ParameterType);
            return null;
        }

        private static object DrawParameterField(ParameterInfo param, object currentValue)
        {
            var type = param.ParameterType;
            var label = new GUIContent(ObjectNames.NicifyVariableName(param.Name));

            if (type == typeof(int))
                return EditorGUILayout.IntField(label, currentValue is int i ? i : default);
            if (type == typeof(float))
                return EditorGUILayout.FloatField(label, currentValue is float f ? f : default);
            if (type == typeof(double))
                return EditorGUILayout.DoubleField(label, currentValue is double d ? d : default);
            if (type == typeof(bool))
                return EditorGUILayout.Toggle(label, currentValue is bool b && b);
            if (type == typeof(string))
                return EditorGUILayout.TextField(label, currentValue as string ?? "");
            if (type == typeof(Vector2))
                return EditorGUILayout.Vector2Field(label, currentValue is Vector2 v2 ? v2 : default);
            if (type == typeof(Vector3))
                return EditorGUILayout.Vector3Field(label, currentValue is Vector3 v3 ? v3 : default);
            if (type == typeof(Vector4))
                return EditorGUILayout.Vector4Field(label, currentValue is Vector4 v4 ? v4 : default);
            if (type == typeof(Color))
                return EditorGUILayout.ColorField(label, currentValue is Color c ? c : Color.white);
            if (type == typeof(Rect))
                return EditorGUILayout.RectField(label, currentValue is Rect r ? r : default);
            if (type == typeof(Bounds))
                return EditorGUILayout.BoundsField(label, currentValue is Bounds b ? b : default);
            if (type == typeof(LayerMask))
            {
                var mask = currentValue is LayerMask lm ? lm.value : 0;
                var layer = EditorGUILayout.LayerField(label, mask);
                return new LayerMask { value = 1 << layer };
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var obj = currentValue as UnityEngine.Object;
                return EditorGUILayout.ObjectField(label, obj, type, true);
            }
            if (type.IsEnum)
                return EditorGUILayout.EnumPopup(label, currentValue as Enum ?? (Enum)Enum.ToObject(type, 0));

            EditorGUILayout.LabelField(label, new GUIContent($"(type: {type.Name})"));
            return currentValue ?? GetDefaultValue(param);
        }

        private static void DrawResultField(MethodInfo method, object value)
        {
            var type = method.ReturnType;
            var label = new GUIContent("Result");

            EditorGUI.BeginDisabledGroup(true);

            if (type == typeof(void))
            {
                EditorGUI.EndDisabledGroup();
                return;
            }

            if (value == null && type.IsValueType && Nullable.GetUnderlyingType(type) == null)
            {
                EditorGUILayout.TextField(label, "(chưa gọi)");
            }
            else if (type == typeof(int))
                EditorGUILayout.IntField(label, value is int i ? i : default);
            else if (type == typeof(float))
                EditorGUILayout.FloatField(label, value is float f ? f : default);
            else if (type == typeof(double))
                EditorGUILayout.DoubleField(label, value is double d ? d : default);
            else if (type == typeof(bool))
                EditorGUILayout.Toggle(label, value is bool b && b);
            else if (type == typeof(string))
                EditorGUILayout.TextField(label, value as string ?? "");
            else if (type == typeof(Vector2))
                EditorGUILayout.Vector2Field(label, value is Vector2 v2 ? v2 : default);
            else if (type == typeof(Vector3))
                EditorGUILayout.Vector3Field(label, value is Vector3 v3 ? v3 : default);
            else if (type == typeof(Vector4))
                EditorGUILayout.Vector4Field(label, value is Vector4 v4 ? v4 : default);
            else if (type == typeof(Color))
                EditorGUILayout.ColorField(label, value is Color c ? c : Color.white);
            else if (type == typeof(Rect))
                EditorGUILayout.RectField(label, value is Rect r ? r : default);
            else if (type == typeof(Bounds))
                EditorGUILayout.BoundsField(label, value is Bounds b ? b : default);
            else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                EditorGUILayout.ObjectField(label, value as UnityEngine.Object, type, true);
            else if (type.IsEnum)
                EditorGUILayout.EnumPopup(label, value as Enum ?? (Enum)Enum.ToObject(type, 0));
            else
                EditorGUILayout.TextField(label, value?.ToString() ?? "null");

            EditorGUI.EndDisabledGroup();
        }

        private static object[] GetOrCreateParameterValues(UnityEngine.Object target, MethodInfo method)
        {
            var key = GetMethodKey(target, method);
            var parameters = method.GetParameters();
            if (parameters.Length == 0)
                return Array.Empty<object>();

            if (!_parameterCache.TryGetValue(key, out var values) || values.Length != parameters.Length)
            {
                values = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                    values[i] = GetDefaultValue(parameters[i]);
                _parameterCache[key] = values;
            }
            return values;
        }

        private static void DrawButtonAndResult(UnityEngine.Object target, MethodInfo method, string key,
            string label, float height, object[] values)
        {
            if (GUILayout.Button(label, GUILayout.Height(height)))
            {
                try
                {
                    Undo.RecordObject(target, $"Button {label}");
                    var result = method.Invoke(method.IsStatic ? null : target, values);
                    if (method.ReturnType != typeof(void))
                        _returnValueCache[key] = result;
                    if (target)
                        EditorUtility.SetDirty(target);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            if (method.ReturnType != typeof(void))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                _returnValueCache.TryGetValue(key, out var lastResult);
                DrawResultField(method, lastResult);
            }
        }

        internal static void DrawButtons(UnityEngine.Object target)
        {
            var targetType = target.GetType();
            var buttonMethods = GetButtonMethods(targetType);
            if (buttonMethods.Count == 0)
                return;

            EditorGUILayout.Space(4);

            foreach (var (method, attr) in buttonMethods)
            {
                string label = string.IsNullOrEmpty(attr.Name) ? ObjectNames.NicifyVariableName(method.Name) : attr.Name;
                float height = attr.Height > 0 ? attr.Height : 22f;
                var parameters = method.GetParameters();
                var key = GetMethodKey(target, method);
                var values = GetOrCreateParameterValues(target, method);

                if (parameters.Length > 0)
                {
                    if (!_foldoutCache.TryGetValue(key, out var expanded))
                        expanded = true;
                    expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, label);
                    _foldoutCache[key] = expanded;

                    if (expanded)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                        for (var i = 0; i < parameters.Length; i++)
                        {
                            values[i] = DrawParameterField(parameters[i], values[i]);
                        }
                        _parameterCache[key] = values;

                        DrawButtonAndResult(target, method, key, label, height, values);

                        EditorGUILayout.EndVertical();
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.EndFoldoutHeaderGroup();
                }
                else
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    DrawButtonAndResult(target, method, key, label, height, values);
                    EditorGUILayout.EndVertical();
                }
            }
        }
    }

    /// <summary>
    /// Vẽ nút trong Inspector cho các method [Button] trên MonoBehaviour (giống Odin Inspector).
    /// </summary>
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class ButtonAttributeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            ButtonAttributeDrawer.DrawButtons(target);
        }
    }

    /// <summary>
    /// Vẽ nút trong Inspector cho các method [Button] trên ScriptableObject (giống Odin Inspector).
    /// </summary>
    [CustomEditor(typeof(ScriptableObject), true)]
    public class ButtonAttributeScriptableObjectEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            ButtonAttributeDrawer.DrawButtons(target);
        }
    }
}
#endif
