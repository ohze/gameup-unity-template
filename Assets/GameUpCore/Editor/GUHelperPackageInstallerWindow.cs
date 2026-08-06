#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace GameUp.Core.Editor
{
    /// <summary>
    /// Ghi nhớ những asset mà mỗi helper package đã import, để cửa sổ báo đúng "đã cài / chưa cài"
    /// thay vì lần nào mở cũng như mới. Bản ghi tự sai lệch về "chưa cài" nếu người dùng xoá thư mục.
    /// </summary>
    internal static class GUHelperPackageRegistry
    {
        private const string KeyPrefix = "GameUp.HelperPackage.";

        [Serializable]
        private sealed class PathList
        {
            public List<string> paths = new List<string>();
        }

        private static string KeyOf(string packageName) => KeyPrefix + Application.dataPath.GetHashCode() + "." + packageName;

        public static IReadOnlyList<string> GetRecordedPaths(string packageName)
        {
            var json = EditorPrefs.GetString(KeyOf(packageName), string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();

            var data = JsonUtility.FromJson<PathList>(json);
            return data?.paths ?? (IReadOnlyList<string>)Array.Empty<string>();
        }

        public static void Record(string packageName, IEnumerable<string> paths)
        {
            var list = paths?.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList() ?? new List<string>();
            if (list.Count == 0) return;

            EditorPrefs.SetString(KeyOf(packageName), JsonUtility.ToJson(new PathList { paths = list }));
        }

        public static void Forget(string packageName) => EditorPrefs.DeleteKey(KeyOf(packageName));

        public static bool AnyRecordedPathExists(string packageName)
        {
            foreach (var path in GetRecordedPaths(packageName))
            {
                if (AssetDatabase.IsValidFolder(path) || File.Exists(path)) return true;
            }

            return false;
        }

        /// <summary>Ảnh chụp thư mục/asset cấp 1-2 dưới Assets, dùng để suy ra package vừa import thêm gì.</summary>
        public static HashSet<string> SnapshotAssetRoots()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dataPath = Application.dataPath;

            foreach (var dir in Directory.GetDirectories(dataPath))
            {
                var rel = ToAssetPath(dir);
                result.Add(rel);
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    result.Add(ToAssetPath(sub));
                }
            }

            return result;
        }

        private static string ToAssetPath(string fullPath)
        {
            var normalized = fullPath.Replace("\\", "/");
            var dataPath = Application.dataPath.Replace("\\", "/");
            return normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)
                ? "Assets" + normalized.Substring(dataPath.Length)
                : normalized;
        }
    }

    public sealed class GUHelperPackageInstallerWindow : EditorWindow
    {
        private const string MenuPath = "GameUp/Project/Helper Package Installer";
        private const string WindowTitle = "Helper Package Installer";

        private static readonly IReadOnlyList<HelperModuleData> HelperModules = new List<HelperModuleData>
        {
            new HelperModuleData(
                "CoinFly",
                "Hiệu ứng coin bay + text số lượng (dùng UI Particle Image).",
                new List<HelperPackageData>
                {
                    new HelperPackageData(
                        "CoinFlyText",
                        "https://github.com/ohze/gameup-unity-template/releases/download/deps/CoinFlyText.unitypackage",
                        "CoinFlyText.unitypackage"),
                    new HelperPackageData(
                        "UIParticleImage",
                        "https://github.com/ohze/gameup-unity-template/releases/download/deps/UIParticleImage.unitypackage",
                        "UIParticleImage.unitypackage",
                        "Assets/AssetKits/ParticleImage")
                }),
            new HelperModuleData(
                "Tutorial",
                "Hệ thống tutorial (highlight, mask, step) của DuyLV.",
                new List<HelperPackageData>
                {
                    new HelperPackageData(
                        "TutorialByDuyLV",
                        "https://github.com/ohze/gameup-unity-template/releases/download/deps/TutorialByDuyLV.unitypackage",
                        "TutorialByDuyLV.unitypackage")
                })
        };

        private UnityWebRequest _downloadRequest;
        private int _currentModuleIndex;
        private bool _isImportingPackage;
        private string _downloadedPackagePath;
        private string _installMessage;
        private Vector2 _scroll;

        private readonly List<HelperPackageData> _queue = new List<HelperPackageData>();
        private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HelperPackageData _current;
        private int _queueTotal;
        private HashSet<string> _preImportSnapshot;

        private sealed class HelperModuleData
        {
            public HelperModuleData(string moduleName, string description, IReadOnlyList<HelperPackageData> packages)
            {
                ModuleName = moduleName;
                Description = description;
                Packages = packages;
            }

            public string ModuleName { get; }
            public string Description { get; }
            public IReadOnlyList<HelperPackageData> Packages { get; }
        }

        private sealed class HelperPackageData
        {
            public HelperPackageData(string packageName, string packageUrl, string fileName, params string[] markerPaths)
            {
                PackageName = packageName;
                PackageUrl = packageUrl;
                FileName = fileName;
                MarkerPaths = markerPaths ?? Array.Empty<string>();
            }

            public string PackageName { get; }
            public string PackageUrl { get; }
            public string FileName { get; }
            public string[] MarkerPaths { get; }

            public bool IsInstalled()
            {
                foreach (var marker in MarkerPaths)
                {
                    if (AssetDatabase.IsValidFolder(marker) || File.Exists(marker)) return true;
                }

                return GUHelperPackageRegistry.AnyRecordedPathExists(PackageName);
            }
        }

        [MenuItem(MenuPath)]
        private static void OpenWindow()
        {
            var window = GetWindow<GUHelperPackageInstallerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(560f, 380f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= Repaint;

            if (_downloadRequest != null)
            {
                _downloadRequest.Abort();
                _downloadRequest.Dispose();
                _downloadRequest = null;
            }

            UnregisterImportCallbacks();
            _isImportingPackage = false;
        }

        private void OnFocus() => Repaint();

        private bool IsBusy => _downloadRequest != null || _isImportingPackage;

        private void OnGUI()
        {
            GUInstallerUI.EnsureStyles();

            DrawHeader();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawPackageCards();
            EditorGUILayout.EndScrollView();

            DrawFooter();

            if (IsBusy) Repaint();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Helper Package Installer", EditorStyles.largeLabel);
            GUInstallerUI.Hint("Các gói .unitypackage phụ trợ. Trạng thái dựa trên asset thật trong project, không phải cờ đã bấm.");

            EditorGUILayout.Space(4f);
            var names = HelperModules.Select(m => m.ModuleName).ToArray();
            var newIndex = GUILayout.Toolbar(GetCurrentModuleIndex(), names);
            if (newIndex != _currentModuleIndex)
            {
                _currentModuleIndex = newIndex;
                _selected.Clear();
            }

            var module = GetCurrentModule();
            EditorGUILayout.Space(2f);
            GUILayout.Label(module.Description, GUInstallerUI.Desc);

            var installed = module.Packages.Count(p => p.IsInstalled());
            EditorGUILayout.Space(2f);
            GUInstallerUI.ProgressBar("Đã cài", installed, module.Packages.Count);
            EditorGUILayout.Space(2f);
        }

        private void DrawPackageCards()
        {
            var packages = GetCurrentModule().Packages;

            for (var index = 0; index < packages.Count; index++)
            {
                var package = packages[index];
                var installed = package.IsInstalled();
                var isCurrent = _current == package && IsBusy;
                var state = isCurrent ? GUSetupState.Busy : installed ? GUSetupState.Done : GUSetupState.Missing;

                using (GUInstallerUI.BeginCard())
                {
                    EditorGUILayout.BeginHorizontal();

                    using (new EditorGUI.DisabledScope(IsBusy))
                    {
                        var isSelected = _selected.Contains(package.PackageName);
                        var newSelected = EditorGUILayout.ToggleLeft(package.PackageName, isSelected, EditorStyles.boldLabel);
                        if (newSelected != isSelected)
                        {
                            if (newSelected) _selected.Add(package.PackageName);
                            else _selected.Remove(package.PackageName);
                        }
                    }

                    GUILayout.FlexibleSpace();
                    GUInstallerUI.DrawBadge(GUInstallerUI.LabelOf(state), GUInstallerUI.ColorOf(state));
                    EditorGUILayout.EndHorizontal();

                    var recorded = GUHelperPackageRegistry.GetRecordedPaths(package.PackageName);
                    if (installed)
                    {
                        var where = package.MarkerPaths.FirstOrDefault(AssetDatabase.IsValidFolder)
                                    ?? recorded.FirstOrDefault(p => AssetDatabase.IsValidFolder(p) || File.Exists(p));
                        if (!string.IsNullOrEmpty(where))
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUInstallerUI.Hint("Đã có tại: " + where);
                            GUILayout.FlexibleSpace();
                            if (GUInstallerUI.MiniButton("Mở", true, 50f)) GUInstallerUI.PingPath(where);
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    else
                    {
                        GUInstallerUI.Hint("Chưa phát hiện trong project.");
                    }

                    EditorGUILayout.BeginHorizontal();
                    if (GUInstallerUI.MiniButton(installed ? "Cài lại" : "Cài gói này", !IsBusy, 110f))
                    {
                        BeginInstall(new List<HelperPackageData> { package });
                    }

                    if (GUInstallerUI.MiniButton("Mở URL", true, 90f))
                    {
                        Application.OpenURL(package.PackageUrl);
                    }

                    if (recorded.Count > 0 && GUInstallerUI.MiniButton("Xoá ghi nhận", !IsBusy, 110f))
                    {
                        GUHelperPackageRegistry.Forget(package.PackageName);
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(6f);

            if (_downloadRequest != null && !_downloadRequest.isDone)
            {
                var rect = GUILayoutUtility.GetRect(18f, 18f);
                var label = _queueTotal > 1
                    ? $"Đang tải {CurrentPackageName()} ({_queueTotal - _queue.Count}/{_queueTotal})..."
                    : $"Đang tải {CurrentPackageName()}...";
                EditorGUI.ProgressBar(rect, _downloadRequest.downloadProgress, label);
            }
            else if (_downloadRequest != null)
            {
                CompletePackageDownload();
            }
            else if (_isImportingPackage)
            {
                EditorGUILayout.HelpBox($"Đang import {CurrentPackageName()}...", MessageType.Info);
            }

            if (!string.IsNullOrWhiteSpace(_installMessage))
            {
                var isError = _installMessage.IndexOf("thất bại", StringComparison.OrdinalIgnoreCase) >= 0;
                EditorGUILayout.HelpBox(_installMessage, isError ? MessageType.Error : MessageType.Info);
            }

            var packages = GetCurrentModule().Packages;
            var selectedPackages = packages.Where(p => _selected.Contains(p.PackageName)).ToList();
            var missingPackages = packages.Where(p => !p.IsInstalled()).ToList();

            EditorGUILayout.BeginHorizontal();
            if (GUInstallerUI.PrimaryButton(
                    selectedPackages.Count > 0 ? $"Cài {selectedPackages.Count} gói đã chọn" : "Cài gói còn thiếu",
                    !IsBusy && (selectedPackages.Count > 0 || missingPackages.Count > 0),
                    32f))
            {
                BeginInstall(selectedPackages.Count > 0 ? selectedPackages : missingPackages);
            }

            if (GUInstallerUI.PrimaryButton("Cài toàn bộ module", !IsBusy, 32f))
            {
                BeginInstall(packages.ToList());
            }
            EditorGUILayout.EndHorizontal();
        }

        // ─── Hàng đợi cài đặt ────────────────────────────────────────────────

        private void BeginInstall(List<HelperPackageData> packages)
        {
            if (packages == null || packages.Count == 0 || IsBusy) return;

            _queue.Clear();
            _queue.AddRange(packages);
            _queueTotal = _queue.Count;
            _installMessage = $"Bắt đầu cài {_queueTotal} gói...";
            StartNextPackageDownload();
        }

        private void StartNextPackageDownload()
        {
            if (_queue.Count == 0)
            {
                _current = null;
                _installMessage = $"Đã cài xong {_queueTotal} gói của module {GetCurrentModule().ModuleName}.";
                _queueTotal = 0;
                AssetDatabase.Refresh();
                return;
            }

            _current = _queue[0];
            _queue.RemoveAt(0);

            _downloadedPackagePath = Path.Combine(Path.GetTempPath(), _current.FileName);
            if (File.Exists(_downloadedPackagePath))
            {
                File.Delete(_downloadedPackagePath);
            }

            _downloadRequest = UnityWebRequest.Get(_current.PackageUrl);
            _downloadRequest.downloadHandler = new DownloadHandlerFile(_downloadedPackagePath);
            _downloadRequest.SendWebRequest();
            _installMessage = $"Đang tải {_current.PackageName}...";
        }

        private void CompletePackageDownload()
        {
            if (_downloadRequest == null) return;

            var result = _downloadRequest.result;
            var error = _downloadRequest.error;
            _downloadRequest.Dispose();
            _downloadRequest = null;

            if (result != UnityWebRequest.Result.Success)
            {
                _installMessage = $"Cài thất bại: không tải được {CurrentPackageName()} ({error}).";
                _queue.Clear();
                _current = null;
                return;
            }

            ImportCurrentPackage();
        }

        private void ImportCurrentPackage()
        {
            if (string.IsNullOrWhiteSpace(_downloadedPackagePath) || !File.Exists(_downloadedPackagePath))
            {
                _installMessage = $"Cài thất bại: thiếu file vừa tải của {CurrentPackageName()}.";
                _queue.Clear();
                _current = null;
                return;
            }

            _isImportingPackage = true;
            _installMessage = $"Đang import {CurrentPackageName()}...";
            _preImportSnapshot = GUHelperPackageRegistry.SnapshotAssetRoots();

            AssetDatabase.importPackageCompleted += OnPackageImportCompleted;
            AssetDatabase.importPackageFailed += OnPackageImportFailed;
            AssetDatabase.importPackageCancelled += OnPackageImportCancelled;
            AssetDatabase.ImportPackage(_downloadedPackagePath, false);
        }

        private void OnPackageImportCompleted(string packageName)
        {
            UnregisterImportCallbacks();
            _isImportingPackage = false;
            RecordImportedPaths();
            _installMessage = $"Đã import {CurrentPackageName()} ({packageName}).";
            StartNextPackageDownload();
            Repaint();
        }

        /// <summary>So ảnh chụp trước/sau import để biết gói vừa thêm thư mục nào — dùng cho trạng thái "đã cài".</summary>
        private void RecordImportedPaths()
        {
            if (_current == null || _preImportSnapshot == null) return;

            var after = GUHelperPackageRegistry.SnapshotAssetRoots();
            after.ExceptWith(_preImportSnapshot);
            _preImportSnapshot = null;

            if (after.Count > 0) GUHelperPackageRegistry.Record(_current.PackageName, after);
        }

        private void OnPackageImportFailed(string packageName, string errorMessage)
        {
            UnregisterImportCallbacks();
            _isImportingPackage = false;
            _preImportSnapshot = null;
            _installMessage = $"Cài thất bại: lỗi import {CurrentPackageName()} ({packageName}) - {errorMessage}";
            _queue.Clear();
            _current = null;
            Repaint();
        }

        private void OnPackageImportCancelled(string packageName)
        {
            UnregisterImportCallbacks();
            _isImportingPackage = false;
            _preImportSnapshot = null;
            _installMessage = $"Cài thất bại: import bị hủy ({packageName}).";
            _queue.Clear();
            _current = null;
            Repaint();
        }

        private void UnregisterImportCallbacks()
        {
            AssetDatabase.importPackageCompleted -= OnPackageImportCompleted;
            AssetDatabase.importPackageFailed -= OnPackageImportFailed;
            AssetDatabase.importPackageCancelled -= OnPackageImportCancelled;
        }

        private int GetCurrentModuleIndex()
        {
            if (_currentModuleIndex < 0 || _currentModuleIndex >= HelperModules.Count) _currentModuleIndex = 0;
            return _currentModuleIndex;
        }

        private HelperModuleData GetCurrentModule() => HelperModules[GetCurrentModuleIndex()];

        private string CurrentPackageName() => _current != null ? _current.PackageName : "package";
    }
}
#endif
