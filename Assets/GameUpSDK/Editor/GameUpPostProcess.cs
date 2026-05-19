using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

#if UNITY_IOS
using System.IO;
using UnityEditor.iOS.Xcode;
#endif

namespace GameUp.SDK.Editor
{
    public class GameUpPostProcess : IPostprocessBuildWithReport
    {
        private const string TrackingUsageDescription = "Dữ li�?u này giúp hi�fn th�< quảng cáo phù hợp hơn v�>i bạn.";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
#if UNITY_IOS
            if (report.summary.platform != BuildTarget.iOS)
                return;

            var plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSUserTrackingUsageDescription", TrackingUsageDescription);
            File.WriteAllText(plistPath, plist.WriteToString());
#endif
        }
    }
}
