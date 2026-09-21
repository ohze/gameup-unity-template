using System;
using System.Collections.Generic;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Kết quả dò một file sprite: khớp ở đâu, hoặc lý do không khớp.</summary>
    [Serializable]
    public class LocateSprite
    {
        public const string StatusMatched = "matched";

        public string sprite;
        public string name;
        public string status;
        public string reason;
        public int spriteWidth;
        public int spriteHeight;
        public bool lowTexture;
        public List<LocateMatch> matches = new List<LocateMatch>();
        public SpriteBorderHint suggestedBorder;

        public bool IsMatched => status == StatusMatched && matches != null && matches.Count > 0;

        /// <summary>Lý do không khớp, diễn giải cho người đọc.</summary>
        public string ReasonLabel
        {
            get
            {
                switch (reason)
                {
                    case "soft-alpha": return "glow / bán trong suốt — ước lượng bằng mắt";
                    case "too-large": return "lớn hơn demo";
                    case "explained-by-other": return "trùng pixel của sprite khác";
                    case "no-match": return "không có trên demo";
                    default: return reason;
                }
            }
        }
    }
}
