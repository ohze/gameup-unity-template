using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Tutorial
{
    [UnityEditor.CustomEditor(typeof(SOTutorialStep))]
    public class SOTutorialStepEditor : UnityEditor.Editor
    {
        private SerializedProperty _stepName;
        private SerializedProperty _useFocus;
        private SerializedProperty _focusType;
        private SerializedProperty _destinationType;
        private SerializedProperty _destinationType2;
        private SerializedProperty _useTalk;
        private SerializedProperty _talkText;
        private SerializedProperty _useArrow;
        private SerializedProperty _useHandDrag;
        private SerializedProperty _arrowDirection;

        private static readonly GUIContent StepNameLabel = new("Step Name");
        private static readonly GUIContent FocusTypeLabel = new("Focus Type");
        private static readonly GUIContent DestinationTypeLabel = new("Destination Type");
        private static readonly GUIContent DestinationType2Label = new("Destination Type 2");
        private static readonly GUIContent TalkTextLabel = new("Talk Text");
        private static readonly GUIContent UseHandDragLabel = new("Use Hand Drag");
        private static readonly GUIContent ArrowDirectionLabel = new("Arrow Direction");

        private void OnEnable()
        {
            _stepName = serializedObject.FindProperty("stepName");
            _useFocus = serializedObject.FindProperty("useFocus");
            _focusType = serializedObject.FindProperty("focusType");
            _destinationType = serializedObject.FindProperty("destinationPoint");
            _destinationType2 = serializedObject.FindProperty("destinationPoint2");
            _useTalk = serializedObject.FindProperty("useTalk");
            _talkText = serializedObject.FindProperty("talkText");
            _useArrow = serializedObject.FindProperty("useArrow");
            _useHandDrag = serializedObject.FindProperty("useHandDrag");
            _arrowDirection = serializedObject.FindProperty("arrowDirection");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_stepName, StepNameLabel);
            }

            EditorGUILayout.Space(6f);
            DrawFocusSection();

            EditorGUILayout.Space(6f);
            DrawTalkSection();

            EditorGUILayout.Space(6f);
            DrawArrowSection();

            EditorGUILayout.Space(6f);
            DrawSharedDestinationSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFocusSection()
        {
            EditorGUILayout.LabelField("Focus", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_useFocus);

                if (_useFocus.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_focusType, FocusTypeLabel);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawTalkSection()
        {
            EditorGUILayout.LabelField("Talk", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_useTalk);

                if (_useTalk.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_talkText, TalkTextLabel);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawArrowSection()
        {
            EditorGUILayout.LabelField("Arrow", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_useArrow);

                if (_useArrow.boolValue)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(_useHandDrag, UseHandDragLabel);

                    if (!_useHandDrag.boolValue)
                    {
                        EditorGUILayout.PropertyField(_arrowDirection, ArrowDirectionLabel);
                    }

                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawSharedDestinationSection()
        {
            if (!(_useArrow.boolValue || _useFocus.boolValue))
                return;

            EditorGUILayout.LabelField("Destination", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_destinationType, DestinationTypeLabel);

                if ((_useFocus.boolValue && _focusType.enumValueIndex == (int)FocusType.Multi) ||
                    (_useArrow.boolValue && _useHandDrag.boolValue))
                {
                    EditorGUILayout.PropertyField(_destinationType2, DestinationType2Label);
                }
            }
        }
    }
}
