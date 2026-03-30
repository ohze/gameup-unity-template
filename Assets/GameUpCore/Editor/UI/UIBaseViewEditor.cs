using System;
using System.Collections.Generic;
using System.Linq;
using GameUp.Core.UI;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GameUp.Core.Editor.UI
{
    [CustomEditor(typeof(UIBaseView), true)]
    public class UIBaseViewEditor : UnityEditor.Editor
    {
        private SerializedProperty _animationModeProp;
        private SerializedProperty _animationTypeNameProp;

        private List<Type> _cachedTypes;
        private string[] _cachedLabels;
        private string[] _cachedAqn;

        private void OnEnable()
        {
            _animationModeProp = serializedObject.FindProperty("animationMode");
            _animationTypeNameProp = serializedObject.FindProperty("animationTypeName");
            BuildTypeCache();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, "m_Script", "animationMode", "animationTypeName");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_animationModeProp);
            var modeChanged = EditorGUI.EndChangeCheck();

            var mode = (UIAnimationMode)_animationModeProp.enumValueIndex;
            if (mode == UIAnimationMode.Custom)
            {
                var currentIndex = GetCurrentIndex(_animationTypeNameProp.stringValue);
                var nextIndex = EditorGUILayout.Popup("Animation Type", currentIndex, _cachedLabels);
                if (nextIndex != currentIndex)
                {
                    _animationTypeNameProp.stringValue = _cachedAqn[nextIndex];
                    ApplyAndEnsureAnimationComponent();
                }
                else if (modeChanged)
                {
                    ApplyCustomModeCleanupOnly();
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(_animationTypeNameProp.stringValue))
                {
                    _animationTypeNameProp.stringValue = null;
                    serializedObject.ApplyModifiedProperties();
                }

                if (modeChanged)
                {
                    ApplyDefaultModeSetup();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private int GetCurrentIndex(string aqn)
        {
            if (string.IsNullOrWhiteSpace(aqn)) return 0;
            for (int i = 0; i < _cachedAqn.Length; i++)
            {
                if (_cachedAqn[i] == aqn) return i;
            }

            return 0;
        }

        private void BuildTypeCache()
        {
            _cachedTypes = new List<Type>();
            _cachedLabels = Array.Empty<string>();
            _cachedAqn = Array.Empty<string>();

            var derived = TypeCache.GetTypesDerivedFrom<UIBaseAnimation>()
                .Where(t => t != null && !t.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .ToList();

            _cachedTypes.Add(null);
            _cachedTypes.AddRange(derived);

            var labels = new List<string> { "Default (UIDefaultAnimation)" };
            labels.AddRange(derived.Select(t => t.Name));
            _cachedLabels = labels.ToArray();

            var aqn = new List<string> { null };
            aqn.AddRange(derived.Select(t => t.AssemblyQualifiedName));
            _cachedAqn = aqn.ToArray();
        }

        private void ApplyAndEnsureAnimationComponent()
        {
            serializedObject.ApplyModifiedProperties();

            foreach (var obj in targets)
            {
                if (obj is not UIBaseView view) continue;
                var typeName = _animationTypeNameProp.stringValue;
                if (string.IsNullOrWhiteSpace(typeName)) continue;

                var type = Type.GetType(typeName);
                if (type == null) continue;

                RemoveDefaultOnlyAnimation(view);

                var existing = view.GetComponent(type);
                if (existing == null)
                {
                    Undo.AddComponent(view.gameObject, type);
                }

                var all = view.GetComponents<UIBaseAnimation>();
                foreach (var anim in all)
                {
                    if (anim == null) continue;
                    if (anim.GetType() == type) continue;
                    Undo.DestroyObjectImmediate(anim);
                }

                EditorUtility.SetDirty(view);
                EditorUtility.SetDirty(view.gameObject);
            }
        }

        private void ApplyCustomModeCleanupOnly()
        {
            serializedObject.ApplyModifiedProperties();
            foreach (var obj in targets)
            {
                if (obj is not UIBaseView view) continue;
                RemoveDefaultOnlyAnimation(view);
                EditorUtility.SetDirty(view);
                EditorUtility.SetDirty(view.gameObject);
            }
        }

        private void ApplyDefaultModeSetup()
        {
            serializedObject.ApplyModifiedProperties();
            foreach (var obj in targets)
            {
                if (obj is not UIBaseView view) continue;

                var all = view.GetComponents<UIBaseAnimation>();
                foreach (var anim in all)
                {
                    if (anim == null) continue;
                    Undo.DestroyObjectImmediate(anim);
                }

                var defaults = view.GetComponents<UIDefaultAnimation>();
                var hasPureDefault = defaults.Any(d => d != null && d is not UIBaseAnimation);
                if (!hasPureDefault)
                {
                    Undo.AddComponent<UIDefaultAnimation>(view.gameObject);
                }

                EditorUtility.SetDirty(view);
                EditorUtility.SetDirty(view.gameObject);
            }
        }

        private void RemoveDefaultOnlyAnimation(UIBaseView view)
        {
            var defaults = view.GetComponents<UIDefaultAnimation>();
            foreach (var d in defaults)
            {
                if (d == null) continue;
                if (d is UIBaseAnimation) continue;
                Undo.DestroyObjectImmediate(d);
            }
        }
    }
}

