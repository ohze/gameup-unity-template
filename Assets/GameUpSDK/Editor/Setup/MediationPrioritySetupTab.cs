using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameUp.SDK.Editor.Setup
{
    // ==========================================
    // MEDIATION PRIORITY TAB -> GameUpAdsConfig.mediationPriority
    // Thứ tự trong danh sách = thứ tự waterfall lúc runtime (xem AdsManager.GetAvailableProvider).
    // ==========================================
    public class MediationPrioritySetupTab : AdsConfigTabBase
    {
        public override string Title => "Thứ tự ưu tiên";

        protected override void DrawHeader()
        {
            // Bỏ platform selector của AdsConfigTabBase: thứ tự ưu tiên dùng chung cho mọi platform.
            EditorGUILayout.LabelField(Title, EditorStyles.boldLabel);
            EditorGUILayout.Space();
        }

        protected override void DrawSection(SerializedObject so)
        {
            var listProp = so.FindProperty("mediationPriority");
            if (listProp == null) return;

            SyncWithInstalledNetworks(listProp);

            if (listProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Chưa cài SDK mạng quảng cáo nào.", MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(
                "Mạng ở trên cùng được thử trước; nếu không có ad sẵn sàng, SDK tự rớt xuống mạng kế tiếp.\n" +
                "Danh sách chỉ hiện các mạng đã cài SDK trong project.",
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
        /// Ép danh sách khớp CHÍNH XÁC các mạng đã cài SDK: bỏ None/trùng/mạng đã gỡ SDK,
        /// thêm mạng vừa cài (define symbol mới xuất hiện) vào cuối. Giữ nguyên thứ tự
        /// tương đối người dùng đã sắp xếp cho các mạng còn lại.
        /// </summary>
        private static void SyncWithInstalledNetworks(SerializedProperty listProp)
        {
            var installed = GetInstalledProviders();
            var seen = new HashSet<MediationProvider>();
            var ordered = new List<MediationProvider>();

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var provider = (MediationProvider)listProp.GetArrayElementAtIndex(i).intValue;
                if (provider == MediationProvider.None) continue;
                if (!installed.Contains(provider)) continue;
                if (seen.Add(provider)) ordered.Add(provider);
            }

            foreach (var provider in installed)
            {
                if (seen.Add(provider)) ordered.Add(provider);
            }

            bool changed = listProp.arraySize != ordered.Count;
            if (!changed)
            {
                for (int i = 0; i < ordered.Count; i++)
                {
                    if ((MediationProvider)listProp.GetArrayElementAtIndex(i).intValue != ordered[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }
            if (!changed) return;

            listProp.arraySize = ordered.Count;
            for (int i = 0; i < ordered.Count; i++)
                listProp.GetArrayElementAtIndex(i).intValue = (int)ordered[i];
        }

        private static List<MediationProvider> GetInstalledProviders()
        {
            var list = new List<MediationProvider> { MediationProvider.Admob };
#if MAXSDK_DEPENDENCIES_INSTALLED
            list.Add(MediationProvider.Max);
#endif
#if LEVELPLAY_DEPENDENCIES_INSTALLED
            list.Add(MediationProvider.IronSource);
#endif
            return list;
        }
    }
}
