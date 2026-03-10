using UnityEditor;
using UnityEngine;
using GameUp.Core;

namespace GameUp.Core.Editor
{
    [CustomEditor(typeof(AudioData))]
    public sealed class AudioDataEditor : UnityEditor.Editor
    {
        SerializedProperty _entries;

        void OnEnable()
        {
            _entries = serializedObject.FindProperty("_entries");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Audio Entries", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (GUILayout.Button("Add Entry"))
            {
                _entries.InsertArrayElementAtIndex(_entries.arraySize);
            }

            EditorGUILayout.Space(4);

            for (int i = 0; i < _entries.arraySize; i++)
            {
                var entry = _entries.GetArrayElementAtIndex(i);
                var id = entry.FindPropertyRelative("_id");
                var clip = entry.FindPropertyRelative("_clip");
                var volume = entry.FindPropertyRelative("_volume");
                var loop = entry.FindPropertyRelative("_loop");

                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"[{i}] {id.stringValue}", EditorStyles.boldLabel);
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    _entries.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(id, new GUIContent("ID"));
                EditorGUILayout.PropertyField(clip, new GUIContent("Clip"));
                EditorGUILayout.Slider(volume, 0f, 1f, new GUIContent("Volume"));
                EditorGUILayout.PropertyField(loop, new GUIContent("Loop"));

                // Preview button
                var clipObj = clip.objectReferenceValue as AudioClip;
                if (clipObj != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Preview"))
                    {
                        var method = typeof(AudioImporter).Assembly
                            .GetType("UnityEditor.AudioUtil")
                            ?.GetMethod("PlayPreviewClip",
                                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                                null,
                                new[] { typeof(AudioClip), typeof(int), typeof(bool) },
                                null);
                        method?.Invoke(null, new object[] { clipObj, 0, false });
                    }
                    if (GUILayout.Button("Stop"))
                    {
                        var method = typeof(AudioImporter).Assembly
                            .GetType("UnityEditor.AudioUtil")
                            ?.GetMethod("StopAllPreviewClips",
                                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                        method?.Invoke(null, null);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
