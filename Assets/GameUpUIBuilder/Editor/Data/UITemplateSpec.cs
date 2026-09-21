using System;
using System.Collections.Generic;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Prefab con dùng lặp lại (item trong danh sách: RankItem, RewardItem). Dựng trước prefab chính; node
    /// <c>kind: instance</c> trong spec chính trỏ tới <see cref="output"/>. Tọa độ node tương đối góc trên-trái item.
    /// </summary>
    [Serializable]
    public class UITemplateSpec
    {
        public string name;
        public string output;
        public int width;
        public int height;
        public List<UISpecNode> nodes = new List<UISpecNode>();
    }
}
