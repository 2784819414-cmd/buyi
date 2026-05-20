using System;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    /// <summary>
    /// 中国普通高中 / 寄宿制高中节奏的时间表。
    /// </summary>
    public static class CampusTimeSchedule
    {
        private const int MinutesPerDay = 24 * 60;

        private static readonly SegmentInfo[] SegmentInfos =
        {
            new SegmentInfo(CampusTimeSegment.WakeUp, "起床 / 洗漱", "Wake Up / Wash", 6, 30, 7, 0),
            new SegmentInfo(CampusTimeSegment.BreakfastPrep, "早饭 / 到教室", "Breakfast / Head to Class", 7, 0, 7, 30),
            new SegmentInfo(CampusTimeSegment.MorningReading, "早读", "Morning Reading", 7, 30, 7, 55),
            new SegmentInfo(CampusTimeSegment.MorningClass1, "上午第一节课", "Morning Class 1", 8, 0, 8, 40),
            new SegmentInfo(CampusTimeSegment.MorningBreak1, "课间", "Break", 8, 40, 8, 50),
            new SegmentInfo(CampusTimeSegment.MorningClass2, "上午第二节课", "Morning Class 2", 8, 50, 9, 30),
            new SegmentInfo(CampusTimeSegment.MorningExerciseBreak, "大课间 / 课间操", "Long Break / Exercises", 9, 30, 10, 0),
            new SegmentInfo(CampusTimeSegment.MorningClass3, "上午第三节课", "Morning Class 3", 10, 0, 10, 40),
            new SegmentInfo(CampusTimeSegment.MorningBreak2, "课间", "Break", 10, 40, 10, 50),
            new SegmentInfo(CampusTimeSegment.MorningClass4, "上午第四节课", "Morning Class 4", 10, 50, 11, 30),
            new SegmentInfo(CampusTimeSegment.LunchBreak, "午饭 / 午休", "Lunch / Noon Rest", 11, 30, 14, 0),
            new SegmentInfo(CampusTimeSegment.AfternoonClass1, "下午第一节课", "Afternoon Class 1", 14, 0, 14, 40),
            new SegmentInfo(CampusTimeSegment.AfternoonBreak1, "课间", "Break", 14, 40, 14, 50),
            new SegmentInfo(CampusTimeSegment.AfternoonClass2, "下午第二节课", "Afternoon Class 2", 14, 50, 15, 30),
            new SegmentInfo(CampusTimeSegment.AfternoonBreak2, "课间", "Break", 15, 30, 15, 45),
            new SegmentInfo(CampusTimeSegment.AfternoonClass3, "下午第三节课", "Afternoon Class 3", 15, 45, 16, 25),
            new SegmentInfo(CampusTimeSegment.AfternoonBreak3, "课间", "Break", 16, 25, 16, 35),
            new SegmentInfo(CampusTimeSegment.AfternoonClass4, "下午第四节课", "Afternoon Class 4", 16, 35, 17, 15),
            new SegmentInfo(CampusTimeSegment.DinnerBreak, "晚饭 / 自由活动", "Dinner / Free Time", 17, 15, 18, 40),
            new SegmentInfo(CampusTimeSegment.EveningStudy1, "晚自习第一段", "Evening Study 1", 18, 40, 20, 10),
            new SegmentInfo(CampusTimeSegment.EveningBreak1, "晚自习课间", "Evening Study Break", 20, 10, 20, 25),
            new SegmentInfo(CampusTimeSegment.EveningStudy2, "晚自习第二段", "Evening Study 2", 20, 25, 21, 55),
            new SegmentInfo(CampusTimeSegment.EveningBreak2, "晚自习课间", "Evening Study Break", 21, 55, 22, 10),
            new SegmentInfo(CampusTimeSegment.EveningStudy3, "晚自习第三段", "Evening Study 3", 22, 10, 22, 55),
            new SegmentInfo(CampusTimeSegment.DormReturn, "回宿舍", "Return to Dorm", 22, 55, 23, 10),
            new SegmentInfo(CampusTimeSegment.DormCheck, "查寝", "Dorm Check", 23, 10, 23, 20),
            new SegmentInfo(CampusTimeSegment.LightsOut, "熄灯", "Lights Out", 23, 20, 23, 40),
            new SegmentInfo(CampusTimeSegment.NightFree, "夜间自由行动", "Night Free Time", 23, 40, 6, 0),
            new SegmentInfo(CampusTimeSegment.PreWakeSettlement, "凌晨结算", "Pre-Wake Settlement", 6, 0, 6, 30)
        };

        /// <summary>
        /// 获取时段中文名。
        /// </summary>
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

        /// <summary>
        /// 获取时段时间标签。
        /// </summary>
        public static string GetTimeLabel(CampusTimeSegment segment)
        {
            SegmentInfo info = GetInfo(segment);
            return FormatClockMinute(info.StartMinute) + " - " + FormatClockMinute(info.EndMinute);
        }

        /// <summary>
        /// 获取时段持续分钟数。
        /// </summary>
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

        /// <summary>
        /// 获取时段开始分钟。
        /// </summary>
        public static int GetStartMinute(CampusTimeSegment segment)
        {
            return GetInfo(segment).StartMinute;
        }

        /// <summary>
        /// 获取时段结束分钟。
        /// </summary>
        public static int GetEndMinute(CampusTimeSegment segment)
        {
            return GetInfo(segment).EndMinute;
        }

        /// <summary>
        /// 获取下一个时段。
        /// </summary>
        public static CampusTimeSegment GetNextSegment(CampusTimeSegment segment)
        {
            return segment == CampusTimeSegment.PreWakeSettlement
                ? CampusTimeSegment.WakeUp
                : (CampusTimeSegment)((int)segment + 1);
        }

        /// <summary>
        /// 根据时段内计时获取当前校内时间文本。
        /// </summary>
        public static string GetClockText(CampusTimeSegment segment, float elapsedMinutes)
        {
            int elapsed = Mathf.Clamp(Mathf.FloorToInt(elapsedMinutes), 0, Mathf.FloorToInt(GetDurationMinutes(segment)));
            int clockMinute = (GetStartMinute(segment) + elapsed) % MinutesPerDay;
            return FormatClockMinute(clockMinute);
        }

        /// <summary>
        /// 将当天分钟数格式化为 HH:mm。
        /// </summary>
        public static string FormatClockMinute(int minuteOfDay)
        {
            int normalizedMinute = ((minuteOfDay % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;
            int hour = normalizedMinute / 60;
            int minute = normalizedMinute % 60;
            return hour.ToString("00") + ":" + minute.ToString("00");
        }

        /// <summary>
        /// 获取时段变化日志文本。
        /// </summary>
        public static string GetSegmentLogMessage(CampusTimeSegment segment, string dateText)
        {
            return GetSegmentLogMessage(CampusLanguageState.CurrentLanguage, segment, dateText);
        }

        public static string GetSegmentLogMessage(CampusDisplayLanguage language, CampusTimeSegment segment, string dateText)
        {
            switch (segment)
            {
                case CampusTimeSegment.WakeUp:
                    return Resolve(language, "[系统] " + dateText + "，起床铃响了。", "[System] " + dateText + ", the wake-up bell rings.");
                case CampusTimeSegment.BreakfastPrep:
                    return Resolve(language, "[系统] " + dateText + "，早饭和到教室时间开始。", "[System] " + dateText + ", breakfast and classroom prep begin.");
                case CampusTimeSegment.MorningReading:
                    return Resolve(language, "[课堂] " + dateText + "，早读开始，学生陆续进教室。", "[Classroom] " + dateText + ", morning reading begins and students enter classrooms.");
                case CampusTimeSegment.MorningClass1:
                    return Resolve(language, "[课堂] " + dateText + "，上午第一节课开始。", "[Classroom] " + dateText + ", morning class 1 begins.");
                case CampusTimeSegment.MorningBreak1:
                    return Resolve(language, "[课堂] " + dateText + "，上午第一节课下课，课间开始。", "[Classroom] " + dateText + ", morning class 1 ends and break begins.");
                case CampusTimeSegment.MorningClass2:
                    return Resolve(language, "[课堂] " + dateText + "，上午第二节课开始。", "[Classroom] " + dateText + ", morning class 2 begins.");
                case CampusTimeSegment.MorningExerciseBreak:
                    return Resolve(language, "[系统] " + dateText + "，大课间和课间操开始。", "[System] " + dateText + ", long break and exercises begin.");
                case CampusTimeSegment.MorningClass3:
                    return Resolve(language, "[课堂] " + dateText + "，上午第三节课开始。", "[Classroom] " + dateText + ", morning class 3 begins.");
                case CampusTimeSegment.MorningBreak2:
                    return Resolve(language, "[课堂] " + dateText + "，课间开始。", "[Classroom] " + dateText + ", break begins.");
                case CampusTimeSegment.MorningClass4:
                    return Resolve(language, "[课堂] " + dateText + "，上午第四节课开始。", "[Classroom] " + dateText + ", morning class 4 begins.");
                case CampusTimeSegment.LunchBreak:
                    return Resolve(language, "[系统] " + dateText + "，午饭和午休开始。", "[System] " + dateText + ", lunch and noon rest begin.");
                case CampusTimeSegment.AfternoonClass1:
                    return Resolve(language, "[课堂] " + dateText + "，下午第一节课开始。", "[Classroom] " + dateText + ", afternoon class 1 begins.");
                case CampusTimeSegment.AfternoonBreak1:
                    return Resolve(language, "[课堂] " + dateText + "，课间开始。", "[Classroom] " + dateText + ", break begins.");
                case CampusTimeSegment.AfternoonClass2:
                    return Resolve(language, "[课堂] " + dateText + "，下午第二节课开始。", "[Classroom] " + dateText + ", afternoon class 2 begins.");
                case CampusTimeSegment.AfternoonBreak2:
                    return Resolve(language, "[课堂] " + dateText + "，课间开始。", "[Classroom] " + dateText + ", break begins.");
                case CampusTimeSegment.AfternoonClass3:
                    return Resolve(language, "[课堂] " + dateText + "，下午第三节课开始。", "[Classroom] " + dateText + ", afternoon class 3 begins.");
                case CampusTimeSegment.AfternoonBreak3:
                    return Resolve(language, "[课堂] " + dateText + "，课间开始。", "[Classroom] " + dateText + ", break begins.");
                case CampusTimeSegment.AfternoonClass4:
                    return Resolve(language, "[课堂] " + dateText + "，下午第四节课开始。", "[Classroom] " + dateText + ", afternoon class 4 begins.");
                case CampusTimeSegment.DinnerBreak:
                    return Resolve(language, "[系统] " + dateText + "，晚饭和自由活动开始。", "[System] " + dateText + ", dinner and free time begin.");
                case CampusTimeSegment.EveningStudy1:
                    return Resolve(language, "[课堂] " + dateText + "，晚自习第一段开始。", "[Classroom] " + dateText + ", evening study 1 begins.");
                case CampusTimeSegment.EveningBreak1:
                    return Resolve(language, "[课堂] " + dateText + "，晚自习课间开始。", "[Classroom] " + dateText + ", evening study break begins.");
                case CampusTimeSegment.EveningStudy2:
                    return Resolve(language, "[课堂] " + dateText + "，晚自习第二段开始。", "[Classroom] " + dateText + ", evening study 2 begins.");
                case CampusTimeSegment.EveningBreak2:
                    return Resolve(language, "[课堂] " + dateText + "，晚自习课间开始。", "[Classroom] " + dateText + ", evening study break begins.");
                case CampusTimeSegment.EveningStudy3:
                    return Resolve(language, "[课堂] " + dateText + "，晚自习第三段开始。", "[Classroom] " + dateText + ", evening study 3 begins.");
                case CampusTimeSegment.DormReturn:
                    return Resolve(language, "[系统] " + dateText + "，晚自习结束，学生回宿舍。", "[System] " + dateText + ", evening study ends and students return to dorms.");
                case CampusTimeSegment.DormCheck:
                    return Resolve(language, "[系统] " + dateText + "，查寝开始。", "[System] " + dateText + ", dorm check begins.");
                case CampusTimeSegment.LightsOut:
                    return Resolve(language, "[系统] " + dateText + "，熄灯了，但校园没有睡着。", "[System] " + dateText + ", lights are out, but campus is not asleep.");
                case CampusTimeSegment.NightFree:
                    return Resolve(language, "[系统] " + dateText + "，夜间自由行动开始。", "[System] " + dateText + ", night free time begins.");
                case CampusTimeSegment.PreWakeSettlement:
                    return Resolve(language, "[系统] " + dateText + "，凌晨结算开始。", "[System] " + dateText + ", pre-wake settlement begins.");
                default:
                    return Resolve(
                        language,
                        "[系统] " + dateText + "，" + GetChineseName(segment) + "开始。",
                        "[System] " + dateText + ", " + GetDisplayName(CampusDisplayLanguage.English, segment) + " begins.");
            }
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

        private readonly struct SegmentInfo
        {
            public readonly CampusTimeSegment Segment;
            public readonly string ChineseName;
            public readonly string EnglishName;
            public readonly int StartMinute;
            public readonly int EndMinute;

            public SegmentInfo(CampusTimeSegment segment, string chineseName, string englishName, int startHour, int startMinute, int endHour, int endMinute)
            {
                Segment = segment;
                ChineseName = chineseName;
                EnglishName = englishName;
                StartMinute = startHour * 60 + startMinute;
                EndMinute = endHour * 60 + endMinute;
            }
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
    }
}
