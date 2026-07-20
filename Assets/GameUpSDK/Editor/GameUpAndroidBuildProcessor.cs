#if UNITY_EDITOR
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace GameUp.SDK.Editor
{
    // Script này tự động chạy sau khi Unity xuất ra project Gradle (Ngay trước khi build ra APK/AAB)
    public class GameUpAndroidBuildProcessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 99; // Chạy sau cùng để đảm bảo file proguard đã được Unity sinh ra

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // 'path' là đường dẫn tới thư mục gradle của project (Temp/gradleOut)
            string proguardFilePath = Path.Combine(path, "proguard-unity.txt");
            
            // Nếu dùng Custom Proguard, Unity có thể đẩy ra tên file khác
            if (!File.Exists(proguardFilePath))
            {
                proguardFilePath = Path.Combine(path, "proguard-user.txt");
            }

            if (File.Exists(proguardFilePath))
            {
                string currentContent = File.ReadAllText(proguardFilePath);

                // THÊM MỚI: Kiểm tra chống trùng lặp. 
                // Tránh việc mỗi lần Build lại bị append thêm 1 cục text giống hệt nhau làm rác file.
                if (!currentContent.Contains("GAMEUP SDK PROGUARD RULES"))
                {
                    // Luật ProGuard bảo vệ class Java của GameUp SDK khỏi bị obfuscate (làm rối mã)
                    string proGuardRules = @"
# ==========================================
# GAMEUP SDK PROGUARD RULES (AUTO GENERATED)
# ==========================================

# 1. Rules cho Native FullScreen (Hỗ trợ Multi-ID Waterfall & Bidding)
-keep class com.plugins.nativebridge.UnityNativeFullScreen { *; }
-keep interface com.plugins.nativebridge.UnityNativeFullScreen$INativeAdCallback { *; }

# 2. Rules cho Native Collapsible Banner
-keep class com.gameup.ads.NativeBannerManager { *; }
-keep interface com.gameup.ads.NativeBannerManager$AdCallback { *; }
";
                    // Chèn thêm luật vào cuối file
                    File.AppendAllText(proguardFilePath, "\n" + proGuardRules);
                    Debug.Log("[GameUp.SDK] Đã tự động chèn ProGuard Rules cho Native Ads thành công.");
                }
                else
                {
                    // Log nhẹ để báo hiệu rules đã tồn tại và hệ thống đã chủ động bỏ qua
                    Debug.Log("[GameUp.SDK] ProGuard Rules đã tồn tại trong cấu hình Gradle, bỏ qua bước chèn thêm.");
                }
            }
            else
            {
                Debug.LogWarning("[GameUp.SDK] Không tìm thấy file proguard-unity.txt hoặc proguard-user.txt để cấu hình Native Ads!");
            }
        }
    }
}
#endif