using UnityEngine;
using System.Collections;
using System;
using System.IO;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GameUp.Core
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

#if ENABLE_INPUT_SYSTEM
        private KeyCode _cachedKeyCode = KeyCode.None;
        private Key _cachedKey = Key.None;
#endif

        private void Update()
        {
            if (!enableKeyboardShortcut || !Application.isPlaying)
            {
                return;
            }

            if (!WasShortcutPressedThisFrame())
            {
                return;
            }

            TakeScreenshot();
        }

        private bool WasShortcutPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                return WasShortcutPressedOnInputSystem(Keyboard.current);
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return WasShortcutPressedOnLegacyInput();
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private bool WasShortcutPressedOnInputSystem(Keyboard keyboard)
        {
            Key key = ResolveInputSystemKey();
            if (key == Key.None || !keyboard[key].wasPressedThisFrame)
            {
                return false;
            }

            if (requireLeftControl && !keyboard.leftCtrlKey.isPressed)
            {
                return false;
            }

            if (requireLeftShift && !keyboard.leftShiftKey.isPressed)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Map <see cref="KeyCode"/> sang <see cref="Key"/> bằng tên enum (KeyCode.F12 -> Key.F12).
        /// Kết quả được cache để <c>Update</c> không parse enum mỗi frame (0 alloc sau lần đầu).
        /// </summary>
        private Key ResolveInputSystemKey()
        {
            if (_cachedKeyCode == screenshotKey)
            {
                return _cachedKey;
            }

            _cachedKeyCode = screenshotKey;
            _cachedKey = Enum.TryParse(screenshotKey.ToString(), true, out Key parsedKey) ? parsedKey : Key.None;

            if (_cachedKey == Key.None)
            {
                GULogger.Warning($"Không map được {screenshotKey} sang Key của Input System, phím tắt chụp màn hình bị bỏ qua.");
            }

            return _cachedKey;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        private bool WasShortcutPressedOnLegacyInput()
        {
            if (!Input.GetKeyDown(screenshotKey))
            {
                return false;
            }

            if (requireLeftControl && !Input.GetKey(KeyCode.LeftControl))
            {
                return false;
            }

            if (requireLeftShift && !Input.GetKey(KeyCode.LeftShift))
            {
                return false;
            }

            return true;
        }
#endif

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
            GULogger.Log($"Screenshot saved at: {outputPath}");
        }

        private IEnumerator CaptureWithCustomSize()
        {
            yield return new WaitForEndOfFrame();

            int targetWidth = Mathf.Max(1, width);
            int targetHeight = Mathf.Max(1, height);

            Texture2D screenTexture = ScreenCapture.CaptureScreenshotAsTexture();
            SaveTextureToFile(screenTexture, targetWidth, targetHeight);
        }

        private void SaveTextureToFile(Texture2D sourceTexture, int targetWidth, int targetHeight)
        {
            bool needResize = sourceTexture.width != targetWidth || sourceTexture.height != targetHeight;
            Texture2D outputTexture = needResize
                ? ResizeTexture(sourceTexture, targetWidth, targetHeight)
                : sourceTexture;
            string outputPath = BuildOutputPath();

            File.WriteAllBytes(outputPath, outputTexture.EncodeToPNG());
            GULogger.Log($"Screenshot saved at: {outputPath}");

            DestroyTexture(sourceTexture);
            if (needResize)
            {
                DestroyTexture(outputTexture);
            }
        }

        private void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
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

        /// <summary>
        /// Resize ảnh chụp mà <b>không</b> để Unity chèn thêm bước chuyển sRGB &lt;-&gt; Linear.
        /// Project chạy Linear color space, còn pixel do <c>ScreenCapture</c> trả về đã là sRGB
        /// thành phẩm; nếu blit qua RenderTexture sRGB thì ảnh bị convert dư một lần và trắng bệch.
        /// </summary>
        private static Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(
                targetWidth,
                targetHeight,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;

            source.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, renderTexture);
            RenderTexture.active = renderTexture;

            Texture2D output = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false, true);
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
                GULogger.Log($"Screenshot saved at: {outputPath}");
                return;
            }

            int targetWidth = Mathf.Max(1, width);
            int targetHeight = Mathf.Max(1, height);

            Texture2D screenTexture = ScreenCapture.CaptureScreenshotAsTexture();
            SaveTextureToFile(screenTexture, targetWidth, targetHeight);
        }
#endif
    }
}