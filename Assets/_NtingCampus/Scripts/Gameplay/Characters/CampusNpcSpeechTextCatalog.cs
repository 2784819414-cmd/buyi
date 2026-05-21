using System.Collections.Generic;
using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Characters
{
    public enum CampusNpcSpeechTextId
    {
        StudentNeedsDesk = 0,
        StudentDeciding = 1,
        StudentBackToDesk = 2,
        StudentBackToDorm = 3,
        StudentHeadingOut = 4,
        StaffAtWorkstation = 20,
        StaffGoingWorkstation = 21,
        StaffHeadingOut = 22,
        TeacherClassInProgress = 40,
        TeacherBackOfficeInteractive = 41,
        TeacherHeadingToClass = 42,
        TeacherBackOffice = 43,
        TeacherHeadingOut = 44
    }

    public static class CampusNpcSpeechTextCatalog
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

        private static readonly Dictionary<CampusNpcSpeechTextId, Entry> Entries =
            new Dictionary<CampusNpcSpeechTextId, Entry>
            {
                { CampusNpcSpeechTextId.StudentNeedsDesk, new Entry("我要回到座位。", "I need to get back to my desk.") },
                { CampusNpcSpeechTextId.StudentDeciding, new Entry("我在想接下来做什么。", "I am deciding what to do next.") },
                { CampusNpcSpeechTextId.StudentBackToDesk, new Entry("回到我的座位。", "Back to my desk.") },
                { CampusNpcSpeechTextId.StudentBackToDorm, new Entry("回宿舍休息。", "Back to the dorm.") },
                { CampusNpcSpeechTextId.StudentHeadingOut, new Entry("我先出去转转。", "I am heading out.") },
                { CampusNpcSpeechTextId.StaffAtWorkstation, new Entry("我在岗位上。", "I am at my workstation.") },
                { CampusNpcSpeechTextId.StaffGoingWorkstation, new Entry("去我的岗位。", "Heading to my workstation.") },
                { CampusNpcSpeechTextId.StaffHeadingOut, new Entry("我先去别处。", "I am heading out.") },
                { CampusNpcSpeechTextId.TeacherClassInProgress, new Entry("正在上课。", "Class is in progress.") },
                { CampusNpcSpeechTextId.TeacherBackOfficeInteractive, new Entry("我正要回办公室。", "I am heading back to the office.") },
                { CampusNpcSpeechTextId.TeacherHeadingToClass, new Entry("去上课。", "Heading to class.") },
                { CampusNpcSpeechTextId.TeacherBackOffice, new Entry("回办公室。", "Back to the office.") },
                { CampusNpcSpeechTextId.TeacherHeadingOut, new Entry("我先出去。", "I am heading out.") }
            };

        public static string Get(CampusNpcSpeechTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            switch (CampusLanguageState.CurrentLanguage)
            {
                case CampusDisplayLanguage.English:
                    return entry.English;
                case CampusDisplayLanguage.Bilingual:
                    return entry.Chinese + " / " + entry.English;
                default:
                    return entry.Chinese;
            }
        }
    }
}
