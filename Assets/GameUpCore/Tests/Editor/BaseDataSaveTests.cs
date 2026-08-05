using System;
using NUnit.Framework;
using UnityEngine;

namespace GameUp.Core.Tests
{
    public class BaseDataSaveTests
    {
        private const string StorageKey = "__gu_test_save";

        /// <summary>Schema "cũ": chỉ có coin, version 1.</summary>
        private class SaveV1 : BaseDataSave<SaveV1>
        {
            public int coin;

            protected override string Key => StorageKey;
            protected override int Version => 1;
            protected override void InitDefault() => coin = 100;
            protected override void InitHasKey() { }
        }

        /// <summary>Schema "mới": thêm gem, version 2, migrate từ v1 và từ save chưa có version.</summary>
        private class SaveV2 : BaseDataSave<SaveV2>
        {
            // Sổ ghi chép của test phải là static: mọi field public trên lớp save đều bị serialize
            // xuống storage, nên nếu để field thường thì lần Create() sau sẽ đọc lại giá trị cũ.
            public static int LastMigratedFrom = -1;
            public static int MigrateCallCount;

            public int coin;
            public int gem;

            protected override string Key => StorageKey;
            protected override int Version => 2;
            protected override void InitDefault() => coin = 100;
            protected override void InitHasKey() { }

            protected override void Migrate(int fromVersion)
            {
                LastMigratedFrom = fromVersion;
                MigrateCallCount++;
                if (fromVersion < 2) gem = 10;
            }
        }

        /// <summary>Dữ liệu ghi bởi bản build trước khi có versioning — không hề có field dataVersion.</summary>
        [Serializable]
        private class LegacySave
        {
            public int coin;
        }

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(StorageKey);
            SaveV2.LastMigratedFrom = -1;
            SaveV2.MigrateCallCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(StorageKey);
        }

        [Test]
        public void Create_WritesDefaultsAndStampsVersion()
        {
            var data = SaveV1.Create();

            Assert.AreEqual(100, data.coin);
            Assert.AreEqual(1, data.dataVersion);
        }

        [Test]
        public void Create_ReadsBackExistingData_WithoutMigrating()
        {
            var first = SaveV1.Create();
            first.coin = 555;
            first.Save();

            var second = SaveV1.Create();

            Assert.AreEqual(555, second.coin);
            Assert.AreEqual(1, second.dataVersion);
        }

        [Test]
        public void Create_MigratesOlderVersion_AndKeepsExistingValues()
        {
            var old = SaveV1.Create();
            old.coin = 999;
            old.Save();

            var upgraded = SaveV2.Create();

            Assert.AreEqual(1, SaveV2.LastMigratedFrom, "Migrate phải nhận đúng version cũ");
            Assert.AreEqual(999, upgraded.coin, "Dữ liệu cũ không được mất khi migrate");
            Assert.AreEqual(10, upgraded.gem, "Field mới phải được migrate gán giá trị");
            Assert.AreEqual(2, upgraded.dataVersion, "Version phải được cập nhật sau migrate");
        }

        [Test]
        public void Create_TreatsUnversionedSaveAsVersionZero()
        {
            LocalStorageUtils.SetObject(StorageKey, new LegacySave { coin = 77 });

            var upgraded = SaveV2.Create();

            Assert.AreEqual(0, SaveV2.LastMigratedFrom, "Save chưa có version phải được coi là version 0");
            Assert.AreEqual(2, upgraded.dataVersion);
        }

        [Test]
        public void Migration_RunsOnlyOnce()
        {
            var old = SaveV1.Create();
            old.Save();

            SaveV2.Create();
            SaveV2.Create();

            Assert.AreEqual(1, SaveV2.MigrateCallCount, "Save đã ở version mới thì không migrate lại");
        }
    }
}
