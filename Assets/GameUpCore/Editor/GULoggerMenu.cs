#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace GameUp.Core.Editor
{
    public static class GULoggerMenu
    {
        private const string SYMBOL = "ENABLE_LOG";

        // Sử dụng NamedBuildTarget thay cho BuildTargetGroup cũ
        private static readonly NamedBuildTarget[] _supportTargets = new[]
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS
        };

        [MenuItem("GameUp/Logger/Enable Logs (Debug)")]
        public static void EnableLogs()
        {
            SetLogSymbol(true);
        }

        [MenuItem("GameUp/Logger/Disable Logs (Release)")]
        public static void DisableLogs()
        {
            SetLogSymbol(false);
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
                    Debug.Log($"<color=green>[GLogger]</color> Đã <b>BẬT</b> log cho {target.TargetName}.");
                    hasChanged = true;
                }
                else if (!enable && defineList.Contains(SYMBOL))
                {
                    defineList.Remove(SYMBOL);
                    PlayerSettings.SetScriptingDefineSymbols(target, defineList.ToArray());
                    Debug.Log($"<color=orange>[GLogger]</color> Đã <b>TẮT</b> log cho {target.TargetName}.");
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
                Debug.Log($"[GLogger] Trạng thái log đã ở mức mong muốn, không có thay đổi nào.");
            }
        }
    }
}
#endif