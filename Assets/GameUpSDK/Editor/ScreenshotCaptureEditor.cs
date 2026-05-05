using UnityEditor;
using UnityEngine;

namespace GameUpSDK.Editor
{
    [CustomEditor(typeof(ScreenshotCapture))]
    public class ScreenshotCaptureEditor : UnityEditor.Editor
    {
        private static readonly string[] PresetLabels =
        {
            "Custom",
            "App Store iPhone 6.7\" Portrait (1290x2796)",
            "App Store iPhone 6.5\" Portrait (1242x2688)",
            "App Store iPhone 5.5\" Portrait (1242x2208)",
            "Google Play Phone Portrait (1080x1920)",
            "Google Play Tablet Landscape (1920x1080)"
        };

        private static readonly Vector2Int[] PresetSizes =
        {
            new Vector2Int(0, 0),
            new Vector2Int(1290, 2796),
            new Vector2Int(1242, 2688),
            new Vector2Int(1242, 2208),
            new Vector2Int(1080, 1920),
            new Vector2Int(1920, 1080)
        };

        private int selectedPresetIndex;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Store Screenshot Presets", EditorStyles.boldLabel);

            selectedPresetIndex = EditorGUILayout.Popup("Preset", selectedPresetIndex, PresetLabels);

            if (selectedPresetIndex > 0 && GUILayout.Button("Apply Preset Size"))
            {
                ApplyPresetSize(PresetSizes[selectedPresetIndex]);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Capture Screenshot (Editor)"))
            {
                CaptureFromInspector();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ApplyPresetSize(Vector2Int size)
        {
            SerializedProperty useCustomSize = serializedObject.FindProperty("useCustomSize");
            SerializedProperty width = serializedObject.FindProperty("width");
            SerializedProperty height = serializedObject.FindProperty("height");

            useCustomSize.boolValue = true;
            width.intValue = size.x;
            height.intValue = size.y;

            serializedObject.ApplyModifiedProperties();
        }

        private void CaptureFromInspector()
        {
            foreach (Object targetObject in targets)
            {
                ScreenshotCapture capture = targetObject as ScreenshotCapture;
                if (capture == null)
                {
                    continue;
                }

                capture.TakeScreenshot();
                EditorUtility.SetDirty(capture);
            }
        }
    }
}
