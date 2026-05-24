using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal static partial class CampusNpcEcologyPresetCatalog
    {
        private static CampusFacilityType[] ParseFacilityTypes(string[] values)
        {
            List<CampusFacilityType> result = new List<CampusFacilityType>();
            if (values == null)
            {
                return result.ToArray();
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (TryParseFacilityType(values[i], out CampusFacilityType facilityType))
                {
                    result.Add(facilityType);
                }
            }

            return result.ToArray();
        }

        private static CampusTimeSegment[] ParseSegments(string[] values)
        {
            List<CampusTimeSegment> result = new List<CampusTimeSegment>();
            if (values == null)
            {
                return result.ToArray();
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (Enum.TryParse(values[i], true, out CampusTimeSegment segment))
                {
                    result.Add(segment);
                }
            }

            return result.ToArray();
        }

        private static CampusNpcEcologyScheduleWindow[] ParseScheduleWindows(string[] values)
        {
            List<CampusNpcEcologyScheduleWindow> result = new List<CampusNpcEcologyScheduleWindow>();
            if (values == null)
            {
                return result.ToArray();
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (Enum.TryParse(values[i], true, out CampusNpcEcologyScheduleWindow scheduleWindow))
                {
                    result.Add(scheduleWindow);
                }
            }

            return result.ToArray();
        }

        private static string[] ParseRequirementIds(string[] values)
        {
            return NormalizeIds(values);
        }

        private static CampusTeacherDuty ParseTeacherDutyMask(string[] values)
        {
            CampusTeacherDuty mask = CampusTeacherDuty.None;
            if (values == null)
            {
                return mask;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (Enum.TryParse(values[i], true, out CampusTeacherDuty duty))
                {
                    mask |= duty;
                }
            }

            return mask;
        }

        private static CampusStaffDuty ParseStaffDutyMask(string[] values)
        {
            CampusStaffDuty mask = CampusStaffDuty.None;
            if (values == null)
            {
                return mask;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (Enum.TryParse(values[i], true, out CampusStaffDuty duty))
                {
                    mask |= duty;
                }
            }

            return mask;
        }

        private static CampusCharacterTrait[] ParseTraits(string[] values)
        {
            List<CampusCharacterTrait> result = new List<CampusCharacterTrait>();
            if (values == null)
            {
                return result.ToArray();
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (Enum.TryParse(values[i], true, out CampusCharacterTrait trait))
                {
                    result.Add(trait);
                }
            }

            return result.ToArray();
        }

        private static CampusRoomType ParseRoomType(string value)
        {
            return Enum.TryParse(value, true, out CampusRoomType roomType)
                ? roomType
                : CampusRoomType.Unknown;
        }

        private static bool TryParseRole(string value, out CampusCharacterRole role)
        {
            return Enum.TryParse(value, true, out role);
        }

        private static bool TryParseTargetKind(string value, out CampusNpcEcologyTargetKind targetKind)
        {
            return Enum.TryParse(value, true, out targetKind);
        }

        private static bool TryParseIntentKind(string value, out CampusNpcIntentKind intentKind)
        {
            return Enum.TryParse(value, true, out intentKind);
        }

        private static bool TryParseActionMode(string value, out CampusNpcEcologyActionMode actionMode)
        {
            return Enum.TryParse(value, true, out actionMode);
        }

        private static bool TryParseFacilityType(string value, out CampusFacilityType facilityType)
        {
            return Enum.TryParse(value, true, out facilityType);
        }

        private static bool TryParseClockMinute(string value, out int minuteOfDay)
        {
            minuteOfDay = 0;
            string normalized = NormalizeId(value);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            string[] parts = normalized.Split(':');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out int hour) ||
                !int.TryParse(parts[1], out int minute))
            {
                return false;
            }

            hour = Mathf.Clamp(hour, 0, 23);
            minute = Mathf.Clamp(minute, 0, 59);
            minuteOfDay = hour * 60 + minute;
            return true;
        }

        private static string[] NormalizeIds(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<string>();
            }

            List<string> result = new List<string>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                string normalized = NormalizeId(values[i]);
                if (!string.IsNullOrEmpty(normalized))
                {
                    result.Add(normalized);
                }
            }

            return result.ToArray();
        }

        private static bool ContainsId(string[] values, string id)
        {
            string normalizedId = NormalizeId(id);
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
