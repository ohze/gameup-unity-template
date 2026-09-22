using System;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Một dòng <c>@progress {json}</c> do <c>ui_locate.py</c> in ra trong lúc chạy.</summary>
    [Serializable]
    public class LocateProgressEvent
    {
        public const string PhaseStart = "start";
        public const string PhaseSprite = "sprite";
        public const string PhaseFilter = "filter";
        public const string PhaseDrop = "drop";
        public const string PhaseTextScan = "text-scan";
        public const string PhaseTextRead = "text-read";
        public const string PhaseText = "text";

        public string phase;
        public string message;
        public int done;
        public int total;
        public bool cached;
        public int cachedCount;
        public int workers;
        public int demoWidth;
        public int demoHeight;
        public LocateSprite sprite;
        public LocateDrop drop;
        public LocateText text;
    }
}
