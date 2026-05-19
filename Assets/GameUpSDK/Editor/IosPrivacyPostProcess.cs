#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public class IosPrivacyPostProcess
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        // Thêm dòng thông báo xin quyền ATT
        string trackingDescription = "This data helps us provide personalized advertising and a better experience for you.";
        plist.root.SetString("NSUserTrackingUsageDescription", trackingDescription);

        plist.WriteToFile(plistPath);
    }
}
#endif