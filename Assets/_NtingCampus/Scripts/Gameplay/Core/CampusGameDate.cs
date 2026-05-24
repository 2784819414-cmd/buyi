using System;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampus.Gameplay.Core
{
    [Serializable]
    public struct CampusGameDate
    {
        [SerializeField, Min(1)] private int year;
        [SerializeField, Range(1, 12)] private int month;
        [SerializeField, Range(1, 31)] private int day;

        public CampusGameDate(int year, int month, int day)
        {
            this.year = 2021;
            this.month = 9;
            this.day = 1;
            Set(year, month, day);
        }

        public static CampusGameDate DefaultStartDate => new(2021, 9, 1);

        public int Year => year;
        public int Month => month;
        public int Day => day;

        public void Set(int newYear, int newMonth, int newDay)
        {
            year = Mathf.Clamp(newYear, 1, 9999);
            month = Mathf.Clamp(newMonth, 1, 12);
            day = Mathf.Clamp(newDay, 1, DateTime.DaysInMonth(year, month));
        }

        public void AddDays(int dayCount)
        {
            DateTime dateTime = ToDateTime().AddDays(dayCount);
            year = dateTime.Year;
            month = dateTime.Month;
            day = dateTime.Day;
        }

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

        public DateTime ToDateTime()
        {
            return TryToDateTime(out DateTime dateTime) ? dateTime : new DateTime(2021, 9, 1);
        }

        public string ToDisplayString()
        {
            return ToDisplayString(CampusLanguageState.CurrentLanguage);
        }

        public string ToDisplayString(CampusDisplayLanguage language)
        {
            DateTime dateTime = ToDateTime();
            string chinese = dateTime.Year + "年" + dateTime.Month + "月" + dateTime.Day + "日 " + GetChineseWeekday(dateTime.DayOfWeek);
            string english = dateTime.ToString("yyyy-MM-dd") + " " + dateTime.DayOfWeek;
            return Resolve(language, chinese, english);
        }

        public override string ToString()
        {
            return ToDisplayString();
        }

        private static string GetChineseWeekday(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday => "星期一",
                DayOfWeek.Tuesday => "星期二",
                DayOfWeek.Wednesday => "星期三",
                DayOfWeek.Thursday => "星期四",
                DayOfWeek.Friday => "星期五",
                DayOfWeek.Saturday => "星期六",
                DayOfWeek.Sunday => "星期日",
                _ => "星期一"
            };
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
