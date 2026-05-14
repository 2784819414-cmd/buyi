using System.Diagnostics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NtingCampusMapEditor
{
    public static class CampusMapEditorPerformance
    {
        private const string LogPrefix = "[CampusPerf]";

#if UNITY_EDITOR
        private const string EnabledKey = "NtingCampus.MapEditor.PerformanceLogging.Enabled";
        private const string ThresholdKey = "NtingCampus.MapEditor.PerformanceLogging.ThresholdMs";
#else
        private static bool runtimeEnabled;
        private static float runtimeThresholdMs = 50f;
#endif

        public static bool Enabled
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetBool(EnabledKey, false);
#else
                return runtimeEnabled;
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetBool(EnabledKey, value);
#else
                runtimeEnabled = value;
#endif
            }
        }

        public static float SlowOperationThresholdMs
        {
            get
            {
#if UNITY_EDITOR
                return Mathf.Max(0f, EditorPrefs.GetFloat(ThresholdKey, 50f));
#else
                return Mathf.Max(0f, runtimeThresholdMs);
#endif
            }
            set
            {
                float normalized = Mathf.Max(0f, value);
#if UNITY_EDITOR
                EditorPrefs.SetFloat(ThresholdKey, normalized);
#else
                runtimeThresholdMs = normalized;
#endif
            }
        }

        public static long Begin()
        {
            return Enabled ? Stopwatch.GetTimestamp() : 0L;
        }

        public static void End(long startTimestamp, string label)
        {
            if (startTimestamp == 0L || !Enabled)
            {
                return;
            }

            double elapsedMs = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
            if (elapsedMs >= SlowOperationThresholdMs)
            {
                UnityEngine.Debug.Log(string.Format("{0} {1} took {2:0.0} ms", LogPrefix, label, elapsedMs));
            }
        }

#if UNITY_EDITOR
        [MenuItem("Tools/Nting Campus/Performance Logging/Slow Operation Logs")]
        private static void ToggleSlowOperationLogs()
        {
            Enabled = !Enabled;
            UnityEngine.Debug.Log(string.Format(
                "{0} Slow operation logs {1}. Threshold: {2:0.#} ms.",
                LogPrefix,
                Enabled ? "enabled" : "disabled",
                SlowOperationThresholdMs));
        }

        [MenuItem("Tools/Nting Campus/Performance Logging/Slow Operation Logs", true)]
        private static bool ToggleSlowOperationLogsValidate()
        {
            Menu.SetChecked("Tools/Nting Campus/Performance Logging/Slow Operation Logs", Enabled);
            return true;
        }

        [MenuItem("Tools/Nting Campus/Performance Logging/Threshold/20 ms")]
        private static void SetThreshold20()
        {
            SetThreshold(20f);
        }

        [MenuItem("Tools/Nting Campus/Performance Logging/Threshold/50 ms")]
        private static void SetThreshold50()
        {
            SetThreshold(50f);
        }

        [MenuItem("Tools/Nting Campus/Performance Logging/Threshold/100 ms")]
        private static void SetThreshold100()
        {
            SetThreshold(100f);
        }

        private static void SetThreshold(float thresholdMs)
        {
            SlowOperationThresholdMs = thresholdMs;
            UnityEngine.Debug.Log(string.Format("{0} Slow operation threshold set to {1:0.#} ms.", LogPrefix, SlowOperationThresholdMs));
        }
#endif
    }
}
