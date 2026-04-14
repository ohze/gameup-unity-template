#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace GameUp.Core.Editor
{
    public sealed class GUHelperPackageInstallerWindow : EditorWindow
    {
        private const string MenuPath = "GameUp/Project/Helper Package Installer";
        private const string WindowTitle = "Helper Package Installer";

        private static readonly IReadOnlyList<HelperModuleData> HelperModules = new List<HelperModuleData>
        {
            new HelperModuleData(
                "CoinFly",
                "Install helper packages cho CoinFly effect.",
                new List<HelperPackageData>
                {
                    new HelperPackageData(
                        "CoinFlyText",
                        "https://github.com/ohze/gameup-unity-template/releases/download/deps/CoinFlyText.unitypackage",
                        "CoinFlyText.unitypackage"),
                    new HelperPackageData(
                        "UIParticleImage",
                        "https://github.com/ohze/gameup-unity-template/releases/download/deps/UIParticleImage.unitypackage",
                        "UIParticleImage.unitypackage")
                })
        };

        private UnityWebRequest _downloadRequest;
        private int _currentModuleIndex;
        private int _currentPackageIndex = -1;
        private bool _isImportingPackage;
        private string _downloadedPackagePath;
        private string _installMessage;

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
            public HelperPackageData(string packageName, string packageUrl, string fileName)
            {
                PackageName = packageName;
                PackageUrl = packageUrl;
                FileName = fileName;
            }

            public string PackageName { get; }
            public string PackageUrl { get; }
            public string FileName { get; }
        }

        [MenuItem(MenuPath)]
        private static void OpenWindow()
        {
            var window = GetWindow<GUHelperPackageInstallerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(620f, 320f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("GameUp Helper Package Installer", EditorStyles.boldLabel);
            DrawModuleSelection();
            EditorGUILayout.Space(6f);
            DrawPackageList();
            EditorGUILayout.Space(8f);
            DrawInstallAction();
            EditorGUILayout.Space(10f);
            DrawInstallationStatus();
            EditorGUILayout.Space(6f);
            DrawOpenReleaseLinksAction();
        }

        private void DrawModuleSelection()
        {
            var moduleNames = GetModuleNames();
            _currentModuleIndex = EditorGUILayout.Popup("Helper Module", _currentModuleIndex, moduleNames);
            EditorGUILayout.HelpBox(GetCurrentModule().Description, MessageType.Info);
        }

        private string[] GetModuleNames()
        {
            var names = new string[HelperModules.Count];
            for (var index = 0; index < HelperModules.Count; index++)
            {
                names[index] = HelperModules[index].ModuleName;
            }

            return names;
        }

        private void DrawPackageList()
        {
            var packages = GetCurrentModule().Packages;
            EditorGUILayout.LabelField("Packages", EditorStyles.boldLabel);
            for (var index = 0; index < packages.Count; index++)
            {
                EditorGUILayout.LabelField($"- {packages[index].PackageName}", EditorStyles.label);
            }
        }

        private void DrawInstallAction()
        {
            using (new EditorGUI.DisabledScope(_downloadRequest != null || _isImportingPackage))
            {
                if (GUILayout.Button($"Download & Auto Install {GetCurrentModule().ModuleName} Helpers", GUILayout.Height(32f)))
                {
                    BeginInstallPackages();
                }
            }
        }

        private void DrawInstallationStatus()
        {
            if (_downloadRequest != null)
            {
                if (_downloadRequest.isDone)
                {
                    CompletePackageDownload();
                }
                else
                {
                    var progressRect = GUILayoutUtility.GetRect(18f, 18f, "TextField");
                    EditorGUI.ProgressBar(progressRect, _downloadRequest.downloadProgress, $"Downloading {GetCurrentPackageName()}...");
                    Repaint();
                }
            }
            else if (_isImportingPackage)
            {
                EditorGUILayout.HelpBox($"Importing {GetCurrentPackageName()} package...", MessageType.Info);
                Repaint();
            }

            if (!string.IsNullOrWhiteSpace(_installMessage))
            {
                var isError = _installMessage.StartsWith("Install failed:", StringComparison.OrdinalIgnoreCase);
                EditorGUILayout.HelpBox(_installMessage, isError ? MessageType.Error : MessageType.Info);
            }
        }

        private void DrawOpenReleaseLinksAction()
        {
            if (GUILayout.Button("Open Module Package URLs", GUILayout.Height(24f)))
            {
                var packages = GetCurrentModule().Packages;
                for (var index = 0; index < packages.Count; index++)
                {
                    Application.OpenURL(packages[index].PackageUrl);
                }
            }
        }

        private void BeginInstallPackages()
        {
            _currentPackageIndex = -1;
            _installMessage = $"Starting {GetCurrentModule().ModuleName} helper installation...";
            StartNextPackageDownload();
        }

        private void StartNextPackageDownload()
        {
            var packages = GetCurrentModule().Packages;
            _currentPackageIndex++;
            if (_currentPackageIndex >= packages.Count)
            {
                _installMessage = $"Installed all packages for module {GetCurrentModule().ModuleName} successfully.";
                _currentPackageIndex = -1;
                AssetDatabase.Refresh();
                return;
            }

            var package = packages[_currentPackageIndex];
            _downloadedPackagePath = Path.Combine(Path.GetTempPath(), package.FileName);
            if (File.Exists(_downloadedPackagePath))
            {
                File.Delete(_downloadedPackagePath);
            }

            _downloadRequest = UnityWebRequest.Get(package.PackageUrl);
            _downloadRequest.downloadHandler = new DownloadHandlerFile(_downloadedPackagePath);
            _downloadRequest.SendWebRequest();
            _installMessage = $"Downloading {package.PackageName}...";
        }

        private void CompletePackageDownload()
        {
            if (_downloadRequest == null)
            {
                return;
            }

            var result = _downloadRequest.result;
            var error = _downloadRequest.error;
            _downloadRequest.Dispose();
            _downloadRequest = null;

            if (result != UnityWebRequest.Result.Success)
            {
                _installMessage = $"Install failed: cannot download {GetCurrentPackageName()} ({error}).";
                return;
            }

            ImportCurrentPackage();
        }

        private void ImportCurrentPackage()
        {
            if (string.IsNullOrWhiteSpace(_downloadedPackagePath) || !File.Exists(_downloadedPackagePath))
            {
                _installMessage = $"Install failed: missing downloaded file for {GetCurrentPackageName()}.";
                return;
            }

            _isImportingPackage = true;
            _installMessage = $"Importing {GetCurrentPackageName()}...";

            AssetDatabase.importPackageCompleted += OnPackageImportCompleted;
            AssetDatabase.importPackageFailed += OnPackageImportFailed;
            AssetDatabase.importPackageCancelled += OnPackageImportCancelled;
            AssetDatabase.ImportPackage(_downloadedPackagePath, false);
        }

        private void OnPackageImportCompleted(string packageName)
        {
            UnregisterImportCallbacks();
            _isImportingPackage = false;
            _installMessage = $"Imported {GetCurrentPackageName()} ({packageName}).";
            StartNextPackageDownload();
            Repaint();
        }

        private void OnPackageImportFailed(string packageName, string errorMessage)
        {
            UnregisterImportCallbacks();
            _isImportingPackage = false;
            _installMessage = $"Install failed: import error on {GetCurrentPackageName()} ({packageName}) - {errorMessage}";
            Repaint();
        }

        private void OnPackageImportCancelled(string packageName)
        {
            UnregisterImportCallbacks();
            _isImportingPackage = false;
            _installMessage = $"Install failed: import cancelled on {GetCurrentPackageName()} ({packageName}).";
            Repaint();
        }

        private void UnregisterImportCallbacks()
        {
            AssetDatabase.importPackageCompleted -= OnPackageImportCompleted;
            AssetDatabase.importPackageFailed -= OnPackageImportFailed;
            AssetDatabase.importPackageCancelled -= OnPackageImportCancelled;
        }

        private HelperModuleData GetCurrentModule()
        {
            if (_currentModuleIndex < 0 || _currentModuleIndex >= HelperModules.Count)
            {
                _currentModuleIndex = 0;
            }

            return HelperModules[_currentModuleIndex];
        }

        private string GetCurrentPackageName()
        {
            var packages = GetCurrentModule().Packages;
            if (_currentPackageIndex < 0 || _currentPackageIndex >= packages.Count)
            {
                return "package";
            }

            return packages[_currentPackageIndex].PackageName;
        }

        private void OnDisable()
        {
            if (_downloadRequest != null)
            {
                _downloadRequest.Abort();
                _downloadRequest.Dispose();
                _downloadRequest = null;
            }

            UnregisterImportCallbacks();
            _isImportingPackage = false;
        }
    }
}
#endif
