namespace GameUpCore.Runtime.LocalTracking
{
    public interface ILevelTracking
    {
        void StartLevel(int level);
        void WinLevel(int level);
        void LoseLevel(int level, string reason);
    }
}
