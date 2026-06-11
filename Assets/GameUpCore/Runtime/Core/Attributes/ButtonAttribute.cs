using System;

namespace GameUp.Core
{
    /// <summary>
    /// Đánh dấu method để hiển thị nút trên Inspector (chỉ trong Editor), gọi method khi bấm.
    /// Tương tự [Button] của Odin Inspector.
    /// Method có kiểu trả về khác <c>void</c>: Inspector hiển thị kết quả sau khi bấm.
    /// Kiểu gắn <see cref="SerializableAttribute"/> được vẽ dạng field mở rộng (giống property trong Inspector).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ButtonAttribute : Attribute
    {
        /// <summary> Nhãn hiển thị trên nút. Nếu null/trống thì dùng tên method. </summary>
        public string Name { get; }

        /// <summary> Chiều cao nút (pixel). Mặc định 0 = dùng style mặc định. </summary>
        public int Height { get; }

        public ButtonAttribute()
        {
            Name = null;
            Height = 0;
        }

        public ButtonAttribute(string name)
        {
            Name = name;
            Height = 0;
        }

        public ButtonAttribute(string name, int height)
        {
            Name = name;
            Height = height;
        }
    }
}
