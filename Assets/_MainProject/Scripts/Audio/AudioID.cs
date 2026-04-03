public static class AudioID
{
    private static GameUp.Core.AudioIdentity Get(string name)
    {
        return GameUp.Core.AudioManager.TryGetIdentity(name, out var identity) ? identity : null;
    }
}