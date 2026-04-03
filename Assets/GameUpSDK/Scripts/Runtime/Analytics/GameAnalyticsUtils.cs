using System;
using System.Collections.Generic;
using UnityEngine;
#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
using GameAnalyticsSDK;
#endif

namespace GameUp.SDK
{
    /// <summary>
    /// Gọi trực tiếp API runtime GameAnalytics (assembly <c>GameAnalyticsSDK</c>).
    /// Progression: <see href="https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/event-tracking">GA Unity — Progression events</see>.
    /// </summary>
    internal static class GameAnalyticsUtils
    {
#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
        private static GAProgressionStatus ToGa(GaProgressionStatus status) => (GAProgressionStatus)(int)status;

        private static Dictionary<string, object> StringFieldsToObjectDict(Dictionary<string, string> stringFields)
        {
            if (stringFields == null || stringFields.Count == 0) return null;
            var objFields = new Dictionary<string, object>();
            foreach (var kv in stringFields)
            {
                if (kv.Value != null) objFields[kv.Key] = kv.Value;
            }

            return objFields.Count > 0 ? objFields : null;
        }
#endif

        /// <summary>
        /// Gửi progression theo hierarchy GA: progression01 (+ optional 02, 03) và optional score (Complete/Fail).
        /// Custom fields (index, time, …) chỉ có trong raw export theo tài liệu GA.
        /// </summary>
        public static void LogProgression(
            GaProgressionStatus status,
            string progression01,
            string progression02 = null,
            string progression03 = null,
            int? score = null,
            Dictionary<string, string> stringFields = null)
        {
#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
            if (string.IsNullOrEmpty(progression01)) return;

            try
            {
                var st = ToGa(status);
                var fields = StringFieldsToObjectDict(stringFields);
                var has02 = !string.IsNullOrEmpty(progression02);
                var has03 = !string.IsNullOrEmpty(progression03);

                if (has03)
                {
                    if (score.HasValue && fields != null)
                        GameAnalytics.NewProgressionEvent(st, progression01, progression02, progression03, score.Value, fields, false);
                    else if (score.HasValue)
                        GameAnalytics.NewProgressionEvent(st, progression01, progression02, progression03, score.Value);
                    else if (fields != null)
                        GameAnalytics.NewProgressionEvent(st, progression01, progression02, progression03, fields, false);
                    else
                        GameAnalytics.NewProgressionEvent(st, progression01, progression02, progression03);
                    return;
                }

                if (has02)
                {
                    if (score.HasValue && fields != null)
                        GameAnalytics.NewProgressionEvent(st, progression01, progression02, score.Value, fields, false);
                    else if (score.HasValue)
                        GameAnalytics.NewProgressionEvent(st, progression01, progression02, score.Value);
                    else if (fields != null)
                        GameAnalytics.NewProgressionEvent(st, progression01, progression02, fields, false);
                    else
                        GameAnalytics.NewProgressionEvent(st, progression01, progression02);
                    return;
                }

                GameAnalytics.NewProgressionEvent(st, progression01);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameUpAnalytics] GameAnalytics LogProgression failed: " + e.Message);
            }
#endif
        }

        /// <summary>Design event (tương thích; level/wave dùng <see cref="LogProgression"/>).</summary>
        public static void LogDesign(string eventPath, float value, Dictionary<string, string> stringFields)
        {
#if GAMEANALYTICS_DEPENDENCIES_INSTALLED
            if (string.IsNullOrEmpty(eventPath)) return;

            try
            {
                var objFields = StringFieldsToObjectDict(stringFields);
                if (objFields != null)
                    GameAnalytics.NewDesignEvent(eventPath, value, objFields, false);
                else
                    GameAnalytics.NewDesignEvent(eventPath, value);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameUpAnalytics] GameAnalytics LogDesign failed: " + e.Message);
            }
#endif
        }
    }
}
