using System.Collections.Generic;
using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Schedule
{
    public enum CampusClassroomTextId
    {
        WaitingForClass = 0,
        DistractionActive = 1,
        ClassInSession = 2,
        StudentDozedLog = 3,
        ActorCaughtLeavingLog = 4,
        ActorLeftDuringDistractionLog = 5,
        ActorSneakedOutLog = 6
    }

    public static class CampusClassroomTextCatalog
    {
        private readonly struct Entry
        {
            public Entry(string chinese, string english)
            {
                Chinese = chinese;
                English = english;
            }

            public string Chinese { get; }
            public string English { get; }
        }

        private static readonly Dictionary<CampusClassroomTextId, Entry> Entries = new Dictionary<CampusClassroomTextId, Entry>
        {
            { CampusClassroomTextId.WaitingForClass, new Entry("课堂闭环等待上课。", "Classroom loop is waiting for class.") },
            { CampusClassroomTextId.DistractionActive, new Entry("老师分心窗口开启：{0} 秒，可传纸条或离开教室。", "Teacher distraction window: {0}s. Passing notes or leaving class is possible.") },
            { CampusClassroomTextId.ClassInSession, new Entry("上课中：高困倦学生可能睡着，受控角色离开教室会触发逃课判定。", "Class is in session: sleepy students may doze off, and leaving class can trigger skip detection.") },
            { CampusClassroomTextId.StudentDozedLog, new Entry("[课堂] {0} 撑不住睡着了，老师注意力被吸引。", "[Classroom] {0} dozed off, drawing the teacher's attention.") },
            { CampusClassroomTextId.ActorCaughtLeavingLog, new Entry("[课堂] {0} 离开教室时被老师发现。", "[Classroom] {0} was seen leaving class by the teacher.") },
            { CampusClassroomTextId.ActorLeftDuringDistractionLog, new Entry("[课堂] {0} 趁老师分神离开教室。", "[Classroom] {0} left the classroom while the teacher was distracted.") },
            { CampusClassroomTextId.ActorSneakedOutLog, new Entry("[课堂] {0} 偷偷离开教室，暂时没人追出来。", "[Classroom] {0} slipped out of class. No one follows for now.") }
        };

        public static string Get(CampusClassroomTextId id)
        {
            return Get(CampusLanguageState.CurrentLanguage, id);
        }

        public static string Get(CampusDisplayLanguage language, CampusClassroomTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return language switch
            {
                CampusDisplayLanguage.English => entry.English,
                CampusDisplayLanguage.Bilingual => entry.Chinese + " / " + entry.English,
                _ => entry.Chinese
            };
        }

        public static string Format(CampusClassroomTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }
    }
}
