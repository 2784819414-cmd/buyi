using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Delivery
{
    [Serializable]
    internal sealed class CampusDeliveryOrderWindow
    {
        public string MealId = string.Empty;
        public string StartClock = string.Empty;
        public string EndClock = string.Empty;
        public string[] ActiveScheduleSegments = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class CampusDeliveryRules
    {
        public string ItemDefinitionId = string.Empty;
        public string SourceLocationId = string.Empty;
        public string SourceContainerPrefix = string.Empty;
        public string DeliverySpotObjectId = string.Empty;
        public string DeliverySpotInteractionPresetEid = string.Empty;
        public int OrderChancePercent;
        public int MinDeliveryMinutes;
        public int MaxDeliveryMinutes;
        public List<CampusDeliveryOrderWindow> OrderWindows =
            new List<CampusDeliveryOrderWindow>();

        private readonly List<CampusDeliveryOrderWindowRecord> normalizedOrderWindows =
            new List<CampusDeliveryOrderWindowRecord>();

        public IReadOnlyList<CampusDeliveryOrderWindowRecord> NormalizedOrderWindows => normalizedOrderWindows;

        public void Normalize()
        {
            ItemDefinitionId = Clean(ItemDefinitionId);
            SourceLocationId = Clean(SourceLocationId);
            SourceContainerPrefix = Clean(SourceContainerPrefix);
            DeliverySpotObjectId = Clean(DeliverySpotObjectId);
            DeliverySpotInteractionPresetEid = Clean(DeliverySpotInteractionPresetEid);
            OrderChancePercent = Mathf.Clamp(OrderChancePercent, 0, 100);
            MinDeliveryMinutes = Mathf.Clamp(MinDeliveryMinutes, 1, 24 * 60);
            MaxDeliveryMinutes = Mathf.Clamp(MaxDeliveryMinutes, MinDeliveryMinutes, 24 * 60);
            NormalizeOrderWindows();
        }

        private void NormalizeOrderWindows()
        {
            normalizedOrderWindows.Clear();
            if (OrderWindows == null)
            {
                return;
            }

            for (int i = 0; i < OrderWindows.Count; i++)
            {
                CampusDeliveryOrderWindow window = OrderWindows[i];
                if (window == null ||
                    !CampusDeliveryPresetCatalog.TryParseMealId(window.MealId, out string mealId) ||
                    !CampusDeliveryPresetCatalog.TryParseClockMinute(window.StartClock, out int startMinute) ||
                    !CampusDeliveryPresetCatalog.TryParseClockMinute(window.EndClock, out int endMinute) ||
                    !CampusDeliveryPresetCatalog.TryParseSegments(window.ActiveScheduleSegments, out CampusTimeSegment[] activeSegments))
                {
                    continue;
                }

                normalizedOrderWindows.Add(new CampusDeliveryOrderWindowRecord(
                    mealId,
                    startMinute,
                    endMinute,
                    activeSegments));
            }
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    internal readonly struct CampusDeliveryOrderWindowRecord
    {
        public CampusDeliveryOrderWindowRecord(
            string mealId,
            int startMinute,
            int endMinute,
            CampusTimeSegment[] activeSegments)
        {
            MealId = Clean(mealId);
            StartMinute = startMinute;
            EndMinute = endMinute;
            ActiveSegments = activeSegments ?? Array.Empty<CampusTimeSegment>();
        }

        public string MealId { get; }
        public int StartMinute { get; }
        public int EndMinute { get; }
        public CampusTimeSegment[] ActiveSegments { get; }

        public bool ContainsOrderMinute(int minute)
        {
            return IsWithinRange(CampusTimeSchedule.NormalizeMinuteOfDay(minute), StartMinute, EndMinute);
        }

        public bool ContainsActiveSegment(CampusTimeSegment segment)
        {
            for (int i = 0; i < ActiveSegments.Length; i++)
            {
                if (ActiveSegments[i] == segment)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWithinRange(int minute, int start, int end)
        {
            return start <= end
                ? minute >= start && minute < end
                : minute >= start || minute < end;
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    internal static class CampusDeliveryPresetCatalog
    {
        private const string PresetFileName = "DeliveryPresets.json";
        private static CampusDeliveryRules cachedRules;

        public static CampusDeliveryRules Rules
        {
            get
            {
                if (cachedRules == null)
                {
                    cachedRules = LoadRules();
                }

                return cachedRules;
            }
        }

        public static string ResolveUpcomingMeal(int currentMinute)
        {
            IReadOnlyList<CampusDeliveryOrderWindowRecord> windows = Rules.NormalizedOrderWindows;
            for (int i = 0; i < windows.Count; i++)
            {
                CampusDeliveryOrderWindowRecord window = windows[i];
                if (window.ContainsOrderMinute(currentMinute))
                {
                    return window.MealId;
                }
            }

            return string.Empty;
        }

        public static string ResolveActiveMeal(CampusTimeSegment segment)
        {
            IReadOnlyList<CampusDeliveryOrderWindowRecord> windows = Rules.NormalizedOrderWindows;
            for (int i = 0; i < windows.Count; i++)
            {
                CampusDeliveryOrderWindowRecord window = windows[i];
                if (window.ContainsActiveSegment(segment))
                {
                    return window.MealId;
                }
            }

            return string.Empty;
        }

        private static CampusDeliveryRules LoadRules()
        {
            CampusDeliveryRules rules = new CampusDeliveryRules();
            if (CampusRuntimeModPresetStore.TryReadJson(PresetFileName, out string json))
            {
                try
                {
                    CampusDeliveryRules loaded = JsonUtility.FromJson<CampusDeliveryRules>(json);
                    if (loaded != null)
                    {
                        rules = loaded;
                    }
                }
                catch (ArgumentException)
                {
                    rules = new CampusDeliveryRules();
                }
            }

            rules.Normalize();
            return rules;
        }

        internal static bool TryParseMealId(string value, out string mealId)
        {
            mealId = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            return !string.IsNullOrEmpty(mealId);
        }

        internal static bool TryParseClockMinute(string value, out int minute)
        {
            minute = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] parts = value.Trim().Split(':');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out int hour) ||
                !int.TryParse(parts[1], out int clockMinute) ||
                hour < 0 ||
                hour > 23 ||
                clockMinute < 0 ||
                clockMinute > 59)
            {
                return false;
            }

            minute = hour * 60 + clockMinute;
            return true;
        }

        internal static bool TryParseSegments(string[] values, out CampusTimeSegment[] segments)
        {
            segments = Array.Empty<CampusTimeSegment>();
            if (values == null || values.Length == 0)
            {
                return false;
            }

            List<CampusTimeSegment> parsed = new List<CampusTimeSegment>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                if (Enum.TryParse(values[i], true, out CampusTimeSegment segment))
                {
                    parsed.Add(segment);
                }
            }

            if (parsed.Count == 0)
            {
                return false;
            }

            segments = parsed.ToArray();
            return true;
        }
    }
}
