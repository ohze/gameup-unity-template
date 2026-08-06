using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

#if UNITY_IOS
using System.IO;
using UnityEditor.iOS.Xcode;
#endif

namespace GameUp.SDK.Editor
{
    public class GameUpPostProcess : IPostprocessBuildWithReport
    {
        /// <summary>
        /// Câu mặc định cho hộp thoại ATT. Dùng khi GameUpSdkConfig.trackingUsageDescription để trống.
        /// LƯU Ý: user đọc thấy câu này và Apple review nó — giữ nguyên tiếng Việt có dấu, đừng để
        /// file bị lưu sai encoding (bản trước đây đã dính ký tự U+FFFD và lọt vào Info.plist).
        /// </summary>
        private const string DefaultTrackingUsageDescription =
            "Dữ liệu này giúp hiển thị quảng cáo phù hợp hơn với bạn.";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
#if UNITY_IOS
            if (report.summary.platform != BuildTarget.iOS)
                return;

            var plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSUserTrackingUsageDescription", ResolveTrackingUsageDescription());
            File.WriteAllText(plistPath, plist.WriteToString());
#endif
        }

        private static string ResolveTrackingUsageDescription()
        {
            var config = GameUpSdkConfig.Instance;
            var custom = config != null ? config.trackingUsageDescription : null;
            if (!string.IsNullOrWhiteSpace(custom)) return custom.Trim();

            Debug.Log("[GameUp.SDK] NSUserTrackingUsageDescription dùng câu mặc định. " +
                      "Sửa ở GameUpSdkConfig.trackingUsageDescription nếu muốn câu riêng.");
            return DefaultTrackingUsageDescription;
        }
    }
}
