namespace GameUp.Core
{
    public class AudioSetting : Singleton<AudioSetting>
    {
        public readonly BooleanVar IsMusicOn = new(Constant.MusicKey);
        public readonly BooleanVar IsSoundOn = new(Constant.SoundKey);
    }

    public static class Constant
    {
        public const string SoundKey = "audio_sk";
        public const string MusicKey = "audio_mk";
    }
}