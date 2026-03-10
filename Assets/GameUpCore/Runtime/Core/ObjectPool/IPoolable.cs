namespace GameUp.Core
{
    /// <summary>
    /// Implement on objects managed by an ObjectPool to receive spawn/despawn callbacks.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }
}
