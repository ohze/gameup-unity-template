namespace GameUp.Core
{
    /// <summary>
    /// Nhóm âm thanh, quyết định lấy volume và mute từ kênh nào trong <see cref="AudioSetting"/>.
    /// <see cref="Sfx"/> và <see cref="Ui"/> dùng chung kênh Sound; <see cref="Music"/> có kênh riêng.
    /// </summary>
    public enum AudioCategory
    {
        Sfx = 0,
        Ui = 1,
        Music = 2
    }
}
