#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace GameUp.Core.Editor
{
    public static class GUDotweenDependencyUtility
    {
        public const string DotweenInstalledDefineSymbol = "DOTween__DEPENDENCIES_INSTALLED";
        public const string DotweenProDownloadUrl = "https://github.com/ohze/gameup-unity-template/releases/download/deps/DOTween.Pro.v1.0.381.unitypackage";
        public const string DotweenFolderPath = "Assets/Plugins/Demigiant/DOTween";
        public const string DotweenModulesAsmdefPath = "Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef";
        public const string GameUpSdkGitUpmUrl = "https://github.com/ohze/gameup-unity-template.git?path=Assets/GameUpSDK";
        public const string GameUpSdkPackageName = "com.ohze.gameup.sdk";

        private static readonly NamedBuildTarget[] SupportTargets =
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS
        };

        public static bool CanUseCoreTools()
        {
            return IsDotweenDependencyInstalled();
        }

        public static bool IsDotweenDependencyInstalled()
        {
            return HasDotweenFolder() && HasDotweenModulesAsmdef() && HasDefineSymbolOnAllTargets();
        }

        public static bool HasDotweenFolder()
        {
            return AssetDatabase.IsValidFolder(DotweenFolderPath);
        }

        public static bool HasDotweenModulesAsmdef()
        {
            return File.Exists(DotweenModulesAsmdefPath);
        }

        public static bool HasDefineSymbolOnAllTargets()
        {
            for (var index = 0; index < SupportTargets.Length; index++)
            {
                var target = SupportTargets[index];
                PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
                if (defines == null || !defines.Contains(DotweenInstalledDefineSymbol))
                    return false;
            }

            return true;
        }

        public static bool EnableDefineSymbolOnAllTargets()
        {
            var changed = false;
            for (var index = 0; index < SupportTargets.Length; index++)
            {
                var target = SupportTargets[index];
                PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
                var defineList = defines?.ToList() ?? new List<string>();
                if (defineList.Contains(DotweenInstalledDefineSymbol))
                    continue;

                defineList.Add(DotweenInstalledDefineSymbol);
                PlayerSettings.SetScriptingDefineSymbols(target, defineList.ToArray());
                changed = true;
            }

            if (changed)
                AssetDatabase.Refresh();

            return changed;
        }

        public static bool DisableDefineSymbolOnAllTargets()
        {
            var changed = false;
            for (var index = 0; index < SupportTargets.Length; index++)
            {
                var target = SupportTargets[index];
                PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
                var defineList = defines?.ToList() ?? new List<string>();
                if (!defineList.Contains(DotweenInstalledDefineSymbol))
                    continue;

                defineList.Remove(DotweenInstalledDefineSymbol);
                PlayerSettings.SetScriptingDefineSymbols(target, defineList.ToArray());
                changed = true;
            }

            if (changed)
                AssetDatabase.Refresh();

            return changed;
        }

        public static bool OpenDotweenUtilityPanel()
        {
            // Hỗ trợ nhiều variant menu path của DOTween Utility Panel.
            return EditorApplication.ExecuteMenuItem("Tools/Demigiant/DOTween Utility Panel")
                   || EditorApplication.ExecuteMenuItem("Demigiant/DOTween Utility Panel");
        }

        public static bool CreateDotweenModulesAsmdefIfMissing()
        {
            if (HasDotweenModulesAsmdef())
                return true;

            if (!HasDotweenFolder())
                return false;

            var parentFolder = Path.GetDirectoryName(DotweenModulesAsmdefPath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(parentFolder))
                return false;

            if (!AssetDatabase.IsValidFolder(parentFolder))
                return false;

            var asmdefJson = "{\n    \"name\": \"DOTween.Modules\"\n}\n";
            File.WriteAllText(DotweenModulesAsmdefPath, asmdefJson);
            AssetDatabase.ImportAsset(DotweenModulesAsmdefPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh();
            return true;
        }

        public static bool IsGameUpSdkInstalled()
        {
            if (AssetDatabase.IsValidFolder("Assets/GameUpSDK"))
                return true;

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
                return false;

            var packagePath = Path.Combine(projectRoot, "Packages", GameUpSdkPackageName);
            return Directory.Exists(packagePath);
        }
    }

    public sealed class GUDotweenDependencyInstallerWindow : EditorWindow
    {
        private const string MenuPath = "GameUp/Project/GameUpCore Installer";
        private const string WindowTitle = "GameUpCore Installer";

        private AddRequest _gameUpSdkInstallRequest;
        private string _gameUpSdkInstallMessage;

        [MenuItem(MenuPath)]
        private static void OpenWindow()
        {
            var window = GetWindow<GUDotweenDependencyInstallerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(560f, 280f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("GameUpCore DOTween Dependency Installer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Git UPM flow: can setup DOTween first, then use GameUp Project/Audio tools.",
                MessageType.Info);

            DrawStatus();
            EditorGUILayout.Space(10f);
            DrawInstallActions();
            EditorGUILayout.Space(10f);
            DrawFinalizeActions();
            EditorGUILayout.Space(12f);
            DrawGameUpSdkInstallActions();
        }

        private static void DrawStatus()
        {
            DrawStatusLine("1. DOTween folder", GUDotweenDependencyUtility.HasDotweenFolder());
            DrawStatusLine("2. DOTween.Modules asmdef", GUDotweenDependencyUtility.HasDotweenModulesAsmdef());
            DrawStatusLine($"3. Define `{GUDotweenDependencyUtility.DotweenInstalledDefineSymbol}`", GUDotweenDependencyUtility.HasDefineSymbolOnAllTargets());

            var ready = GUDotweenDependencyUtility.IsDotweenDependencyInstalled();
            EditorGUILayout.HelpBox(
                ready
                    ? "DOTween dependency is ready. GameUp setup menus are unlocked."
                    : "Dependency is not ready. Complete steps below to unlock setup menus.",
                ready ? MessageType.Info : MessageType.Warning);
        }

        private static void DrawStatusLine(string label, bool ok)
        {
            var oldColor = GUI.color;
            GUI.color = ok ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.95f, 0.35f, 0.35f);
            EditorGUILayout.LabelField($"{(ok ? "[OK]" : "[MISSING]")} {label}");
            GUI.color = oldColor;
        }

        private static void DrawInstallActions()
        {
            EditorGUILayout.LabelField("Step 1 - Install DOTween Pro", EditorStyles.boldLabel);
            if (GUILayout.Button("Download DOTween.Pro.v1.0.381.unitypackage", GUILayout.Height(30f)))
            {
                Application.OpenURL(GUDotweenDependencyUtility.DotweenProDownloadUrl);
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Open DOTween Utility Panel", GUILayout.Height(26f)))
            {
                var opened = GUDotweenDependencyUtility.OpenDotweenUtilityPanel();
                if (!opened)
                {
                    EditorUtility.DisplayDialog(
                        "DOTween Utility Panel Not Found",
                        "Could not open DOTween Utility Panel.\nPlease install DOTween first, then open it manually from Tools/Demigiant.",
                        "OK");
                }
            }

            using (new EditorGUI.DisabledScope(!GUDotweenDependencyUtility.HasDotweenFolder()))
            {
                if (GUILayout.Button("Create DOTween.Modules asmdef", GUILayout.Height(26f)))
                {
                    var created = GUDotweenDependencyUtility.CreateDotweenModulesAsmdefIfMissing();
                    if (!created)
                    {
                        EditorUtility.DisplayDialog(
                            "Create asmdef failed",
                            "Could not create DOTween.Modules.asmdef. Please run it from DOTween Utility Panel.",
                            "OK");
                    }
                }
            }
        }

        private static void DrawFinalizeActions()
        {
            EditorGUILayout.LabelField("Step 2 - Finalize for GameUp", EditorStyles.boldLabel);
            if (GUILayout.Button($"Enable define `{GUDotweenDependencyUtility.DotweenInstalledDefineSymbol}`", GUILayout.Height(30f)))
            {
                GUDotweenDependencyUtility.EnableDefineSymbolOnAllTargets();
                if (GUDotweenDependencyUtility.IsDotweenDependencyInstalled())
                {
                    EditorApplication.ExecuteMenuItem("GameUp/Project/Folder Setup");
                }
            }

            using (new EditorGUI.DisabledScope(!GUDotweenDependencyUtility.HasDefineSymbolOnAllTargets()))
            {
                if (GUILayout.Button("Disable DOTween dependency define", GUILayout.Height(24f)))
                {
                    GUDotweenDependencyUtility.DisableDefineSymbolOnAllTargets();
                }
            }
        }

        private void DrawGameUpSdkInstallActions()
        {
            EditorGUILayout.LabelField("Optional - Install GameUpSDK (Git UPM)", EditorStyles.boldLabel);
            DrawStatusLine("GameUpSDK package", GUDotweenDependencyUtility.IsGameUpSdkInstalled());

            using (new EditorGUI.DisabledScope(_gameUpSdkInstallRequest != null && !_gameUpSdkInstallRequest.IsCompleted))
            {
                if (GUILayout.Button("Install GameUpSDK via Git UPM", GUILayout.Height(28f)))
                {
                    _gameUpSdkInstallMessage = "Installing GameUpSDK...";
                    _gameUpSdkInstallRequest = Client.Add(GUDotweenDependencyUtility.GameUpSdkGitUpmUrl);
                }
            }

            if (_gameUpSdkInstallRequest != null)
            {
                if (_gameUpSdkInstallRequest.IsCompleted)
                {
                    if (_gameUpSdkInstallRequest.Status == StatusCode.Success)
                    {
                        _gameUpSdkInstallMessage = $"Installed: {_gameUpSdkInstallRequest.Result.name} {_gameUpSdkInstallRequest.Result.version}";
                    }
                    else if (_gameUpSdkInstallRequest.Status >= StatusCode.Failure)
                    {
                        _gameUpSdkInstallMessage = $"Install failed: {_gameUpSdkInstallRequest.Error.message}";
                    }

                    _gameUpSdkInstallRequest = null;
                    AssetDatabase.Refresh();
                }
                else
                {
                    Repaint();
                }
            }

            if (!string.IsNullOrWhiteSpace(_gameUpSdkInstallMessage))
            {
                var msgType = _gameUpSdkInstallMessage.StartsWith("Install failed:", StringComparison.OrdinalIgnoreCase)
                    ? MessageType.Error
                    : MessageType.Info;
                EditorGUILayout.HelpBox(_gameUpSdkInstallMessage, msgType);
            }
        }
    }
}
#endif
