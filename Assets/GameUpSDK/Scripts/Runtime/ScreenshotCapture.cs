using UnityEngine;
using System.Collections;
using System;
using System.IO;
using GameUp.Core;

namespace GameUp.SDK
{
    public class ScreenshotCapture : MonoBehaviour
    {
        [SerializeField] private string fileName = "screenshot.png";
        [SerializeField] private bool useCustomSize = true;
        [SerializeField] private int width = 1080;
        [SerializeField] private int height = 1920;
        [SerializeField] private int superSize = 1;
        [SerializeField] private string editorOutputFolder = "Screenshots";
        [SerializeField] private bool appendTimestampToFileName = true;
        [SerializeField] private bool enableKeyboardShortcut = true;
        [SerializeField] private KeyCode screenshotKey = KeyCode.F12;
        [SerializeField] private bool requireLeftControl = false;
        [SerializeField] private bool requireLeftShift = false;

        private void Update()
        {
            if (!enableKeyboardShortcut || !Application.isPlaying)
            {
                return;
            }

            if (!Input.GetKeyDown(screenshotKey))
            {
                return;
            }

            if (requireLeftControl && !Input.GetKey(KeyCode.LeftControl))
            {
                return;
            }

            if (requireLeftShift && !Input.GetKey(KeyCode.LeftShift))
            {
                return;
            }

            TakeScreenshot();
        }

        [ContextMenu("Take Screenshot")]
        public void TakeScreenshot()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                TakeScreenshotInEditor();
                return;
            }
#endif
            if (useCustomSize)
            {
                StartCoroutine(CaptureWithCustomSize());
                return;
            }

            string outputPath = BuildOutputPath();
            ScreenCapture.CaptureScreenshot(outputPath, Mathf.Max(1, superSize));
            Debug.Log($"Screenshot saved at: {outputPath}");
        }

        private IEnumerator CaptureWithCustomSize()
        {
            yield return new WaitForEndOfFrame();

            int targetWidth = Mathf.Max(1, width);
            int targetHeight = Mathf.Max(1, height);

            Texture2D screenTexture = GameUtils.TakeScreenShot();
            SaveTextureToFile(screenTexture, targetWidth, targetHeight);
        }

        private void SaveTextureToFile(Texture2D sourceTexture, int targetWidth, int targetHeight)
        {
            Texture2D resizedTexture = ResizeTexture(sourceTexture, targetWidth, targetHeight);
            string outputPath = BuildOutputPath();

            File.WriteAllBytes(outputPath, resizedTexture.EncodeToPNG());
            Debug.Log($"Screenshot saved at: {outputPath}");

            if (Application.isPlaying)
            {
                Destroy(sourceTexture);
                Destroy(resizedTexture);
            }
            else
            {
                DestroyImmediate(sourceTexture);
                DestroyImmediate(resizedTexture);
            }
        }

        private string BuildOutputPath()
        {
            string outputFileName = BuildOutputFileName();
#if UNITY_EDITOR
            DirectoryInfo projectDirectory = Directory.GetParent(Application.dataPath);
            string projectRoot = projectDirectory != null ? projectDirectory.FullName : Application.dataPath;
            string outputDirectory = Path.Combine(projectRoot, editorOutputFolder);
            Directory.CreateDirectory(outputDirectory);
            return Path.Combine(outputDirectory, outputFileName);
#else
            return Path.Combine(Application.persistentDataPath, outputFileName);
#endif
        }

        private string BuildOutputFileName()
        {
            string rawName = string.IsNullOrWhiteSpace(fileName) ? "screenshot.png" : fileName.Trim();
            string extension = Path.GetExtension(rawName);

            if (string.IsNullOrEmpty(extension))
            {
                extension = ".png";
            }

            string nameWithoutExtension = Path.GetFileNameWithoutExtension(rawName);
            if (string.IsNullOrWhiteSpace(nameWithoutExtension))
            {
                nameWithoutExtension = "screenshot";
            }

            if (!appendTimestampToFileName)
            {
                return $"{nameWithoutExtension}{extension}";
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            return $"{nameWithoutExtension}_{timestamp}{extension}";
        }

        private static Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(targetWidth, targetHeight);
            RenderTexture previous = RenderTexture.active;

            Graphics.Blit(source, renderTexture);
            RenderTexture.active = renderTexture;

            Texture2D output = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            output.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            output.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);

            return output;
        }

#if UNITY_EDITOR
        public void TakeScreenshotInEditor()
        {
            if (Application.isPlaying)
            {
                TakeScreenshot();
                return;
            }

            if (!useCustomSize)
            {
                string outputPath = BuildOutputPath();
                ScreenCapture.CaptureScreenshot(outputPath, Mathf.Max(1, superSize));
                Debug.Log($"Screenshot saved at: {outputPath}");
                return;
            }

            int targetWidth = Mathf.Max(1, width);
            int targetHeight = Mathf.Max(1, height);

            Texture2D screenTexture = GameUtils.TakeScreenShot();
            SaveTextureToFile(screenTexture, targetWidth, targetHeight);
        }
#endif
    }
}