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
        public string ReasonLabel => LocateReason.Label(reason);
    }
}
