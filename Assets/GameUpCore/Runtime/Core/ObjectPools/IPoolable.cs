namespace GameUp.Core
{
    /// <summary>
    /// Cho object tự reset trạng thái khi được pool lấy ra / trả về.
    /// Đặt trên chính clone hoặc bất kỳ component con nào — <see cref="GUPoolers"/> gọi tất cả.
    /// Vì object tái sử dụng không chạy lại Awake/OnEnable theo vòng đời mới, mọi state
    /// (HP, tween đang chạy, coroutine, sự kiện đã đăng ký) nên được dọn ở đây.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>Gọi ngay sau khi object được lấy khỏi pool và bật lên.</summary>
        void OnSpawn();

        /// <summary>Gọi trước khi object bị tắt và trả về pool.</summary>
        void OnDespawn();
    }
}
