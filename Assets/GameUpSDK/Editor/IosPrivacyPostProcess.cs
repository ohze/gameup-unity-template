#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace GameUp.SDK.Editor
{
    public class IosPrivacyPostProcess
    {
        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
                return;

            var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            var trackingDescription = "This data helps us provide personalized advertising and a better experience for you.";
            plist.root.SetString("NSUserTrackingUsageDescription", trackingDescription);

            plist.WriteToFile(plistPath);
        }
    }
}
#endif
