using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Bản sao sâu của spec/node qua JSON — sửa bản sao không đụng bản đang hiện trên cửa sổ hay trên đĩa.</summary>
    public static class UISpecClone
    {
        public static UISpec Of(UISpec spec) => spec == null ? null : JsonUtility.FromJson<UISpec>(JsonUtility.ToJson(spec));

        public static UISpecNode Of(UISpecNode node) => node == null ? null : JsonUtility.FromJson<UISpecNode>(JsonUtility.ToJson(node));
    }
}
