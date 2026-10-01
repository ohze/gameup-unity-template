using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    // ==========================================
    // MEDIATION PRIORITY TAB -> GameUpAdsConfig.mediationPriority + cài đặt chung (appOpenOnColdStart, nativeCtaClickRate)
    // Thứ tự trong danh sách = thứ tự waterfall lúc runtime (xem AdsManager.GetAvailableProvider).
    // ==========================================
    public class MediationPrioritySetupTab : AdsConfigTabBase
    {
        public override string Title => Installer.GameUpDependenciesWindow.SetupPriorityTabTitle;

        protected override void DrawHeader()
        {
            // Bỏ platform selector của AdsConfigTabBase: thứ tự ưu tiên dùng chung cho mọi platform.
            EditorGUILayout.LabelField(Title, EditorStyles.boldLabel);
            EditorGUILayout.Space();
        }

        protected override void DrawSection(SerializedObject so)
        {
            NetworkEditorUI.DrawGeneralSection(so);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Thứ tự ưu tiên waterfall", EditorStyles.boldLabel);
            var listProp = so.FindProperty("mediationPriority");
            if (listProp == null) return;

            SyncWithInstalledNetworks(listProp);

            if (listProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "Chưa cài SDK mạng quảng cáo nào. Cài ở GameUp → SDK → Setup Dependencies (mặc định AdMob).",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                listProp.arraySize == 1
                    ? "Đang dùng một mạng quảng cáo. Muốn chạy song song MAX / LevelPlay / AdMob làm mạng dự phòng, " +
                      "cài thêm ở GameUp → SDK → Setup Dependencies (mục \"Mạng quảng cáo bổ sung\") — mạng mới tự xuất hiện ở đây."
                    : "Mạng ở trên cùng được thử trước; nếu không có ad sẵn sàng, SDK tự rớt xuống mạng kế tiếp.\n" +
                      "Danh sách chỉ hiện các mạng đã cài SDK. Mạng mới cài tự được thêm vào cuối.",
                MessageType.Info);
            GUILayout.Space(6);

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var element = listProp.GetArrayElementAtIndex(i);
                var provider = (MediationProvider)element.intValue;

                EditorGUILayout.BeginHorizontal("box");
                GUILayout.Label($"{i + 1}.", GUILayout.Width(20));
                GUILayout.Label(provider.ToString(), EditorStyles.boldLabel, GUILayout.Width(120));
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button("▲", GUILayout.Width(26))) listProp.MoveArrayElement(i, i - 1);
                }
                using (new EditorGUI.DisabledScope(i == listProp.arraySize - 1))
                {
                    if (GUILayout.Button("▼", GUILayout.Width(26))) listProp.MoveArrayElement(i, i + 1);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Ép danh sách khớp CHÍNH XÁC các mạng đã cài SDK theo cùng luật với runtime (<see cref="MediationPriority.Resolve"/>):
        /// bỏ None/trùng/mạng đã gỡ SDK, thêm mạng vừa cài vào cuối, giữ nguyên thứ tự người dùng đã sắp cho phần còn lại.
        /// </summary>
        private static void SyncWithInstalledNetworks(SerializedProperty listProp)
        {
            var saved = new List<MediationProvider>(listProp.arraySize);
            for (int i = 0; i < listProp.arraySize; i++)
                saved.Add((MediationProvider)listProp.GetArrayElementAtIndex(i).intValue);

            var ordered = MediationPriority.Resolve(saved);

            bool changed = saved.Count != ordered.Count;
            for (int i = 0; !changed && i < ordered.Count; i++)
                changed = saved[i] != ordered[i];
            if (!changed) return;

            listProp.arraySize = ordered.Count;
            for (int i = 0; i < ordered.Count; i++)
                listProp.GetArrayElementAtIndex(i).intValue = (int)ordered[i];
        }
    }
}
