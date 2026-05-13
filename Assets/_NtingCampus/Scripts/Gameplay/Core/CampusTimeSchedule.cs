using System;
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
            new SegmentInfo(CampusTimeSegment.WakeUp, "起床 / 洗漱", 6, 30, 7, 0),
            new SegmentInfo(CampusTimeSegment.BreakfastPrep, "早饭 / 到教室", 7, 0, 7, 30),
            new SegmentInfo(CampusTimeSegment.MorningReading, "早读", 7, 30, 7, 55),
            new SegmentInfo(CampusTimeSegment.MorningClass1, "上午第一节课", 8, 0, 8, 40),
            new SegmentInfo(CampusTimeSegment.MorningBreak1, "课间", 8, 40, 8, 50),
            new SegmentInfo(CampusTimeSegment.MorningClass2, "上午第二节课", 8, 50, 9, 30),
            new SegmentInfo(CampusTimeSegment.MorningExerciseBreak, "大课间 / 课间操", 9, 30, 10, 0),
            new SegmentInfo(CampusTimeSegment.MorningClass3, "上午第三节课", 10, 0, 10, 40),
            new SegmentInfo(CampusTimeSegment.MorningBreak2, "课间", 10, 40, 10, 50),
            new SegmentInfo(CampusTimeSegment.MorningClass4, "上午第四节课", 10, 50, 11, 30),
            new SegmentInfo(CampusTimeSegment.LunchBreak, "午饭 / 午休", 11, 30, 14, 0),
            new SegmentInfo(CampusTimeSegment.AfternoonClass1, "下午第一节课", 14, 0, 14, 40),
            new SegmentInfo(CampusTimeSegment.AfternoonBreak1, "课间", 14, 40, 14, 50),
            new SegmentInfo(CampusTimeSegment.AfternoonClass2, "下午第二节课", 14, 50, 15, 30),
            new SegmentInfo(CampusTimeSegment.AfternoonBreak2, "课间", 15, 30, 15, 45),
            new SegmentInfo(CampusTimeSegment.AfternoonClass3, "下午第三节课", 15, 45, 16, 25),
            new SegmentInfo(CampusTimeSegment.AfternoonBreak3, "课间", 16, 25, 16, 35),
            new SegmentInfo(CampusTimeSegment.AfternoonClass4, "下午第四节课", 16, 35, 17, 15),
            new SegmentInfo(CampusTimeSegment.DinnerBreak, "晚饭 / 自由活动", 17, 15, 18, 40),
            new SegmentInfo(CampusTimeSegment.EveningStudy1, "晚自习第一段", 18, 40, 20, 10),
            new SegmentInfo(CampusTimeSegment.EveningBreak1, "晚自习课间", 20, 10, 20, 25),
            new SegmentInfo(CampusTimeSegment.EveningStudy2, "晚自习第二段", 20, 25, 21, 55),
            new SegmentInfo(CampusTimeSegment.EveningBreak2, "晚自习课间", 21, 55, 22, 10),
            new SegmentInfo(CampusTimeSegment.EveningStudy3, "晚自习第三段", 22, 10, 22, 55),
            new SegmentInfo(CampusTimeSegment.DormReturn, "回宿舍", 22, 55, 23, 10),
            new SegmentInfo(CampusTimeSegment.DormCheck, "查寝", 23, 10, 23, 20),
            new SegmentInfo(CampusTimeSegment.LightsOut, "熄灯", 23, 20, 23, 40),
            new SegmentInfo(CampusTimeSegment.NightFree, "夜间自由行动", 23, 40, 6, 0),
            new SegmentInfo(CampusTimeSegment.PreWakeSettlement, "凌晨结算", 6, 0, 6, 30)
        };

        /// <summary>
        /// 获取时段中文名。
        /// </summary>
        public static string GetChineseName(CampusTimeSegment segment)
        {
            return GetInfo(segment).ChineseName;
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
            switch (segment)
            {
                case CampusTimeSegment.WakeUp:
                    return "[系统] " + dateText + "，起床铃响了。";
                case CampusTimeSegment.BreakfastPrep:
                    return "[系统] " + dateText + "，早饭和到教室时间开始。";
                case CampusTimeSegment.MorningReading:
                    return "[课堂] " + dateText + "，早读开始，学生陆续进教室。";
                case CampusTimeSegment.MorningClass1:
                    return "[课堂] " + dateText + "，上午第一节课开始。";
                case CampusTimeSegment.MorningBreak1:
                    return "[课堂] " + dateText + "，上午第一节课下课，课间开始。";
                case CampusTimeSegment.MorningClass2:
                    return "[课堂] " + dateText + "，上午第二节课开始。";
                case CampusTimeSegment.MorningExerciseBreak:
                    return "[系统] " + dateText + "，大课间和课间操开始。";
                case CampusTimeSegment.MorningClass3:
                    return "[课堂] " + dateText + "，上午第三节课开始。";
                case CampusTimeSegment.MorningBreak2:
                    return "[课堂] " + dateText + "，课间开始。";
                case CampusTimeSegment.MorningClass4:
                    return "[课堂] " + dateText + "，上午第四节课开始。";
                case CampusTimeSegment.LunchBreak:
                    return "[系统] " + dateText + "，午饭和午休开始。";
                case CampusTimeSegment.AfternoonClass1:
                    return "[课堂] " + dateText + "，下午第一节课开始。";
                case CampusTimeSegment.AfternoonBreak1:
                    return "[课堂] " + dateText + "，课间开始。";
                case CampusTimeSegment.AfternoonClass2:
                    return "[课堂] " + dateText + "，下午第二节课开始。";
                case CampusTimeSegment.AfternoonBreak2:
                    return "[课堂] " + dateText + "，课间开始。";
                case CampusTimeSegment.AfternoonClass3:
                    return "[课堂] " + dateText + "，下午第三节课开始。";
                case CampusTimeSegment.AfternoonBreak3:
                    return "[课堂] " + dateText + "，课间开始。";
                case CampusTimeSegment.AfternoonClass4:
                    return "[课堂] " + dateText + "，下午第四节课开始。";
                case CampusTimeSegment.DinnerBreak:
                    return "[系统] " + dateText + "，晚饭和自由活动开始。";
                case CampusTimeSegment.EveningStudy1:
                    return "[课堂] " + dateText + "，晚自习第一段开始。";
                case CampusTimeSegment.EveningBreak1:
                    return "[课堂] " + dateText + "，晚自习课间开始。";
                case CampusTimeSegment.EveningStudy2:
                    return "[课堂] " + dateText + "，晚自习第二段开始。";
                case CampusTimeSegment.EveningBreak2:
                    return "[课堂] " + dateText + "，晚自习课间开始。";
                case CampusTimeSegment.EveningStudy3:
                    return "[课堂] " + dateText + "，晚自习第三段开始。";
                case CampusTimeSegment.DormReturn:
                    return "[系统] " + dateText + "，晚自习结束，学生回宿舍。";
                case CampusTimeSegment.DormCheck:
                    return "[系统] " + dateText + "，查寝开始。";
                case CampusTimeSegment.LightsOut:
                    return "[系统] " + dateText + "，熄灯了，但校园没有睡着。";
                case CampusTimeSegment.NightFree:
                    return "[系统] " + dateText + "，夜间自由行动开始。";
                case CampusTimeSegment.PreWakeSettlement:
                    return "[系统] " + dateText + "，凌晨结算开始。";
                default:
                    return "[系统] " + dateText + "，" + GetChineseName(segment) + "开始。";
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
            public readonly int StartMinute;
            public readonly int EndMinute;

            public SegmentInfo(CampusTimeSegment segment, string chineseName, int startHour, int startMinute, int endHour, int endMinute)
            {
                Segment = segment;
                ChineseName = chineseName;
                StartMinute = startHour * 60 + startMinute;
                EndMinute = endHour * 60 + endMinute;
            }
        }
    }
}
