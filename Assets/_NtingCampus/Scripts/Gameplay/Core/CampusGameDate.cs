using System;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    /// <summary>
    /// 可序列化的游戏日期，只保存年月日，运行时再转换为 DateTime 计算星期。
    /// </summary>
    [Serializable]
    public struct CampusGameDate
    {
        [SerializeField, Min(1)] private int year;
        [SerializeField, Range(1, 12)] private int month;
        [SerializeField, Range(1, 31)] private int day;

        /// <summary>
        /// 创建一个游戏日期。
        /// </summary>
        public CampusGameDate(int year, int month, int day)
        {
            this.year = 2021;
            this.month = 9;
            this.day = 1;
            Set(year, month, day);
        }

        /// <summary>
        /// 默认开局日期。
        /// </summary>
        public static CampusGameDate DefaultStartDate => new CampusGameDate(2021, 9, 1);

        /// <summary>
        /// 年。
        /// </summary>
        public int Year => year;

        /// <summary>
        /// 月。
        /// </summary>
        public int Month => month;

        /// <summary>
        /// 日。
        /// </summary>
        public int Day => day;

        /// <summary>
        /// 设置年月日，并修正到合法日期范围。
        /// </summary>
        public void Set(int newYear, int newMonth, int newDay)
        {
            year = Mathf.Clamp(newYear, 1, 9999);
            month = Mathf.Clamp(newMonth, 1, 12);
            day = Mathf.Clamp(newDay, 1, DateTime.DaysInMonth(year, month));
        }

        /// <summary>
        /// 增加指定天数。
        /// </summary>
        public void AddDays(int dayCount)
        {
            DateTime dateTime = ToDateTime().AddDays(dayCount);
            year = dateTime.Year;
            month = dateTime.Month;
            day = dateTime.Day;
        }

        /// <summary>
        /// 尝试转换为运行时 DateTime。
        /// </summary>
        public bool TryToDateTime(out DateTime dateTime)
        {
            try
            {
                dateTime = new DateTime(year, month, day);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                dateTime = new DateTime(2021, 9, 1);
                return false;
            }
        }

        /// <summary>
        /// 转换为运行时 DateTime。
        /// </summary>
        public DateTime ToDateTime()
        {
            return TryToDateTime(out DateTime dateTime) ? dateTime : new DateTime(2021, 9, 1);
        }

        /// <summary>
        /// 返回当前语言的日期显示文本。
        /// </summary>
        public string ToDisplayString()
        {
            return ToDisplayString(CampusLanguageState.CurrentLanguage);
        }

        public string ToDisplayString(CampusDisplayLanguage language)
        {
            DateTime dateTime = ToDateTime();
            string chinese = dateTime.Year + "年" + dateTime.Month + "月" + dateTime.Day + "日 " + GetChineseWeekday(dateTime.DayOfWeek);
            string english = dateTime.ToString("yyyy-MM-dd") + " " + GetEnglishWeekday(dateTime.DayOfWeek);
            return language switch
            {
                CampusDisplayLanguage.English => english,
                CampusDisplayLanguage.Bilingual => chinese + " / " + english,
                _ => chinese
            };
        }

        /// <summary>
        /// 返回中文日期显示文本。
        /// </summary>
        public override string ToString()
        {
            return ToDisplayString();
        }

        private static string GetChineseWeekday(DayOfWeek dayOfWeek)
        {
            switch (dayOfWeek)
            {
                case DayOfWeek.Monday:
                    return "星期一";
                case DayOfWeek.Tuesday:
                    return "星期二";
                case DayOfWeek.Wednesday:
                    return "星期三";
                case DayOfWeek.Thursday:
                    return "星期四";
                case DayOfWeek.Friday:
                    return "星期五";
                case DayOfWeek.Saturday:
                    return "星期六";
                case DayOfWeek.Sunday:
                    return "星期日";
                default:
                    return "星期三";
            }
        }

        private static string GetEnglishWeekday(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday => "Monday",
                DayOfWeek.Tuesday => "Tuesday",
                DayOfWeek.Wednesday => "Wednesday",
                DayOfWeek.Thursday => "Thursday",
                DayOfWeek.Friday => "Friday",
                DayOfWeek.Saturday => "Saturday",
                DayOfWeek.Sunday => "Sunday",
                _ => "Wednesday"
            };
        }
    }
}
