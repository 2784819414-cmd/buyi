using System;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    public static class CampusTimeSchedule
    {
        private const int MinutesPerDay = 24 * 60;

        private static readonly SegmentInfo[] SegmentInfos =
        {
            new(CampusTimeSegment.WakeUp, "起床 / 洗漱", "Wake Up / Wash", 6, 30, 7, 0),
            new(CampusTimeSegment.BreakfastPrep, "早饭 / 到教室", "Breakfast / Head to Class", 7, 0, 7, 30),
            new(CampusTimeSegment.MorningReading, "早读", "Morning Reading", 7, 30, 7, 55),
            new(CampusTimeSegment.MorningClass1, "上午第一节课", "Morning Class 1", 8, 0, 8, 40),
            new(CampusTimeSegment.MorningBreak1, "课间", "Break", 8, 40, 8, 50),
            new(CampusTimeSegment.MorningClass2, "上午第二节课", "Morning Class 2", 8, 50, 9, 30),
            new(CampusTimeSegment.MorningExerciseBreak, "大课间 / 课间操", "Long Break / Exercises", 9, 30, 10, 0),
            new(CampusTimeSegment.MorningClass3, "上午第三节课", "Morning Class 3", 10, 0, 10, 40),
            new(CampusTimeSegment.MorningBreak2, "课间", "Break", 10, 40, 10, 50),
            new(CampusTimeSegment.MorningClass4, "上午第四节课", "Morning Class 4", 10, 50, 11, 30),
            new(CampusTimeSegment.LunchBreak, "午饭 / 午休", "Lunch / Noon Rest", 11, 30, 14, 0),
            new(CampusTimeSegment.AfternoonClass1, "下午第一节课", "Afternoon Class 1", 14, 0, 14, 40),
            new(CampusTimeSegment.AfternoonBreak1, "课间", "Break", 14, 40, 14, 50),
            new(CampusTimeSegment.AfternoonClass2, "下午第二节课", "Afternoon Class 2", 14, 50, 15, 30),
            new(CampusTimeSegment.AfternoonBreak2, "课间", "Break", 15, 30, 15, 45),
            new(CampusTimeSegment.AfternoonClass3, "下午第三节课", "Afternoon Class 3", 15, 45, 16, 25),
            new(CampusTimeSegment.AfternoonBreak3, "课间", "Break", 16, 25, 16, 35),
            new(CampusTimeSegment.AfternoonClass4, "下午第四节课", "Afternoon Class 4", 16, 35, 17, 15),
            new(CampusTimeSegment.DinnerBreak, "晚饭 / 自由活动", "Dinner / Free Time", 17, 15, 18, 40),
            new(CampusTimeSegment.EveningStudy1, "晚自习第一段", "Evening Study 1", 18, 40, 20, 10),
            new(CampusTimeSegment.EveningBreak1, "晚自习课间", "Evening Study Break", 20, 10, 20, 25),
            new(CampusTimeSegment.EveningStudy2, "晚自习第二段", "Evening Study 2", 20, 25, 21, 55),
            new(CampusTimeSegment.EveningBreak2, "晚自习课间", "Evening Study Break", 21, 55, 22, 10),
            new(CampusTimeSegment.EveningStudy3, "晚自习第三段", "Evening Study 3", 22, 10, 22, 55),
            new(CampusTimeSegment.DormReturn, "回宿舍", "Return to Dorm", 22, 55, 23, 10),
            new(CampusTimeSegment.DormCheck, "查寝", "Dorm Check", 23, 10, 23, 20),
            new(CampusTimeSegment.LightsOut, "熄灯", "Lights Out", 23, 20, 23, 40),
            new(CampusTimeSegment.NightFree, "夜间自由行动", "Night Free Time", 23, 40, 6, 0),
            new(CampusTimeSegment.PreWakeSettlement, "凌晨结算", "Pre-Wake Settlement", 6, 0, 6, 30)
        };

        public static string GetChineseName(CampusTimeSegment segment)
        {
            return GetInfo(segment).ChineseName;
        }

        public static string GetDisplayName(CampusTimeSegment segment)
        {
            return GetDisplayName(CampusLanguageState.CurrentLanguage, segment);
        }

        public static string GetDisplayName(CampusDisplayLanguage language, CampusTimeSegment segment)
        {
            SegmentInfo info = GetInfo(segment);
            return Resolve(language, info.ChineseName, info.EnglishName);
        }

        public static string GetTimeLabel(CampusTimeSegment segment)
        {
            SegmentInfo info = GetInfo(segment);
            return FormatClockMinute(info.StartMinute) + " - " + FormatClockMinute(info.EndMinute);
        }

        public static float GetDurationMinutes(CampusTimeSegment segment)
        {
            SegmentInfo info = GetInfo(segment);
            int duration = info.EndMinute - info.StartMinute;
            if (duration <= 0)
            {
                duration += MinutesPerDay;
            }

            return duration;
        }

        public static int GetStartMinute(CampusTimeSegment segment)
        {
            return GetInfo(segment).StartMinute;
        }

        public static int GetEndMinute(CampusTimeSegment segment)
        {
            return GetInfo(segment).EndMinute;
        }

        public static CampusTimeSegment GetNextSegment(CampusTimeSegment segment)
        {
            return segment == CampusTimeSegment.PreWakeSettlement
                ? CampusTimeSegment.WakeUp
                : (CampusTimeSegment)((int)segment + 1);
        }

        public static string GetClockText(CampusTimeSegment segment, float elapsedMinutes)
        {
            int elapsed = Mathf.Clamp(Mathf.FloorToInt(elapsedMinutes), 0, Mathf.FloorToInt(GetDurationMinutes(segment)));
            int clockMinute = NormalizeMinuteOfDay(GetStartMinute(segment) + elapsed);
            return FormatClockMinute(clockMinute);
        }

        public static string FormatClockMinute(int minuteOfDay)
        {
            int normalizedMinute = NormalizeMinuteOfDay(minuteOfDay);
            int hour = normalizedMinute / 60;
            int minute = normalizedMinute % 60;
            return hour.ToString("00") + ":" + minute.ToString("00");
        }

        public static int NormalizeMinuteOfDay(int minuteOfDay)
        {
            return ((minuteOfDay % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;
        }

        public static bool TryResolveSegmentAtMinute(
            int minuteOfDay,
            out CampusTimeSegment segment,
            out float elapsedMinutes)
        {
            int normalizedMinute = NormalizeMinuteOfDay(minuteOfDay);
            for (int i = 0; i < SegmentInfos.Length; i++)
            {
                SegmentInfo info = SegmentInfos[i];
                if (!ContainsMinute(info, normalizedMinute))
                {
                    continue;
                }

                segment = info.Segment;
                elapsedMinutes = ResolveElapsedMinutes(info, normalizedMinute);
                return true;
            }

            segment = CampusTimeSegment.WakeUp;
            elapsedMinutes = 0f;
            return false;
        }

        public static string GetSegmentLogMessage(CampusTimeSegment segment, string dateText)
        {
            return GetSegmentLogMessage(CampusLanguageState.CurrentLanguage, segment, dateText);
        }

        public static string GetSegmentLogMessage(CampusDisplayLanguage language, CampusTimeSegment segment, string dateText)
        {
            return segment switch
            {
                CampusTimeSegment.WakeUp => Resolve(language, "[系统] " + dateText + "，起床铃响了。", "[System] " + dateText + ", the wake-up bell rings."),
                CampusTimeSegment.BreakfastPrep => Resolve(language, "[系统] " + dateText + "，早餐和到教室时间开始。", "[System] " + dateText + ", breakfast and classroom prep begin."),
                CampusTimeSegment.MorningReading => Resolve(language, "[课堂] " + dateText + "，早读开始。", "[Classroom] " + dateText + ", morning reading begins."),
                CampusTimeSegment.MorningClass1 => Resolve(language, "[课堂] " + dateText + "，上午第一节课开始。", "[Classroom] " + dateText + ", morning class 1 begins."),
                CampusTimeSegment.MorningBreak1 => Resolve(language, "[课堂] " + dateText + "，上午第一节课下课，课间开始。", "[Classroom] " + dateText + ", morning class 1 ends and break begins."),
                CampusTimeSegment.MorningClass2 => Resolve(language, "[课堂] " + dateText + "，上午第二节课开始。", "[Classroom] " + dateText + ", morning class 2 begins."),
                CampusTimeSegment.MorningExerciseBreak => Resolve(language, "[系统] " + dateText + "，大课间和课间操开始。", "[System] " + dateText + ", long break and exercises begin."),
                CampusTimeSegment.MorningClass3 => Resolve(language, "[课堂] " + dateText + "，上午第三节课开始。", "[Classroom] " + dateText + ", morning class 3 begins."),
                CampusTimeSegment.MorningBreak2 => Resolve(language, "[课堂] " + dateText + "，课间开始。", "[Classroom] " + dateText + ", break begins."),
                CampusTimeSegment.MorningClass4 => Resolve(language, "[课堂] " + dateText + "，上午第四节课开始。", "[Classroom] " + dateText + ", morning class 4 begins."),
                CampusTimeSegment.LunchBreak => Resolve(language, "[系统] " + dateText + "，午饭和午休开始。", "[System] " + dateText + ", lunch and noon rest begin."),
                CampusTimeSegment.AfternoonClass1 => Resolve(language, "[课堂] " + dateText + "，下午第一节课开始。", "[Classroom] " + dateText + ", afternoon class 1 begins."),
                CampusTimeSegment.AfternoonBreak1 => Resolve(language, "[课堂] " + dateText + "，课间开始。", "[Classroom] " + dateText + ", break begins."),
                CampusTimeSegment.AfternoonClass2 => Resolve(language, "[课堂] " + dateText + "，下午第二节课开始。", "[Classroom] " + dateText + ", afternoon class 2 begins."),
                CampusTimeSegment.AfternoonBreak2 => Resolve(language, "[课堂] " + dateText + "，课间开始。", "[Classroom] " + dateText + ", break begins."),
                CampusTimeSegment.AfternoonClass3 => Resolve(language, "[课堂] " + dateText + "，下午第三节课开始。", "[Classroom] " + dateText + ", afternoon class 3 begins."),
                CampusTimeSegment.AfternoonBreak3 => Resolve(language, "[课堂] " + dateText + "，课间开始。", "[Classroom] " + dateText + ", break begins."),
                CampusTimeSegment.AfternoonClass4 => Resolve(language, "[课堂] " + dateText + "，下午第四节课开始。", "[Classroom] " + dateText + ", afternoon class 4 begins."),
                CampusTimeSegment.DinnerBreak => Resolve(language, "[系统] " + dateText + "，晚饭和自由活动开始。", "[System] " + dateText + ", dinner and free time begin."),
                CampusTimeSegment.EveningStudy1 => Resolve(language, "[课堂] " + dateText + "，晚自习第一段开始。", "[Classroom] " + dateText + ", evening study 1 begins."),
                CampusTimeSegment.EveningBreak1 => Resolve(language, "[课堂] " + dateText + "，晚自习课间开始。", "[Classroom] " + dateText + ", evening study break begins."),
                CampusTimeSegment.EveningStudy2 => Resolve(language, "[课堂] " + dateText + "，晚自习第二段开始。", "[Classroom] " + dateText + ", evening study 2 begins."),
                CampusTimeSegment.EveningBreak2 => Resolve(language, "[课堂] " + dateText + "，晚自习课间开始。", "[Classroom] " + dateText + ", evening study break begins."),
                CampusTimeSegment.EveningStudy3 => Resolve(language, "[课堂] " + dateText + "，晚自习第三段开始。", "[Classroom] " + dateText + ", evening study 3 begins."),
                CampusTimeSegment.DormReturn => Resolve(language, "[系统] " + dateText + "，晚自习结束，学生回宿舍。", "[System] " + dateText + ", evening study ends and students return to dorms."),
                CampusTimeSegment.DormCheck => Resolve(language, "[系统] " + dateText + "，查寝开始。", "[System] " + dateText + ", dorm check begins."),
                CampusTimeSegment.LightsOut => Resolve(language, "[系统] " + dateText + "，熄灯了。", "[System] " + dateText + ", lights are out."),
                CampusTimeSegment.NightFree => Resolve(language, "[系统] " + dateText + "，夜间自由行动开始。", "[System] " + dateText + ", night free time begins."),
                CampusTimeSegment.PreWakeSettlement => Resolve(language, "[系统] " + dateText + "，凌晨结算开始。", "[System] " + dateText + ", pre-wake settlement begins."),
                _ => Resolve(language, "[系统] " + dateText + "，" + GetChineseName(segment) + "开始。", "[System] " + dateText + ", " + GetDisplayName(CampusDisplayLanguage.English, segment) + " begins.")
            };
        }

        private static SegmentInfo GetInfo(CampusTimeSegment segment)
        {
            int index = (int)segment;
            if (index >= 0 && index < SegmentInfos.Length && SegmentInfos[index].Segment == segment)
            {
                return SegmentInfos[index];
            }

            for (int i = 0; i < SegmentInfos.Length; i++)
            {
                if (SegmentInfos[i].Segment == segment)
                {
                    return SegmentInfos[i];
                }
            }

            throw new ArgumentOutOfRangeException(nameof(segment), segment, "Unknown campus time segment.");
        }

        private static bool ContainsMinute(SegmentInfo info, int minuteOfDay)
        {
            if (info.StartMinute == info.EndMinute)
            {
                return true;
            }

            return info.StartMinute < info.EndMinute
                ? minuteOfDay >= info.StartMinute && minuteOfDay < info.EndMinute
                : minuteOfDay >= info.StartMinute || minuteOfDay < info.EndMinute;
        }

        private static float ResolveElapsedMinutes(SegmentInfo info, int minuteOfDay)
        {
            int elapsed = minuteOfDay - info.StartMinute;
            if (elapsed < 0)
            {
                elapsed += MinutesPerDay;
            }

            return elapsed;
        }

        private static string Resolve(CampusDisplayLanguage language, string chinese, string english)
        {
            return language switch
            {
                CampusDisplayLanguage.English => english,
                CampusDisplayLanguage.Bilingual => chinese + " / " + english,
                _ => chinese
            };
        }

        private readonly struct SegmentInfo
        {
            public SegmentInfo(CampusTimeSegment segment, string chineseName, string englishName, int startHour, int startMinute, int endHour, int endMinute)
            {
                Segment = segment;
                ChineseName = chineseName;
                EnglishName = englishName;
                StartMinute = startHour * 60 + startMinute;
                EndMinute = endHour * 60 + endMinute;
            }

            public CampusTimeSegment Segment { get; }
            public string ChineseName { get; }
            public string EnglishName { get; }
            public int StartMinute { get; }
            public int EndMinute { get; }
        }
    }
}
