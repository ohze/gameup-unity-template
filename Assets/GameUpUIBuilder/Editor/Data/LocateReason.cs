namespace GameUp.UIBuilder.Editor
{
    /// <summary>Diễn giải mã lý do (không khớp / bị lọc) do <c>ui_locate.py</c> ghi ra.</summary>
    public static class LocateReason
    {
        public static string Label(string reason)
        {
            switch (reason)
            {
                case "soft-alpha": return "glow / bán trong suốt — ước lượng bằng mắt";
                case "too-large": return "lớn hơn demo";
                case "explained-by-other": return "trùng pixel của sprite khác";
                case "no-match": return "không có trên demo";
                case "behind-dim": return "nằm sau lớp dim (gameplay bị làm tối)";
                case "same-spot": return "sprite khác khớp cùng chỗ tốt hơn";
                case "flat-nested": return "panel một màu nằm trong panel cùng màu";
                case "ambiguous": return "một màu, trượt trên vùng cùng màu — không chốt được vị trí";
                case "inside-text": return "hình ký tự khớp vào chữ cái trong dòng chữ";
                case "foreign-weak": return "khớp yếu (một màu / tint / 9-slice / scale) từ thư mục art của màn khác";
                case "psd-backdrop": return "nằm dưới lớp dim (gameplay phía sau popup)";
                case "psd-dim": return "lớp dim đen phủ màn → imgDim";
                case "psd-empty": return "layer không có pixel";
                case "psd-no-art": return "không có art (bật xuất PNG để lấy từ PSD)";
                default: return reason;
            }
        }
    }
}
