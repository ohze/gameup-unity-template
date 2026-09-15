using System.Collections.Generic;
using System.IO;
using GameUp.Core.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GameUp.Core.Tests
{
    public class GUApiIndexBuilderTests
    {
        [Test]
        public void FormatType_GenericNullableArray_UsesCSharpSyntax()
        {
            Assert.AreEqual("Dictionary<string, int[]>", GUApiIndexBuilder.FormatType(typeof(Dictionary<string, int[]>)));
            Assert.AreEqual("float?", GUApiIndexBuilder.FormatType(typeof(float?)));
            Assert.AreEqual("MonoSingleton<T>", GUApiIndexBuilder.FormatType(typeof(MonoSingleton<>)));
        }

        [Test]
        public void Build_EmbeddedCore_ListsUiBaseClassesAndSourcePaths()
        {
            var markdown = BuildEmbedded("GameUp Core", "com.ohze.gameup.core", "GameUpCore");

            StringAssert.Contains("## Class nền để kế thừa", markdown);
            StringAssert.Contains("class UIPopup : UIBaseView", markdown);
            StringAssert.Contains("| `UIScreen` |", markdown, "Class chỉ override (không khai báo virtual mới) vẫn phải nằm trong bảng kế thừa.");
            StringAssert.Contains("(Assets/GameUpCore/Runtime/UI/Popups/UIPopup.cs)", markdown);
            StringAssert.DoesNotContain("GUClaudeToolkitInstaller", markdown, "Editor tooling không thuộc API cho người viết game.");
        }

        [Test]
        public void Build_EmbeddedSdk_ListsAdsManagerWithoutEditorTooling()
        {
            var markdown = BuildEmbedded("GameUp SDK", "com.ohze.gameup.sdk", "GameUpSDK");

            StringAssert.Contains("# GameUp SDK — API index", markdown);
            StringAssert.Contains("class AdsManager : MonoSingleton<AdsManager>", markdown);
            StringAssert.DoesNotContain("GameUpSetupWindow", markdown, "Editor tooling không thuộc API cho người viết game.");
        }

        /// <summary>Chỉ chạy khi package nằm embedded trong Assets (repo template); cài qua UPM thì bỏ qua.</summary>
        private static string BuildEmbedded(string displayName, string packageName, string embeddedDirName)
        {
            var diskRoot = Path.Combine(Application.dataPath, embeddedDirName);
            Assume.That(File.Exists(Path.Combine(diskRoot, "package.json")), $"Chỉ chạy khi {packageName} embedded trong Assets/{embeddedDirName}.");

            var assetRoot = $"Assets/{embeddedDirName}";
            return GUApiIndexBuilder.Build(displayName, packageName, assetRoot, diskRoot, assetRoot, "test");
        }
    }
}
