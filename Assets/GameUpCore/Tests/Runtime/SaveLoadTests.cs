using System.IO;
using NUnit.Framework;
using UnityEngine;
using GameUp.Core;

namespace GameUp.Core.Tests
{
    [System.Serializable]
    class TestSaveData
    {
        public string Name;
        public int Score;
    }

    public class SaveLoadTests
    {
        const string TestKey = "unit_test_save";

        [TearDown]
        public void TearDown()
        {
            var jsonPath = Path.Combine(Application.persistentDataPath, $"{TestKey}.json");
            var binPath = Path.Combine(Application.persistentDataPath, $"{TestKey}.dat");
            if (File.Exists(jsonPath)) File.Delete(jsonPath);
            if (File.Exists(binPath)) File.Delete(binPath);
        }

        [Test]
        public void JsonSave_SaveAndLoad_RoundTrip()
        {
            var sys = new JsonSaveSystem();
            sys.Save(TestKey, new TestSaveData { Name = "Test", Score = 100 });
            Assert.IsTrue(sys.Exists(TestKey));

            var loaded = sys.Load<TestSaveData>(TestKey);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("Test", loaded.Name);
            Assert.AreEqual(100, loaded.Score);
        }

        [Test]
        public void JsonSave_Delete_RemovesFile()
        {
            var sys = new JsonSaveSystem();
            sys.Save(TestKey, new TestSaveData { Name = "Del", Score = 0 });
            Assert.IsTrue(sys.Exists(TestKey));

            sys.Delete(TestKey);
            Assert.IsFalse(sys.Exists(TestKey));
        }

        [Test]
        public void BinarySave_SaveAndLoad_RoundTrip()
        {
            var sys = new BinarySaveSystem();
            sys.Save(TestKey, new TestSaveData { Name = "Bin", Score = 200 });
            Assert.IsTrue(sys.Exists(TestKey));

            var loaded = sys.Load<TestSaveData>(TestKey);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("Bin", loaded.Name);
            Assert.AreEqual(200, loaded.Score);
        }

        [Test]
        public void Load_NonExistent_ReturnsNull()
        {
            var sys = new JsonSaveSystem();
            var result = sys.Load<TestSaveData>("non_existent_key");
            Assert.IsNull(result);
        }
    }
}
