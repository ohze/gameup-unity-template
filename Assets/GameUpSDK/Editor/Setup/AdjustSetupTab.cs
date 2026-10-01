using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    // ==========================================
    // ADJUST SETUP TAB -> GameUpSdkConfig.adjust
    // ==========================================
    public class AdjustSetupTab : SdkConfigTabBase
    {
        /// <summary>Event MMP mà GameUpAnalytics / AdsTracker gửi — nút "Thêm event mặc định" điền sẵn để dev chỉ việc dán token.</summary>
        private static readonly string[] DefaultEventNames =
        {
            AnalyticsEvent.AfLevelAchieved,
            AnalyticsEvent.AfPurchase,
            AnalyticsEvent.AfTutorialCompletion,
            AnalyticsEvent.AfAchievementUnlocked,
            AnalyticsEvent.AfCompleteRegistration,
            AdsEvent.AfInterShow,
            AdsEvent.AfInterDisplayed,
            AdsEvent.AfRewardShow,
            AdsEvent.AfRewardDisplayed,
        };

        public override string Title => "Adjust";

#if GAMEUP_MMP_ADJUST || ADJUST_DEPENDENCIES_INSTALLED
        public override bool IsVisible => true;
#else
        public override bool IsVisible => false;
#endif

        protected override void DrawSection(SerializedObject so)
        {
            var adjust = so.FindProperty("adjust");

#if !ADJUST_DEPENDENCIES_INSTALLED
            EditorGUILayout.HelpBox(
                "Chưa cài Adjust SDK. Mở GameUp → SDK → Setup Dependencies, chọn MMP = Adjust rồi cài.",
                MessageType.Warning);
#endif

            EditorGUILayout.LabelField("Adjust Configuration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(adjust.FindPropertyRelative("appTokenAndroid"), new GUIContent("App Token (Android)"));
            EditorGUILayout.PropertyField(adjust.FindPropertyRelative("appTokenIOS"), new GUIContent("App Token (iOS)"));

            var sandbox = adjust.FindPropertyRelative("sandbox");
            EditorGUILayout.PropertyField(sandbox, new GUIContent("Sandbox Environment"));
            if (sandbox.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Đang ở Sandbox — install/doanh thu từ build release sẽ KHÔNG được tính. Tắt trước khi build lên store.",
                    MessageType.Warning);
            }

            EditorGUILayout.PropertyField(adjust.FindPropertyRelative("verboseLog"), new GUIContent("Verbose Log"));
            EditorGUILayout.PropertyField(adjust.FindPropertyRelative("sendInBackground"), new GUIContent("Send In Background"));
            EditorGUILayout.PropertyField(adjust.FindPropertyRelative("costDataInAttribution"), new GUIContent("Cost Data In Attribution"));
            EditorGUILayout.PropertyField(adjust.FindPropertyRelative("attConsentWaitingInterval"), new GUIContent("ATT Waiting (giây, iOS)"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Event Tokens", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Adjust chỉ nhận event bằng token 6 ký tự tạo trên dashboard. Event GameUp nào chưa có token sẽ bị bỏ qua " +
                "(level, purchase, ads…). af_purchase gửi kèm doanh thu + order id để Adjust chống trùng.",
                MessageType.None);

            var eventTokens = adjust.FindPropertyRelative("eventTokens");
            if (GUILayout.Button("Thêm các event GameUp mặc định (còn thiếu)"))
                AddMissingDefaultEvents(eventTokens);
            EditorGUILayout.PropertyField(eventTokens, new GUIContent("Event Tokens"), true);

            EditorGUILayout.HelpBox(
                "GameUp tự init Adjust từ cấu hình này lúc load scene đầu (AdjustAnalyticsUtils) — không cần kéo prefab Adjust vào scene. " +
                "Ad revenue được gửi tự động, source theo mạng đã phục vụ impression (admob_sdk / applovin_max_sdk / ironsource_sdk).",
                MessageType.None);
        }

        private static void AddMissingDefaultEvents(SerializedProperty eventTokens)
        {
            foreach (var eventName in DefaultEventNames)
            {
                if (ContainsEvent(eventTokens, eventName)) continue;

                int index = eventTokens.arraySize;
                eventTokens.InsertArrayElementAtIndex(index);
                var entry = eventTokens.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("eventName").stringValue = eventName;
                entry.FindPropertyRelative("token").stringValue = "";
            }
        }

        private static bool ContainsEvent(SerializedProperty eventTokens, string eventName)
        {
            for (int i = 0; i < eventTokens.arraySize; i++)
            {
                if (eventTokens.GetArrayElementAtIndex(i).FindPropertyRelative("eventName").stringValue == eventName)
                    return true;
            }
            return false;
        }
    }
}
