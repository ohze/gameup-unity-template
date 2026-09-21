using System;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Một node UI trong spec. Tọa độ là pixel trên ảnh demo (gốc trên-trái), tuyệt đối — builder tự
    /// quy đổi sang RectTransform tương đối với node cha theo <see cref="anchor"/>.
    /// </summary>
    [Serializable]
    public class UISpecNode
    {
        public const string KindEmpty = "empty";
        public const string KindImage = "image";
        public const string KindButton = "button";
        public const string KindText = "text";

        /// <summary>Tên GameObject, duy nhất trong spec (vd <c>btnReward</c>, <c>txtName</c>).</summary>
        public string id;

        /// <summary>id của node cha; rỗng = con trực tiếp của root.</summary>
        public string parent;

        /// <summary>empty | image | button | text.</summary>
        public string kind = KindImage;

        public int x;
        public int y;
        public int w;
        public int h;

        /// <summary>
        /// auto | center | top | bottom | left | right | top-left | top-right | bottom-left | bottom-right |
        /// stretch | stretch-top | stretch-middle | stretch-bottom.
        /// </summary>
        public string anchor = "auto";

        public bool active = true;

        // ─── image / button ───
        /// <summary>Asset path của sprite (vd <c>Assets/.../btn_green.png</c>).</summary>
        public string sprite;
        public bool sliced;
        public bool preserveAspect;
        /// <summary>Màu tint dạng #RRGGBB hoặc #RRGGBBAA; rỗng = trắng.</summary>
        public string color;
        public bool raycastTarget;

        // ─── text ───
        public string text;
        public float fontSize;
        /// <summary>Asset path TMP_FontAsset; rỗng = font mặc định của TMP Settings.</summary>
        public string font;
        /// <summary>left | center | right.</summary>
        public string align = "center";
        public bool bold;
    }
}
