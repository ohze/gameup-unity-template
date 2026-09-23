namespace GameUp.UIBuilder.Editor
{
    /// <summary>Khung đang nằm dưới con trỏ trên ảnh demo — sprite máy dò được và/hoặc node của spec.</summary>
    public struct UIDemoHover
    {
        /// <summary>Tên sprite trong kết quả định vị; null = con trỏ không nằm trên khung sprite nào.</summary>
        public string Sprite;

        /// <summary>id node trong spec; null = con trỏ không nằm trên node nào.</summary>
        public string Node;
    }
}
