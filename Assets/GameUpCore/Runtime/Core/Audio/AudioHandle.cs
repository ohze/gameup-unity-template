namespace GameUp.Core
{
    /// <summary>
    /// Tham chiếu tới đúng MỘT lần phát âm thanh, để dừng riêng nó mà không đụng tới các lần phát khác
    /// của cùng identity. Cần cho SFX loop (tiếng động cơ, tiếng môi trường...).
    ///
    /// <code>
    /// var engine = AudioManager.PlayAudio(engineLoopIdentity);
    /// ...
    /// engine.Stop(fadeDuration: 0.3f);
    /// </code>
    ///
    /// Handle là struct và không giữ tham chiếu tới AudioSource: sau khi âm thanh kết thúc,
    /// handle đơn giản là hết hiệu lực và mọi thao tác trên nó trở thành no-op.
    /// </summary>
    public readonly struct AudioHandle
    {
        private readonly int _id;

        internal AudioHandle(int id)
        {
            _id = id;
        }

        /// <summary>Handle rỗng — trả về khi không phát được (tắt tiếng, hết source...).</summary>
        public static AudioHandle None => default;

        public bool IsValid => _id > 0;

        /// <summary>Lần phát này còn đang chạy (hoặc clip còn đang load) hay không.</summary>
        public bool IsPlaying => AudioManager.IsHandlePlaying(_id);

        /// <summary>Dừng lần phát này. Gọi ngay sau Play cũng được: clip đang load sẽ bị hủy phát.</summary>
        public void Stop(float fadeDuration = 0f)
        {
            AudioManager.StopHandle(_id, fadeDuration);
        }
    }
}
