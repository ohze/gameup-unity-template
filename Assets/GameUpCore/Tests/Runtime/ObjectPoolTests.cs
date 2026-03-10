using NUnit.Framework;
using GameUp.Core;

namespace GameUp.Core.Tests
{
    class PoolItem : IPoolable
    {
        public bool Spawned;
        public bool Despawned;
        public void OnSpawn() => Spawned = true;
        public void OnDespawn() => Despawned = true;
    }

    public class ObjectPoolTests
    {
        [Test]
        public void Get_ReturnsNewItem()
        {
            var pool = new ObjectPool<PoolItem>();
            var item = pool.Get();
            Assert.IsNotNull(item);
            Assert.IsTrue(item.Spawned);
        }

        [Test]
        public void Release_And_Get_ReusesItem()
        {
            var pool = new ObjectPool<PoolItem>();
            var item = pool.Get();
            pool.Release(item);
            Assert.AreEqual(1, pool.CountInactive);

            var reused = pool.Get();
            Assert.AreSame(item, reused);
            Assert.AreEqual(0, pool.CountInactive);
        }

        [Test]
        public void PreWarm_CreatesItems()
        {
            var pool = new ObjectPool<PoolItem>(preWarm: 5);
            Assert.AreEqual(5, pool.CountInactive);
        }

        [Test]
        public void IPoolable_Callbacks_Called()
        {
            var pool = new ObjectPool<PoolItem>();
            var item = pool.Get();
            Assert.IsTrue(item.Spawned);

            pool.Release(item);
            Assert.IsTrue(item.Despawned);
        }

        [Test]
        public void OnGet_OnRelease_Callbacks()
        {
            int getCount = 0, releaseCount = 0;
            var pool = new ObjectPool<PoolItem>(
                onGet: _ => getCount++,
                onRelease: _ => releaseCount++
            );

            var item = pool.Get();
            Assert.AreEqual(1, getCount);

            pool.Release(item);
            Assert.AreEqual(1, releaseCount);
        }
    }
}
