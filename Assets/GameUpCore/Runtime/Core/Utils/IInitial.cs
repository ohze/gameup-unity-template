namespace GameUp.Core
{
    public interface IInitial
    {
        bool Initialized { get; set; }
        void Initialize();
    }
}