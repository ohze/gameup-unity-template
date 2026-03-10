namespace GameUp.Core
{
    /// <summary>
    /// Strategy interface for save/load implementations.
    /// </summary>
    public interface ISaveSystem
    {
        void Save<T>(string key, T data) where T : class;
        T Load<T>(string key) where T : class;
        bool Exists(string key);
        void Delete(string key);
    }
}
