using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameUp.Core.Tests
{
    /// <summary>
    /// PlayMode test: pool cần GameObject thật và vòng đời MonoBehaviour.
    /// </summary>
    public class GUPoolersTests
    {
        private GameObject _prefab;

        private class SpyPoolable : MonoBehaviour, IPoolable
        {
            public int SpawnCount;
            public int DespawnCount;

            public void OnSpawn() => SpawnCount++;
            public void OnDespawn() => DespawnCount++;
        }

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("PoolTestPrefab");
            _prefab.AddComponent<SpyPoolable>();
            _prefab.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_prefab) Object.DestroyImmediate(_prefab);
        }

        [UnityTest]
        public IEnumerator Spawn_ReusesDespawnedInstance()
        {
            var first = GUPool.Spawn(_prefab);
            yield return null;

            GUPool.DeSpawn(first);
            var second = GUPool.Spawn(_prefab);

            Assert.AreSame(first, second, "Object đã trả về pool phải được tái sử dụng");
            Assert.IsTrue(second.activeSelf);
        }

        [UnityTest]
        public IEnumerator Spawn_CreatesNewInstance_WhenNoneFree()
        {
            var first = GUPool.Spawn(_prefab);
            var second = GUPool.Spawn(_prefab);
            yield return null;

            Assert.AreNotSame(first, second);

            GUPool.DeSpawn(first);
            GUPool.DeSpawn(second);
        }

        [UnityTest]
        public IEnumerator Poolable_ReceivesSpawnAndDespawnCallbacks()
        {
            var clone = GUPool.Spawn(_prefab);
            var spy = clone.GetComponent<SpyPoolable>();
            yield return null;

            Assert.AreEqual(1, spy.SpawnCount);
            Assert.AreEqual(0, spy.DespawnCount);

            GUPool.DeSpawn(clone);
            Assert.AreEqual(1, spy.DespawnCount);

            GUPool.Spawn(_prefab);
            Assert.AreEqual(2, spy.SpawnCount, "Lần lấy lại từ pool cũng phải gọi OnSpawn");
        }

        [UnityTest]
        public IEnumerator DeSpawn_IsIdempotent()
        {
            var clone = GUPool.Spawn(_prefab);
            var spy = clone.GetComponent<SpyPoolable>();
            yield return null;

            GUPool.DeSpawn(clone);
            GUPool.DeSpawn(clone);

            Assert.AreEqual(1, spy.DespawnCount, "DeSpawn hai lần không được đẩy trùng vào pool");

            var a = GUPool.Spawn(_prefab);
            var b = GUPool.Spawn(_prefab);
            Assert.AreNotSame(a, b, "Pool không được phát cùng một object cho hai lần Spawn");
        }

        [UnityTest]
        public IEnumerator Prewarm_CreatesInstancesUpFront()
        {
            GUPoolers.Instance.Prewarm(_prefab, 3);
            yield return null;

            var a = GUPool.Spawn(_prefab);
            var b = GUPool.Spawn(_prefab);
            var c = GUPool.Spawn(_prefab);

            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.IsNotNull(c);
            Assert.AreNotSame(a, b);
            Assert.AreNotSame(b, c);
        }

        [UnityTest]
        public IEnumerator DeSpawnAll_ReturnsEveryClone()
        {
            var a = GUPool.Spawn(_prefab);
            var b = GUPool.Spawn(_prefab);
            yield return null;

            GUPool.DeSpawnAll(_prefab);

            Assert.IsFalse(a.activeSelf);
            Assert.IsFalse(b.activeSelf);
        }
    }
}
