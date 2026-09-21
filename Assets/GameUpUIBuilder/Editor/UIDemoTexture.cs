using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Ảnh demo đọc thẳng từ file gốc: đủ độ phân giải (không bị Max Size / nén của import settings) và có mipmap để
    /// thu nhỏ trong cửa sổ vẫn nét. Cache một texture, đọc lại khi đổi file hoặc file bị ghi đè.
    /// </summary>
    public static class UIDemoTexture
    {
        private static Texture2D _texture;
        private static string _path;
        private static DateTime _writeTime;

        public static Texture2D Get(string demoPath)
        {
            var absolute = UIBuilderPaths.ToAbsolute(demoPath);
            if (string.IsNullOrEmpty(absolute) || !File.Exists(absolute))
            {
                Release();
                return null;
            }

            var writeTime = File.GetLastWriteTimeUtc(absolute);
            if (_texture != null && _path == absolute && _writeTime == writeTime) return _texture;

            Release();
            _texture = new Texture2D(2, 2, TextureFormat.RGBA32, true)
            {
                name = Path.GetFileName(absolute),
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4
            };
            if (!_texture.LoadImage(File.ReadAllBytes(absolute)))
            {
                Release();
                return null;
            }

            _path = absolute;
            _writeTime = writeTime;
            return _texture;
        }

        public static void Release()
        {
            if (_texture != null) Object.DestroyImmediate(_texture);
            _texture = null;
            _path = null;
        }
    }
}
