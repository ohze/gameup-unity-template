using GameUp.Core;
using UnityEngine;
using System;
using GameUp.SDK; // Đảm bảo sử dụng namespace mới chứa AdsManager

namespace GameUp.SDK
{
    public class AdsExample : MonoBehaviour
    {
        private int _currentLevel = 1;
        private string _statusLog = "Sẵn sàng. Nhấn các nút bên dưới để test quảng cáo.";
        private Vector2 _scrollPosition;

        private void Start()
        {
            AdCappingManager.Instance.SetCappingLimit(AdUnitType.Interstitial, 15, 0);
            AdsManager.Instance.AddCondition(new CappingTimeCondition(AdUnitType.Interstitial));
        }

        private void OnGUI()
        {
            // Thiết lập vùng hiển thị UI bằng GUILayout tại góc trên bên trái màn hình
            GUILayout.BeginArea(new Rect(20, 20, 380, Screen.height - 40));
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(380),
                GUILayout.Height(Screen.height - 40));

            GUILayout.Box("--- GAMEUP SDK ADS DEMO ---", GUILayout.ExpandWidth(true));

            // Hiển thị trạng thái mô phỏng
            GUILayout.Label($"<b>Trạng thái:</b> {_statusLog}",
                new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });
            GUILayout.Label($"Mô phỏng Level hiện tại: {_currentLevel}");

            // Tăng giảm level giả lập để test điều kiện inter_start_level
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Level -1", GUILayout.Height(30)))
            {
                _currentLevel = Mathf.Max(1, _currentLevel - 1);
            }

            if (GUILayout.Button("Level +1", GUILayout.Height(30)))
            {
                _currentLevel++;
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            // =================================================================
            // SECTION 1: INIT / PRELOAD ADS
            // =================================================================
            GUILayout.Box("1. Khởi tạo Hệ thống", GUILayout.ExpandWidth(true));

            GUILayout.Space(10);

            // =================================================================
            // SECTION 2: BANNER ADS
            // =================================================================
            GUILayout.Box("2. Quảng cáo Banner", GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Show Standard Banner ('main')", GUILayout.Height(30)))
            {
                LogStatus("Yêu cầu hiển thị Banner tiêu chuẩn...");
                AdsManager.Instance.ShowBanner("main");
            }
            
            if (GUILayout.Button("Hide Banner", GUILayout.Height(30)))
            {
                LogStatus("Đang ẩn Banner vị trí 'main'...");
                AdsManager.Instance.HideBanner("main");
            }

            GUILayout.Space(10);

            // =================================================================
            // SECTION 3: INTERSTITIAL ADS
            // =================================================================
            GUILayout.Box("3. Quảng cáo Xen Kẽ (Interstitial)", GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Show Interstitial (Có check Rule)", GUILayout.Height(35)))
            {
                LogStatus("Đang kiểm tra Capping/Level và gọi Interstitial...");

                AdsManager.Instance.ShowInterstitial(
                    where: "end_game_revive",
                    currentLevel: _currentLevel,
                    onSuccess: () => LogStatus("Interstitial đã xem xong hoặc được đóng thành công!"),
                    onFail: () =>
                        LogStatus("Interstitial thất bại (Chưa load xong, hoặc bị chặn bởi Capping Time/Level).")
                );
            }

            GUILayout.Space(10);

            // =================================================================
            // SECTION 4: REWARDED ADS
            // =================================================================
            GUILayout.Box("4. Quảng cáo Nhận Quà (Rewarded)", GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Show Rewarded Video", GUILayout.Height(35)))
            {
                LogStatus("Đang mở quảng cáo video nhận thưởng...");

                // Mẹo: Bạn nên tạm thời tắt âm thanh của game tại đây trước khi gọi ad
                AdsManager.Instance.ShowRewardedVideo(
                    where: "claim_double_gold",
                    currentLevel: _currentLevel,
                    onSuccess: () =>
                    {
                        LogStatus("Thành công! Người chơi đã xem xong video. Tặng quà: +100 Gold!");
                        // Thực hiện cộng tiền vàng/vật phẩm thực tế ở đây
                    },
                    onFail: () => { LogStatus("Thất bại! Video chưa sẵn sàng hoặc người chơi tắt giữa chừng."); }
                );
            }

            GUILayout.Space(10);

            // =================================================================
            // SECTION 5: APP OPEN ADS
            // =================================================================
            GUILayout.Box("5. Quảng cáo Mở App (App Open)", GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Show App Open Ad", GUILayout.Height(35)))
            {
                LogStatus("Yêu cầu hiển thị App Open Ad...");

                AdsManager.Instance.ShowAppOpenAds(
                    where: "resume_app",
                    onSuccess: () => LogStatus("Đã đóng App Open Ad -> Tiếp tục game."),
                    onFail: () => LogStatus("App Open Ad chưa sẵn sàng hoặc đã hết hạn (4 tiếng).")
                );
            }

            GUILayout.Space(10);

            // =================================================================
            // SECTION 6: NATIVE ADS
            // =================================================================
            GUILayout.Box("6. Native Ad", GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Show Native Ad", GUILayout.Height(35)))
            {
                LogStatus("Yêu cầu hiển thị Native Ad...");

                // Lưu ý: Đối với NativeAd ta có thể không cần truyền level tuỳ thuộc vào quy định game
                AdsManager.Instance.ShowNativeAd(
                    where: "native_menu",
                    onSuccess: () => LogStatus("Đã hiển thị Native Ad thành công."),
                    onFail: () => LogStatus("Hiển thị Native Ad lỗi hoặc chưa tải xong.")
                );
            }

            if (GUILayout.Button("Hide Native Ad", GUILayout.Height(30)))
            {
                LogStatus("Đang ẩn Native Ad...");
                AdsManager.Instance.HideNativeAd("native_menu");
            }

            if (GUILayout.Button("Show Native Banner UI", GUILayout.Height(30)))
            {
                RuntimeCollapsibleUI.Create(() =>
                {
                    GULogger.Log($"Banner change");
                });
            }
            
            GUILayout.Space(30);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void LogStatus(string text)
        {
            _statusLog = $"[{DateTime.Now:HH:mm:ss}] {text}";
            GULogger.Log($"[AdsExample] {text}");
        }
    }
}