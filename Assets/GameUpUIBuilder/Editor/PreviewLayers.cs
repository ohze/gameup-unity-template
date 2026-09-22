using System;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>Lớp khung vẽ đè lên ảnh demo trong cửa sổ UI Builder.</summary>
    [Flags]
    public enum PreviewLayers
    {
        None = 0,
        Sprites = 1,
        Texts = 2,
        Dropped = 4,
        UIRegion = 8
    }
}
