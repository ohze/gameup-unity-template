using System;
using System.Collections.Generic;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>File JSON do <c>Tools~/ui_locate.py</c> sinh ra.</summary>
    [Serializable]
    public class LocateResult
    {
        public int version;
        public string demo;
        public int demoWidth;
        public int demoHeight;
        public int elapsedMs;
        public int cachedCount;
        public List<LocateSprite> sprites = new List<LocateSprite>();
        public List<LocateText> texts = new List<LocateText>();
    }
}
