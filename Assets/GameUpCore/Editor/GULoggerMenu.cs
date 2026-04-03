#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace GameUp.Core.Editor
{
    public static class GULoggerMenu
    {
        private const string SYMBOL = "ENABLE_LOG";
        private const string EnableMenuPath = "GameUp/Logger/Enable Logs (Debug)";
        private const string DisableMenuPath = "GameUp/Logger/Disable Logs (Release)";
        private const string ProjectFolderSetupCompletedKey = "GameUp.ProjectFolderSetup.Completed";

        // Sử dụng NamedBuildTarget thay cho BuildTargetGroup cũ
        private static readonly NamedBuildTarget[] _supportTargets = new[]
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS
        };

        [MenuItem(EnableMenuPath)]
        public static void EnableLogs()
        {
            SetLogSymbol(true);
        }

        [MenuItem(DisableMenuPath)]
        public static void DisableLogs()
        {
            SetLogSymbol(false);
        }

        [MenuItem(EnableMenuPath, true)]
        private static bool ValidateEnableLogs()
        {
            return GUDotweenDependencyUtility.CanUseCoreTools()
                   && EditorPrefs.GetBool(ProjectFolderSetupCompletedKey, false);
        }

        [MenuItem(DisableMenuPath, true)]
        private static bool ValidateDisableLogs()
        {
            return GUDotweenDependencyUtility.CanUseCoreTools()
                   && EditorPrefs.GetBool(ProjectFolderSetupCompletedKey, false);
        }

        private static void SetLogSymbol(bool enable)
        {
            bool hasChanged = false;

            foreach (var target in _supportTargets)
            {
                // Lấy danh sách symbol hiện tại của nền tảng
                PlayerSettings.GetScriptingDefineSymbols(target, out string[] defines);
                var defineList = defines.ToList();

                if (enable && !defineList.Contains(SYMBOL))
                {
                    defineList.Add(SYMBOL);
                    PlayerSettings.SetScriptingDefineSymbols(target, defineList.ToArray());
                    GULogger.Log($"<color=green>[GLogger]</color> Đã <b>BẬT</b> log cho {target.TargetName}.");
                    hasChanged = true;
                }
                else if (!enable && defineList.Contains(SYMBOL))
                {
                    defineList.Remove(SYMBOL);
                    PlayerSettings.SetScriptingDefineSymbols(target, defineList.ToArray());
                    GULogger.Log($"<color=orange>[GLogger]</color> Đã <b>TẮT</b> log cho {target.TargetName}.");
                    hasChanged = true;
                }
            }

            if (hasChanged)
            {
                // Ép Unity compile lại code ngay lập tức để áp dụng thay đổi
                AssetDatabase.Refresh();
            }
            else
            {
                GULogger.Log("GLogger", "Trạng thái log đã ở mức mong muốn, không có thay đổi nào.");
            }
        }
    }
}
#endif