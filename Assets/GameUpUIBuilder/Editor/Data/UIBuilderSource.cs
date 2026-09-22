namespace GameUp.UIBuilder.Editor
{
    /// <summary>Nguồn lấy tọa độ phần tử UI.</summary>
    public enum UIBuilderSource
    {
        /// <summary>Dò vị trí art đã cắt trên ảnh demo (OpenCV + OCR).</summary>
        Demo,

        /// <summary>Đọc file PSD: tọa độ layer, text layer, trạng thái tab; nối layer với art, xuất PNG phần thiếu.</summary>
        Psd
    }
}
