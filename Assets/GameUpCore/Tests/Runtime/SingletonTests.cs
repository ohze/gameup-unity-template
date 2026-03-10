using NUnit.Framework;
using UnityEngine;
using GameUp.Core;

namespace GameUp.Core.Tests
{
    sealed class TestSingleton : Singleton<TestSingleton>
    {
        public int Value;
    }

    sealed class TestPersistentSingleton : PersistentSingleton<TestPersistentSingleton>
    {
        public int Value;
    }

    public class SingletonTests
    {
        [TearDown]
        public void TearDown()
        {
            if (TestSingleton.HasInstance)
                Object.DestroyImmediate(TestSingleton.Instance.gameObject);
            if (TestPersistentSingleton.HasInstance)
                Object.DestroyImmediate(TestPersistentSingleton.Instance.gameObject);
        }

        [Test]
        public void Singleton_AutoCreates_WhenAccessed()
        {
            Assert.IsFalse(TestSingleton.HasInstance);
            var instance = TestSingleton.Instance;
            Assert.IsNotNull(instance);
            Assert.IsTrue(TestSingleton.HasInstance);
        }

        [Test]
        public void Singleton_ReturnsSameInstance()
        {
            var a = TestSingleton.Instance;
            var b = TestSingleton.Instance;
            Assert.AreSame(a, b);
        }

        [Test]
        public void Singleton_DestroysDoubleInstance()
        {
            var first = TestSingleton.Instance;
            var go = new GameObject("Duplicate");
            var second = go.AddComponent<TestSingleton>();

            Assert.AreSame(first, TestSingleton.Instance);
        }

        [Test]
        public void PersistentSingleton_AutoCreates()
        {
            Assert.IsFalse(TestPersistentSingleton.HasInstance);
            var instance = TestPersistentSingleton.Instance;
            Assert.IsNotNull(instance);
            Assert.IsTrue(TestPersistentSingleton.HasInstance);
        }
    }
}
