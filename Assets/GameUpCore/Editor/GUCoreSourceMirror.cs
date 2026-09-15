#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Cho AI (Claude Code) đọc được các package GameUp — Core, SDK, IAP — kể cả khi cài qua Git UPM.
    /// <para>
    /// Git UPM giải nén package vào <c>Library/PackageCache/&lt;tên&gt;@hash</c> — thư mục bị chặn đọc trong
    /// <c>.claude/settings.json</c> (Library chứa hàng GB file sinh ra, mở chặn là ngập context), và tên thư mục còn đổi
    /// theo hash mỗi lần cập nhật. Vì vậy thay vì mở chặn, source text của từng package được chép sang
    /// <c>.claude/gameup-&lt;core|sdk|iap&gt;/src</c> và sinh kèm <c>API_INDEX.md</c> cạnh đó.
    /// </para>
    /// Package embedded (<c>Assets/GameUpCore</c>, thư mục thật trong <c>Packages/</c>) vốn đọc được tại chỗ nên chỉ sinh index.
    /// SDK/IAP đều reference <c>GameUp.Core.Runtime</c>, nên Core luôn có mặt để chạy tool này.
    /// </summary>
    [InitializeOnLoad]
    public static class GUCoreSourceMirror
    {
        public const string MenuPath = "GameUp/Project/Sync GameUp source for AI";

        private const string SourceDirName = "src";
        private const string IndexFileName = "API_INDEX.md";
        private const string StampFileName = "sync-stamp.txt";
        private const string LibraryDirName = "Library";
        private const string LogTag = "ClaudeToolkit";

        /// <summary>Chỉ chép file text giúp hiểu API — bỏ .meta, prefab, texture, thư viện native.</summary>
        private static readonly string[] MirroredExtensions = { ".cs", ".md", ".asmdef", ".json" };

        /// <summary>
        /// <c>Documentation~</c> chỉ chứa template .claude/.cursor (chép sang là nhân đôi chính bộ toolkit);
        /// <c>Plugins</c> là thư viện native Android/iOS, không phải API C#.
        /// </summary>
        private static readonly string[] SkippedTopLevelDirs = { "Documentation~", "Plugins" };

        private static readonly MirroredPackage[] Packages =
        {
            new MirroredPackage("com.ohze.gameup.core", "GameUp Core", "Core", "GameUpCore", ".claude/gameup-core"),
            new MirroredPackage(GUDotweenDependencyUtility.GameUpSdkPackageName, "GameUp SDK", "SDK", "GameUpSDK", ".claude/gameup-sdk"),
            new MirroredPackage(GUDotweenDependencyUtility.GameUpIapPackageName, "GameUp IAP", "IAP", "GameUpIAP", ".claude/gameup-iap")
        };

        /// <summary>Một package GameUp và nơi đặt bản chép của nó.</summary>
        private readonly struct MirroredPackage
        {
            public readonly string PackageName;
            public readonly string DisplayName;
            public readonly string ShortName;
            public readonly string EmbeddedDirName;
            public readonly string MirrorRelativeDir;

            public MirroredPackage(string packageName, string displayName, string shortName, string embeddedDirName, string mirrorRelativeDir)
            {
                PackageName = packageName;
                DisplayName = displayName;
                ShortName = shortName;
                EmbeddedDirName = embeddedDirName;
                MirrorRelativeDir = mirrorRelativeDir;
            }

            public string MirrorDir => Path.Combine(GUClaudeToolkitInstaller.ProjectRoot, MirrorRelativeDir);

            public string SourceMirrorDir => Path.Combine(MirrorDir, SourceDirName);

            public string IndexFilePath => Path.Combine(MirrorDir, IndexFileName);

            public string StampFilePath => Path.Combine(MirrorDir, StampFileName);
        }

        /// <summary>Package đang nằm ở đâu và AI nên đọc nó từ đâu.</summary>
        private readonly struct PackageLocation
        {
            public readonly string DiskRoot;
            public readonly string AssetRoot;
            public readonly string Version;
            public readonly bool ReadableInPlace;

            public PackageLocation(string diskRoot, string assetRoot, string version, bool readableInPlace)
            {
                DiskRoot = diskRoot;
                AssetRoot = assetRoot;
                Version = version;
                ReadableInPlace = readableInPlace;
            }

            /// <summary>
            /// Đổi version hoặc commit Git (hash nằm trong tên thư mục <c>&lt;tên&gt;@hash</c>) là phải chép lại.
            /// Chỉ dùng tên thư mục, không dùng đường dẫn tuyệt đối — file này được commit, máy khác phải ra cùng giá trị.
            /// </summary>
            public string Stamp => $"{Version}\n{Path.GetFileName(DiskRoot.TrimEnd('/', '\\'))}\n{ReadableInPlace}";

            /// <summary>Thư mục AI đọc source, tương đối so với gốc project.</summary>
            public string GetReadRoot(MirroredPackage package)
            {
                return ReadableInPlace ? ToProjectRelative(DiskRoot) : $"{package.MirrorRelativeDir}/{SourceDirName}";
            }
        }

        [Serializable]
        private class PackageManifest
        {
            public string name;
            public string version;
        }

        static GUCoreSourceMirror()
        {
            if (Application.isBatchMode)
                return;

            EditorApplication.delayCall += SyncOnEditorLoad;
        }

        // ─── Menu ────────────────────────────────────────────────────────────

        [MenuItem(MenuPath)]
        private static void SyncFromMenu()
        {
            Sync(force: true, log: true);
        }

        // ─── API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Với mỗi package GameUp đang cài: chép source (nếu nằm ngoài vùng AI đọc được) và sinh lại <c>API_INDEX.md</c>.
        /// Package đã gỡ thì xoá bản chép để AI không gọi API không còn tồn tại.
        /// <paramref name="force"/> = false chỉ chép lại khi version/commit đổi; index luôn được dựng lại
        /// nhưng chỉ ghi khi nội dung khác để không làm bẩn git diff.
        /// </summary>
        public static bool Sync(bool force, bool log)
        {
            var synced = false;
            foreach (var package in Packages)
            {
                if (TryGetLocation(package, out var location))
                {
                    SyncPackage(package, location, force, log);
                    synced = true;
                }
                else
                {
                    RemoveMirrorOfUninstalledPackage(package, log);
                }
            }

            if (!synced && log)
                GULogger.Error(LogTag, "Không tìm thấy package GameUp nào (Core/SDK/IAP) để sync cho AI.");

            return synced;
        }

        /// <summary>Mọi package GameUp đang cài đều đã có index khớp đúng version/commit.</summary>
        public static bool IsSynced()
        {
            foreach (var package in Packages)
            {
                if (TryGetLocation(package, out var location) && !IsPackageSynced(package, location))
                    return false;
            }

            return true;
        }

        /// <summary>Tóm tắt từng package cho dòng trạng thái trong Settings, vd <c>Core ok · SDK chưa sync</c>.</summary>
        public static string DescribeStatus()
        {
            var parts = new List<string>();
            foreach (var package in Packages)
            {
                if (!TryGetLocation(package, out var location))
                    continue;

                parts.Add(IsPackageSynced(package, location) ? $"{package.ShortName} ok" : $"{package.ShortName} chưa sync");
            }

            return parts.Count == 0 ? "không thấy package GameUp" : string.Join(" · ", parts);
        }

        // ─── Nội bộ ──────────────────────────────────────────────────────────

        /// <summary>
        /// Chỉ tự chạy khi project đã dùng bộ công cụ Claude của GameUp — không tự sinh <c>.claude/</c>
        /// cho dev chưa chọn (xem <see cref="GUCoreUserPrefs.AiToolkitChoiceMade"/>). Cài/gỡ/cập nhật SDK hay IAP
        /// đều gây domain reload nên đi qua đây.
        /// </summary>
        private static void SyncOnEditorLoad()
        {
            if (!GUCoreUserPrefs.UseClaudeToolkit || GUClaudeToolkitInstaller.GetStatus().Installed == 0)
                return;

            try
            {
                var wasSynced = IsSynced();
                Sync(force: false, log: !wasSynced);
            }
            catch (Exception e)
            {
                GULogger.Warning(LogTag, $"Không sync được source GameUp cho AI: {e.Message}");
            }
        }

        private static void SyncPackage(MirroredPackage package, PackageLocation location, bool force, bool log)
        {
            Directory.CreateDirectory(package.MirrorDir);

            var copied = 0;
            if (force || ReadStamp(package) != location.Stamp)
            {
                if (Directory.Exists(package.SourceMirrorDir))
                    Directory.Delete(package.SourceMirrorDir, recursive: true);

                if (!location.ReadableInPlace)
                    copied = CopySourceTree(location.DiskRoot, package.SourceMirrorDir);

                File.WriteAllText(package.StampFilePath, location.Stamp);
            }

            var readRoot = location.GetReadRoot(package);
            var index = GUApiIndexBuilder.Build(
                package.DisplayName, package.PackageName, location.AssetRoot, location.DiskRoot, readRoot, location.Version);
            var indexWritten = WriteIfChanged(package.IndexFilePath, index);

            if (!log)
                return;

            var sourceNote = location.ReadableInPlace
                ? $"đọc tại chỗ ở {readRoot}"
                : $"đã chép {copied} file source sang {readRoot}";
            GULogger.Log(
                LogTag,
                $"Sync {package.DisplayName} {location.Version} cho AI: {sourceNote}; {package.MirrorRelativeDir}/{IndexFileName} {(indexWritten ? "đã cập nhật" : "không đổi")}.");
        }

        /// <summary>
        /// Xoá bản chép khi package thật sự đã gỡ. Kiểm thêm bằng AssetDatabase để một lần tra PackageInfo
        /// trả rỗng lúc Editor chưa sẵn sàng không xoá nhầm (và làm bẩn git diff).
        /// </summary>
        private static void RemoveMirrorOfUninstalledPackage(MirroredPackage package, bool log)
        {
            if (!Directory.Exists(package.MirrorDir))
                return;

            if (GUDotweenDependencyUtility.IsPackageInstalled(package.PackageName, $"Assets/{package.EmbeddedDirName}"))
                return;

            Directory.Delete(package.MirrorDir, recursive: true);
            if (log)
                GULogger.Log(LogTag, $"{package.DisplayName} đã gỡ — xoá {package.MirrorRelativeDir}.");
        }

        private static bool IsPackageSynced(MirroredPackage package, PackageLocation location)
        {
            return File.Exists(package.IndexFilePath) && ReadStamp(package) == location.Stamp;
        }

        private static bool TryGetLocation(MirroredPackage package, out PackageLocation location)
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{package.PackageName}");
            if (info != null && !string.IsNullOrEmpty(info.resolvedPath))
            {
                location = new PackageLocation(info.resolvedPath, info.assetPath, info.version, IsInsideProject(info.resolvedPath));
                return true;
            }

            var embedded = Path.Combine(Application.dataPath, package.EmbeddedDirName);
            var manifestPath = Path.Combine(embedded, "package.json");
            if (File.Exists(manifestPath))
            {
                var manifest = JsonUtility.FromJson<PackageManifest>(File.ReadAllText(manifestPath));
                if (manifest != null && manifest.name == package.PackageName)
                {
                    location = new PackageLocation(embedded, $"Assets/{package.EmbeddedDirName}", manifest.version, readableInPlace: true);
                    return true;
                }
            }

            location = default;
            return false;
        }

        /// <summary>Nằm trong project và ngoài <c>Library/</c> thì AI đọc thẳng được, không cần chép.</summary>
        private static bool IsInsideProject(string path)
        {
            var root = NormalizeDir(GUClaudeToolkitInstaller.ProjectRoot);
            var full = NormalizeDir(path);
            return full.StartsWith(root, StringComparison.Ordinal)
                   && !full.StartsWith(root + LibraryDirName + "/", StringComparison.Ordinal);
        }

        private static string ToProjectRelative(string path)
        {
            var root = NormalizeDir(GUClaudeToolkitInstaller.ProjectRoot);
            return NormalizeDir(path).Substring(root.Length).TrimEnd('/');
        }

        private static string NormalizeDir(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/') + "/";
        }

        private static int CopySourceTree(string sourceRoot, string destinationRoot)
        {
            var copied = 0;
            foreach (var file in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var relative = file.Substring(sourceRoot.Length).TrimStart('/', '\\');
                if (!ShouldMirror(relative))
                    continue;

                var destination = Path.Combine(destinationRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, overwrite: true);
                copied++;
            }

            return copied;
        }

        private static bool ShouldMirror(string relativePath)
        {
            if (Array.IndexOf(MirroredExtensions, Path.GetExtension(relativePath).ToLowerInvariant()) < 0)
                return false;

            var segments = relativePath.Split('/', '\\');
            if (Array.IndexOf(SkippedTopLevelDirs, segments[0]) >= 0)
                return false;

            // Thư mục/file ẩn (.git, .github…) không phải API.
            foreach (var segment in segments)
            {
                if (segment.StartsWith(".", StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static string ReadStamp(MirroredPackage package)
        {
            return File.Exists(package.StampFilePath) ? File.ReadAllText(package.StampFilePath) : null;
        }

        private static bool WriteIfChanged(string path, string content)
        {
            if (File.Exists(path) && File.ReadAllText(path) == content)
                return false;

            File.WriteAllText(path, content);
            return true;
        }
    }
}
#endif
