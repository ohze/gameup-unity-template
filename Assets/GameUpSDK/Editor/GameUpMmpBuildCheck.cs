using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameUp.SDK.Editor
{
    /// <summary>
    /// Soát cấu hình MMP (AppsFlyer / Adjust) trước khi build. Chỉ cảnh báo, không chặn build — các lỗi này
    /// không làm app crash nhưng làm mất attribution/doanh thu mà không ai nhận ra cho tới khi xem dashboard.
    /// </summary>
    public class GameUpMmpBuildCheck : IPreprocessBuildWithReport
    {
        private const string SetupHint = "Sửa ở GameUp → SDK → Setup.";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            if (target != BuildTarget.Android && target != BuildTarget.iOS) return;

            var config = GameUpSdkConfig.Instance;
            if (config == null) return;

            bool isIOS = target == BuildTarget.iOS;
            bool isRelease = (report.summary.options & BuildOptions.Development) == 0;
            var issues = new List<string>();

#if GAMEUP_MMP_ADJUST
            CollectAdjustIssues(config.adjust, isIOS, isRelease, issues);
#if APPSFLYER_DEPENDENCIES_INSTALLED
            issues.Add("MMP đang chọn là Adjust nhưng AppsFlyer SDK vẫn còn trong project (tăng size build). " +
                       "Gỡ ở GameUp → SDK → Setup Dependencies.");
#endif
#else
            CollectAppsFlyerIssues(config.appsFlyer, isIOS, isRelease, issues);
#if ADJUST_DEPENDENCIES_INSTALLED
            issues.Add("MMP đang chọn là AppsFlyer nhưng Adjust SDK vẫn còn trong project (tăng size build). " +
                       "Gỡ ở GameUp → SDK → Setup Dependencies.");
#endif
#endif

            foreach (var issue in issues)
                Debug.LogWarning($"[GameUpSDK] MMP: {issue}");
        }

        private static void CollectAdjustIssues(AdjustSettings adjust, bool isIOS, bool isRelease, List<string> issues)
        {
#if !ADJUST_DEPENDENCIES_INSTALLED
            issues.Add("MMP đang chọn là Adjust nhưng chưa cài Adjust SDK — build sẽ không có attribution. Cài ở GameUp → SDK → Setup Dependencies.");
#endif
            if (adjust == null) return;

            if (string.IsNullOrWhiteSpace(adjust.GetAppToken(isIOS)))
                issues.Add($"Chưa có Adjust App Token {(isIOS ? "iOS" : "Android")} — Adjust sẽ không init. {SetupHint}");

            if (isRelease && adjust.sandbox)
                issues.Add($"Adjust đang ở Sandbox trong build release — install/doanh thu sẽ KHÔNG được tính. {SetupHint}");

            if (isRelease && adjust.verboseLog)
                issues.Add($"Adjust Verbose Log đang bật trong build release. {SetupHint}");

            int missing = 0;
            if (adjust.eventTokens != null)
            {
                foreach (var entry in adjust.eventTokens)
                {
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.eventName) && string.IsNullOrWhiteSpace(entry.token))
                        missing++;
                }
            }
            if (missing > 0)
                issues.Add($"{missing} event Adjust chưa có token — các event này sẽ bị bỏ qua. {SetupHint}");
        }

        private static void CollectAppsFlyerIssues(AppsFlyerSettings appsFlyer, bool isIOS, bool isRelease, List<string> issues)
        {
#if !APPSFLYER_DEPENDENCIES_INSTALLED
            issues.Add("MMP đang chọn là AppsFlyer nhưng chưa cài AppsFlyer SDK — build sẽ không có attribution. Cài ở GameUp → SDK → Setup Dependencies.");
#endif
            if (appsFlyer == null) return;

            if (string.IsNullOrWhiteSpace(appsFlyer.devKey))
                issues.Add($"Chưa có AppsFlyer Dev Key — AppsFlyer sẽ không ghi nhận install. {SetupHint}");

            if (isIOS && string.IsNullOrWhiteSpace(appsFlyer.appIdIOS))
                issues.Add($"Chưa có AppsFlyer App ID iOS. {SetupHint}");

            if (isRelease && appsFlyer.isDebug)
                issues.Add($"AppsFlyer Debug đang bật trong build release. {SetupHint}");
        }
    }
}
