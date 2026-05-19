using UnityEngine;
using System;

namespace GameUp.SDK
{
    public class AdsExample : MonoBehaviour
    {
        private int _currentLevel = 1;
        private string _statusLog = "Sáºµn sÃ ng. Nháº¥n cÃ¡c nÃºt bÃªn dÆ°á»›i Ä‘á»ƒ test ad.";
        private Vector2 _scrollPosition;

        private void OnGUI()
        {
            // Thiáº¿t láº­p vÃ¹ng hiá»ƒn thá»‹ UI báº±ng GUILayout táº¡i gÃ³c trÃªn bÃªn trÃ¡i mÃ n hÃ¬nh
            GUILayout.BeginArea(new Rect(20, 20, 350, Screen.height - 40));
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(350), GUILayout.Height(Screen.height - 40));

            GUILayout.Box("--- GAMEUP SDK ADS DEMO ---", GUILayout.ExpandWidth(true));
            
            // Hiá»ƒn thá»‹ tráº¡ng thÃ¡i mÃ´ phá»ng
            GUILayout.Label($"<b>Tráº¡ng thÃ¡i:</b> {_statusLog}", new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });
            GUILayout.Label($"MÃ´ phá»ng Level hiá»‡n táº¡i: {_currentLevel}");
            
            // TÄƒng giáº£m level giáº£ láº­p Ä‘á»ƒ test Ä‘iá»u kiá»‡n inter_start_level
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Level -1")) { _currentLevel = Mathf.Max(1, _currentLevel - 1); }
            if (GUILayout.Button("Level +1")) { _currentLevel++; }
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            // =================================================================
            // SECTION 1: REQUESTS / PRELOAD ADS
            // =================================================================
            GUILayout.Box("1. Táº£i TrÆ°á»›c Quáº£ng CÃ¡o (Preload)", GUILayout.ExpandWidth(true));
            
            if (GUILayout.Button("Request All Ads (Táº£i táº¥t cáº£ cÃ¡c Ä‘á»‹nh dáº¡ng)", GUILayout.Height(35)))
            {
                LogStatus("Äang gá»i RequestAll()...");
                AdsManager.Instance.RequestAll();
            }

            if (GUILayout.Button("Preload Collapsible Banner", GUILayout.Height(30)))
            {
                LogStatus("Äang táº£i trÆ°á»›c Collapsible Banner cho vá»‹ trÃ­ 'main'...");
                AdsManager.Instance.RequestCollapsibleBanner("main", CollapsibleBannerPlacement.Bottom);
            }

            GUILayout.Space(10);

            // =================================================================
            // SECTION 2: BANNER ADS
            // =================================================================
            GUILayout.Box("2. Quáº£ng cÃ¡o Banner", GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Show Standard Banner ('main')", GUILayout.Height(30)))
            {
                LogStatus("YÃªu cáº§u hiá»ƒn thá»‹ Banner tiÃªu chuáº©n vá»‹ trÃ­ 'main'...");
                AdsManager.Instance.ShowBanner("main", onRqFail: () => LogStatus("Show Standard Banner tháº¥t báº¡i (onRqFail)."));
            }

            if (GUILayout.Button("Show Collapsible Banner (Bottom)", GUILayout.Height(30)))
            {
                LogStatus("YÃªu cáº§u hiá»ƒn thá»‹ Collapsible Banner dáº¡ng trÆ°á»£t á»Ÿ dÆ°á»›i mÃ n hÃ¬nh...");
                AdsManager.Instance.ShowCollapsibleBanner("main", CollapsibleBannerPlacement.Bottom, onRqFail: () => LogStatus("Show Collapsible Banner tháº¥t báº¡i."));
            }

            if (GUILayout.Button("Hide Banner", GUILayout.Height(30)))
            {
                LogStatus("Äang áº©n Banner vá»‹ trÃ­ 'main'...");
                AdsManager.Instance.HideBanner("main");
            }

            GUILayout.Space(10);

            // =================================================================
            // SECTION 3: INTERSTITIAL ADS
            // =================================================================
            GUILayout.Box("3. Quáº£ng cÃ¡o Xen Káº½ (Interstitial)", GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Show Interstitial (Kiá»ƒm tra luáº­t)", GUILayout.Height(35)))
            {
                LogStatus("YÃªu cáº§u hiá»ƒn thá»‹ Interstitial (CÃ³ check capping time vÃ  level)...");
                
                AdsManager.Instance.ShowInterstitial(
                    where: "end_game_revive",
                    currentLevel: _currentLevel,
                    onSuccess: () => LogStatus("Interstitial Ä‘Ã£ xem xong hoáº·c Ä‘Æ°á»£c Ä‘Ã³ng thÃ nh cÃ´ng!"),
                    onFail: () => LogStatus("Interstitial bá»‹ cháº·n (ChÆ°a Ä‘á»§ thá»i gian capping hoáº·c chÆ°a Ä‘áº¡t level yÃªu cáº§u)."),
                    onRqFail: () => LogStatus("KhÃ´ng cÃ³ máº¡ng quáº£ng cÃ¡o nÃ o sáºµn sÃ ng xá»­ lÃ½ Interstitial.")
                );
            }

            if (GUILayout.Button("Force Show Interstitial (Ã‰p hiá»ƒn thá»‹)", GUILayout.Height(30)))
            {
                LogStatus("YÃªu cáº§u Ã©p hiá»ƒn thá»‹ Interstitial (Bá» qua Ä‘iá»u kiá»‡n kiá»ƒm tra)...");
                
                AdsManager.Instance.ShowInterWithoutCondition(
                    where: "forced_button",
                    currentLevel: _currentLevel,
                    onSuccess: () => LogStatus("Ã‰p hiá»ƒn thá»‹ Interstitial thÃ nh cÃ´ng!"),
                    onFail: () => LogStatus("Hiá»ƒn thá»‹ tháº¥t báº¡i (Quáº£ng cÃ¡o chÆ°a ká»‹p táº£i hoáº·c lá»—i network)."),
                    onRqFail: () => LogStatus("Máº¡ng quáº£ng cÃ¡o bÃ¡o lá»—i há»‡ thá»‘ng.")
                );
            }

            GUILayout.Space(10);

            // =================================================================
            // SECTION 4: REWARDED ADS
            // =================================================================
            GUILayout.Box("4. Quáº£ng cÃ¡o Nháº­n QuÃ  (Rewarded)", GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Show Rewarded Video", GUILayout.Height(35)))
            {
                LogStatus("Äang má»Ÿ quáº£ng cÃ¡o video nháº­n thÆ°á»Ÿng...");
                
                // Máº¹o: Báº¡n nÃªn táº¡m thá»i táº¯t Ã¢m thanh cá»§a game táº¡i Ä‘Ã¢y trÆ°á»›c khi gá»i ad
                AdsManager.Instance.ShowRewardedVideo(
                    where: "claim_double_gold",
                    currentLevel: _currentLevel,
                    onSuccess: () => {
                        LogStatus("ThÃ nh cÃ´ng! NgÆ°á»i chÆ¡i Ä‘Ã£ xem háº¿t video. Táº·ng quÃ : +100 Gold!");
                        // Thá»±c hiá»‡n cá»™ng tiá»n vÃ ng/váº­t pháº©m thá»±c táº¿ á»Ÿ Ä‘Ã¢y
                    },
                    onFail: () => {
                        LogStatus("Tháº¥t báº¡i! NgÆ°á»i chÆ¡i táº¯t quáº£ng cÃ¡o giá»¯a chá»«ng hoáº·c lá»—i hiá»ƒn thá»‹.");
                    },
                    onRqFail: () => {
                        LogStatus("Video nháº­n quÃ  chÆ°a sáºµn sÃ ng hoáº·c khÃ´ng tÃ¬m tháº¥y video kháº£ dá»¥ng.");
                    }
                );
            }

            GUILayout.Space(10);

            // =================================================================
            // SECTION 5: APP OPEN ADS
            // =================================================================
            GUILayout.Box("5. Quáº£ng cÃ¡o Má»Ÿ App (App Open)", GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Show App Open Ad", GUILayout.Height(35)))
            {
                LogStatus("YÃªu cáº§u hiá»ƒn thá»‹ App Open Ad...");
                
                AdsManager.Instance.ShowAppOpenAds(
                    where: "resume_app",
                    onSuccess: () => LogStatus("ÄÃ£ Ä‘Ã³ng App Open Ad -> Tiáº¿p tá»¥c game."),
                    onFail: () => LogStatus("Hiá»ƒn thá»‹ App Open Ad lá»—i hoáº·c háº¿t háº¡n (4 tiáº¿ng)."),
                    onRqFail: () => LogStatus("App Open Ad chÆ°a Ä‘Æ°á»£c táº£i xong.")
                );
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void LogStatus(string text)
        {
            _statusLog = $"[{DateTime.Now:HH:mm:ss}] {text}";
            Debug.Log($"[AdsExample] {text}");
        }
    }
}