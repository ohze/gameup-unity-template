using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameUp.Core.Tests
{
    public class LocalStorageUtilsTests
    {
        private const string Key = "__gu_test_storage";

        private CultureInfo _originalCulture;

        [SetUp]
        public void SetUp()
        {
            _originalCulture = Thread.CurrentThread.CurrentCulture;
            PlayerPrefs.DeleteKey(Key);
        }

        [TearDown]
        public void TearDown()
        {
            Thread.CurrentThread.CurrentCulture = _originalCulture;
            PlayerPrefs.DeleteKey(Key);
        }

        [Test]
        public void String_RoundTrips()
        {
            LocalStorageUtils.SetString(Key, "xin chào");
            Assert.AreEqual("xin chào", LocalStorageUtils.GetString(Key));
        }

        [Test]
        public void Int_RoundTrips()
        {
            LocalStorageUtils.SetInt(Key, -42);
            Assert.AreEqual(-42, LocalStorageUtils.GetInt(Key));
        }

        [Test]
        public void Long_RoundTrips()
        {
            LocalStorageUtils.SetLong(Key, 9_000_000_000L);
            Assert.AreEqual(9_000_000_000L, LocalStorageUtils.GetLong(Key));
        }

        [Test]
        public void Boolean_RoundTrips()
        {
            LocalStorageUtils.SetBoolean(Key, true);
            Assert.IsTrue(LocalStorageUtils.GetBoolean(Key));

            LocalStorageUtils.SetBoolean(Key, false);
            Assert.IsFalse(LocalStorageUtils.GetBoolean(Key));
        }

        /// <summary>
        /// Trước đây float được ghi bằng culture của máy: máy dùng dấu phẩy sẽ ghi "1,5"
        /// rồi hỏng khi đọc ở culture khác.
        /// </summary>
        [Test]
        public void Float_IsCultureIndependent()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            LocalStorageUtils.SetFloat(Key, 1.5f);

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.AreEqual(1.5f, LocalStorageUtils.GetFloat(Key), 0.0001f);
        }

        [Test]
        public void Getters_ReturnDefault_OnCorruptedData()
        {
            // Ghi thẳng dữ liệu rác, bỏ qua lớp mã hoá.
            PlayerPrefs.SetString(Key, "not-a-valid-payload");

            // Mỗi getter phải cảnh báo đúng một lần rồi lui về giá trị mặc định.
            var decryptWarning = new Regex("Decrypt failed for key");

            LogAssert.Expect(LogType.Warning, decryptWarning);
            Assert.AreEqual(7, LocalStorageUtils.GetInt(Key, 7));

            LogAssert.Expect(LogType.Warning, decryptWarning);
            Assert.AreEqual(1.5f, LocalStorageUtils.GetFloat(Key, 1.5f), 0.0001f);

            LogAssert.Expect(LogType.Warning, decryptWarning);
            Assert.AreEqual("fallback", LocalStorageUtils.GetString(Key, "fallback"));
        }

        [Test]
        public void MissingKey_ReturnsDefault()
        {
            Assert.AreEqual(123, LocalStorageUtils.GetInt(Key, 123));
            Assert.IsFalse(LocalStorageUtils.HasKey(Key));
        }
    }
}
