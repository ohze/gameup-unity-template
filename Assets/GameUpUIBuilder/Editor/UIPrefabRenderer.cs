using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Render prefab UI ra PNG ở đúng độ phân giải tham chiếu, trong một PreviewScene riêng — không đụng scene đang mở,
    /// không cần Play Mode. Kèm ảnh so sánh 3 khung (demo | prefab | chồng 50%) để người hoặc AI đối chiếu nhanh.
    /// </summary>
    public static class UIPrefabRenderer
    {
        /// <summary>
        /// Render prefab theo spec rồi ghi <c>render.png</c> và <c>compare.png</c> (demo | prefab | chồng 50%, nửa độ phân giải)
        /// vào thư mục job. Trả về đường dẫn compare.png, null nếu lỗi.
        /// </summary>
        public static string RenderCompare(UISpec spec, string jobFolder)
        {
            var render = RenderToTexture(spec.output, spec.referenceWidth, spec.referenceHeight);
            if (render == null) return null;

            WritePng(render, Path.Combine(jobFolder, "render.png"));
            var demo = LoadDemo(spec.demo);
            string comparePath = null;
            if (demo != null)
            {
                comparePath = Path.Combine(jobFolder, "compare.png").Replace('\\', '/');
                var compare = BuildCompare(demo, render);
                WritePng(compare, comparePath);
                Object.DestroyImmediate(compare);
                Object.DestroyImmediate(demo);
            }

            Object.DestroyImmediate(render);
            return comparePath;
        }

        private static Texture2D RenderToTexture(string prefabPath, int width, int height)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return null;

            var scene = EditorSceneManager.NewPreviewScene();
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            try
            {
                var cameraGo = new GameObject("UIBuilderCamera");
                SceneManager.MoveGameObjectToScene(cameraGo, scene);
                var camera = cameraGo.AddComponent<Camera>();
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.targetTexture = target;

                var canvasGo = new GameObject("UIBuilderCanvas", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(canvasGo, scene);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(width, height);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(canvasGo.transform, false);

                // PreviewScene ở Edit Mode không chạy vòng PreRender của canvas → TMP có mesh nhưng chưa gán material
                // cho CanvasRenderer, chữ không hiện. Tự rebuild từng text trước khi render.
                Canvas.ForceUpdateCanvases();
                foreach (var text in instance.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    text.SetAllDirty();
                    text.Rebuild(CanvasUpdate.PreRender);
                }

                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                return texture;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Texture2D LoadDemo(string demoPath)
        {
            var absolute = UIBuilderPaths.ToAbsolute(demoPath);
            if (!File.Exists(absolute)) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(absolute));
            return texture;
        }

        /// <summary>3 khung cạnh nhau ở nửa độ phân giải: demo | prefab | chồng 50%.</summary>
        private static Texture2D BuildCompare(Texture2D demo, Texture2D render)
        {
            int w = render.width / 2, h = render.height / 2;
            var output = new Texture2D(w * 3, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * 3 * h];
            var demoPixels = demo.GetPixels32();
            var renderPixels = render.GetPixels32();

            for (var y = 0; y < h; y++)
            {
                var dy = Mathf.Min(demo.height - 1, y * demo.height / h);
                var ry = y * 2;
                for (var x = 0; x < w; x++)
                {
                    var d = demoPixels[dy * demo.width + Mathf.Min(demo.width - 1, x * demo.width / w)];
                    var r = renderPixels[ry * render.width + x * 2];
                    var row = y * w * 3;
                    pixels[row + x] = d;
                    pixels[row + w + x] = r;
                    pixels[row + w * 2 + x] = Color32.Lerp(d, r, 0.5f);
                }
            }

            output.SetPixels32(pixels);
            output.Apply();
            return output;
        }

        private static void WritePng(Texture2D texture, string path)
        {
            var absolute = UIBuilderPaths.ToAbsolute(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllBytes(absolute, texture.EncodeToPNG());
        }
    }
}
