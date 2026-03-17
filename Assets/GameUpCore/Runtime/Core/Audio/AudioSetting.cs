namespace GameUp.Core
{
    public class AudioSetting : Singleton<AudioSetting>
    {
        public const string SoundKey = "audio_sk";
        public const string MusicKey = "audio_mk";
        public readonly BooleanVar IsMusicOn = new(MusicKey);
        public readonly BooleanVar IsSoundOn = new(SoundKey);
    }
}