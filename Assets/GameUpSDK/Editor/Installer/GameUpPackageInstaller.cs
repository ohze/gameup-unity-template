using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Installer
{
    /// <summary>
    /// Chạy mỗi lần Unity Editor load. Tự động mở cửa sổ cài đặt dependency
    /// nếu đây là lần đầu tiên SDK được cài đặt trong dự án này,
    /// hoặc nếu vẫn còn package bắt buộc chưa được cài.
    /// </summary>
    [InitializeOnLoad]
    static class GameUpPackageInstaller
    {
        // Key lưu per-project (EditorPrefs dùng project path làm prefix ở Unity 2020+)
        private const string SetupDoneKey = "GameUpSDK_SetupComplete_v1";

        // Key ngăn mở lại window trong cùng một Unity session
        private const string SessionShownKey = "GameUpSDK_ShownThisSession";

        static GameUpPackageInstaller()
        {
            // Nếu đã mở trong session này thì bỏ qua
            if (SessionState.GetBool(SessionShownKey, false)) return;
            SessionState.SetBool(SessionShownKey, true);

            // Dùng delayCall để đợi Editor load xong hoàn toàn
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            bool allInstalled = GameUpDependenciesWindow.AreAllRequiredPackagesInstalled();

            if (allInstalled)
            {
                // Deps đủ: set define và mark complete (dù cài thủ công hay qua installer)
                GameUpDependenciesWindow.SetDepsReadyDefine(true);
                MarkSetupComplete();
                return;
            }

            // Còn thiếu deps: mở cửa sổ cài đặt
            GameUpDependenciesWindow.ShowWindow();
        }

        /// <summary>
        /// Đánh dấu setup hoàn thành cho project hiện tại.
        /// Được gọi từ GameUpDependenciesWindow khi tất cả package bắt buộc đã cài xong.
        /// </summary>
        public static void MarkSetupComplete()
        {
            EditorPrefs.SetBool(GetSetupKey(), true);
        }

        /// <summary>
        /// Reset trạng thái setup — mở lại cửa sổ dependencies vào lần load tiếp theo.
        /// </summary>
        [MenuItem("GameUp SDK/Reset Setup Status")]
        public static void ResetSetupStatus()
        {
            EditorPrefs.DeleteKey(GetSetupKey());
            SessionState.EraseBool(SessionShownKey);
            EditorUtility.DisplayDialog("GameUp SDK", "Đã reset. Khởi động lại Unity để mở lại cửa sổ setup.", "OK");
        }

        private static string GetSetupKey()
        {
            // Bao gồm project path để key là riêng biệt per-project
            return SetupDoneKey + "_" + Application.dataPath.GetHashCode();
        }
    }
}
