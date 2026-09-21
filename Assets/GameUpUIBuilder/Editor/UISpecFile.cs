using System;
using System.IO;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Đọc/ghi spec JSON (đường dẫn tương đối gốc project hoặc tuyệt đối).</summary>
    public static class UISpecFile
    {
        public static UISpec Load(string path, out string error)
        {
            error = null;
            var absolute = UIBuilderPaths.ToAbsolute(path);
            if (!File.Exists(absolute))
            {
                error = $"Không thấy file spec: {path}";
                return null;
            }

            try
            {
                var spec = JsonUtility.FromJson<UISpec>(File.ReadAllText(absolute));
                if (spec == null || spec.nodes == null) error = $"Spec rỗng hoặc sai định dạng: {path}";
                return spec;
            }
            catch (ArgumentException e)
            {
                error = $"JSON lỗi ({path}): {e.Message}";
                return null;
            }
        }

        public static void Save(UISpec spec, string path)
        {
            var absolute = UIBuilderPaths.ToAbsolute(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllText(absolute, JsonUtility.ToJson(spec, true));
        }
    }
}
