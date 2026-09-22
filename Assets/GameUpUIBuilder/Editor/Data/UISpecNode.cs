using System;
using System.Collections.Generic;

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
        public const string KindScroll = "scroll";
        public const string KindInstance = "instance";

        /// <summary>Tên GameObject, duy nhất trong spec (vd <c>btnReward</c>, <c>txtName</c>).</summary>
        public string id;

        /// <summary>id của node cha; rỗng = con trực tiếp của root.</summary>
        public string parent;

        /// <summary>empty | image | button | text | scroll | instance.</summary>
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
        /// <summary>Cỡ chữ; 0 = builder tự tính để chữ cao đúng bằng khung <see cref="h"/> (và không tràn <see cref="w"/>).</summary>
        public float fontSize;
        /// <summary>Asset path TMP_FontAsset; rỗng = font mặc định của TMP Settings.</summary>
        public string font;
        /// <summary>left | center | right.</summary>
        public string align = "center";
        public bool bold;
        /// <summary>
        /// Asset path material preset TMP (vd "FONNTS Material-outline"). Rỗng + <see cref="outlineWidth"/> &gt; 0 → builder tự
        /// chọn preset có viền dày gần nhất trong thư mục font.
        /// </summary>
        public string material;
        /// <summary>Viền chữ đo trên demo (px) — dùng để chọn material outline; 0 = không viền.</summary>
        public float outlineWidth;
        public string outlineColor;

        // ─── scroll ───
        /// <summary>vertical | horizontal. Con của node scroll nằm trong Content, xếp bằng LayoutGroup.</summary>
        public string direction = "vertical";
        /// <summary>Khoảng cách giữa các item (px) — đo từ demo.</summary>
        public float spacing;
        /// <summary>Lề trong của Content (px, cả 4 phía).</summary>
        public int padding;

        // ─── instance ───
        /// <summary>Asset path prefab (thường là output của một <see cref="UITemplateSpec"/>).</summary>
        public string prefab;
        public List<UISpecOverride> overrides = new List<UISpecOverride>();

        /// <summary>Sprite một màu (panel/nền) — chỉ dùng lúc sinh spec để xếp thứ tự vẽ, không ghi ra JSON.</summary>
        [NonSerialized] public bool flat;

        /// <summary>Căn lề lấy từ nguồn (text layer PSD) — lúc sinh spec không đoán lại theo vị trí.</summary>
        [NonSerialized] public bool alignFixed;

        /// <summary>PSD: đường dẫn nhóm layer — lúc sinh spec dùng đặt tên box theo vùng.</summary>
        [NonSerialized] public string group;
    }
}
