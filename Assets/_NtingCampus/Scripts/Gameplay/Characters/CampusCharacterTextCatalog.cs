using NtingCampus.Gameplay.Sanctions;
using NtingCampus.Gameplay.UI;

namespace NtingCampus.Gameplay.Characters
{
    public enum CampusCharacterDialogueId
    {
        Missing = 0,
        TeacherTeachClassInteractive = 1,
        TeacherInvestigateInteractive = 2,
        TeacherPatrolInteractive = 3,
        TeacherDefaultInteractive = 4,
        StudentAttendClassGoodInteractive = 5,
        StudentAttendClassDefaultInteractive = 6,
        StudentDozeInteractive = 7,
        StudentSocializeTroublemakerInteractive = 8,
        StudentSocializeDefaultInteractive = 9,
        StudentAvoidDisturbanceInteractive = 10,
        StudentCheckBulletinInteractive = 11,
        StudentRestDormInteractive = 12,
        StudentDefaultInteractive = 13,
        StudentAttendClassGoodAmbient = 14,
        StudentAttendClassDefaultAmbient = 15,
        StudentDozeAmbient = 16,
        TeacherTeachClassAmbient = 17,
        TeacherPatrolAmbient = 18,
        TeacherOfficeAmbient = 19,
        StudentRestDormAmbient = 20,
        StudentWanderAmbient = 21,
        StudentCheckBulletinAmbient = 22,
        StudentSocializeTroublemakerAmbient = 23,
        StudentSocializeDefaultAmbient = 24,
        TeacherInvestigateAmbient = 25,
        StudentAvoidDisturbanceAmbient = 26,
        TeacherDefaultAmbient = 27,
        StudentDefaultAmbient = 28
    }

    public static class CampusCharacterTextCatalog
    {
        public static string GetDialogue(CampusDisplayLanguage language, CampusCharacterDialogueId id)
        {
            return id switch
            {
                CampusCharacterDialogueId.TeacherTeachClassInteractive => Get(language, "回到座位上。课还没结束。", "Back to your seat. Class is not over."),
                CampusCharacterDialogueId.TeacherInvestigateInteractive => Get(language, "这里的事我已经在查了。", "I am already investigating what happened here."),
                CampusCharacterDialogueId.TeacherPatrolInteractive => Get(language, "走廊保持畅通，别停留。", "Keep the corridor clear and keep moving."),
                CampusCharacterDialogueId.TeacherDefaultInteractive => Get(language, "把时间用在正事上，我看着呢。", "Use your time properly. I am watching."),
                CampusCharacterDialogueId.StudentAttendClassGoodInteractive => Get(language, "我得跟上这节课。", "I need to keep up with this lesson."),
                CampusCharacterDialogueId.StudentAttendClassDefaultInteractive => Get(language, "下课再聊，行吗？", "Can we talk after class instead?"),
                CampusCharacterDialogueId.StudentDozeInteractive => Get(language, "我只要闭眼一秒就完了。", "If I close my eyes for one second, I am done for."),
                CampusCharacterDialogueId.StudentSocializeTroublemakerInteractive => Get(language, "这里总得闹点事出来。", "Something has to happen around here."),
                CampusCharacterDialogueId.StudentSocializeDefaultInteractive => Get(language, "课间一下子就过去了。", "Breaks go by too fast."),
                CampusCharacterDialogueId.StudentAvoidDisturbanceInteractive => Get(language, "别想，我可不背这个锅。", "No chance. I am not taking the blame for this."),
                CampusCharacterDialogueId.StudentCheckBulletinInteractive => Get(language, "公告栏上总有值得看的东西。", "There is always something worth noticing on the board."),
                CampusCharacterDialogueId.StudentRestDormInteractive => Get(language, "我现在只想找个安静角落。", "I just need a quiet corner for a while."),
                CampusCharacterDialogueId.StudentDefaultInteractive => Get(language, "我先低调一点。", "I am keeping my head down."),
                CampusCharacterDialogueId.StudentAttendClassGoodAmbient => Get(language, "这段得记住。", "Need to remember this."),
                CampusCharacterDialogueId.StudentAttendClassDefaultAmbient => Get(language, "继续翻……", "Back to the page..."),
                CampusCharacterDialogueId.StudentDozeAmbient => Get(language, "先别睡着……", "Just staying awake..."),
                CampusCharacterDialogueId.TeacherTeachClassAmbient => Get(language, "注意听课。", "Eyes on the lesson."),
                CampusCharacterDialogueId.TeacherPatrolAmbient => Get(language, "走廊要清出来。", "Hallway stays clear."),
                CampusCharacterDialogueId.TeacherOfficeAmbient => Get(language, "又一份报告。", "Another report, another delay."),
                CampusCharacterDialogueId.StudentRestDormAmbient => Get(language, "总算安静一点了。", "Finally, a quieter room."),
                CampusCharacterDialogueId.StudentWanderAmbient => Get(language, "休息时间根本不够。", "Break is not long enough."),
                CampusCharacterDialogueId.StudentCheckBulletinAmbient => Get(language, "总有人贴点新东西。", "Someone always posts something interesting."),
                CampusCharacterDialogueId.StudentSocializeTroublemakerAmbient => Get(language, "这房间得添点火花。", "This room needs a spark."),
                CampusCharacterDialogueId.StudentSocializeDefaultAmbient => Get(language, "刚才那边发生什么了？", "What happened back there?"),
                CampusCharacterDialogueId.TeacherInvestigateAmbient => Get(language, "别再窃窃私语了。", "No more whispers."),
                CampusCharacterDialogueId.StudentAvoidDisturbanceAmbient => Get(language, "别把我扯进去。", "Do not drag me into it."),
                CampusCharacterDialogueId.TeacherDefaultAmbient => Get(language, "安静，挺好。", "Quiet room, good."),
                CampusCharacterDialogueId.StudentDefaultAmbient => Get(language, "嗯。", "Mm."),
                _ => "..."
            };
        }

        public static string GetMemory(CampusDisplayLanguage language, CampusCharacterMemoryId id)
        {
            return id switch
            {
                CampusCharacterMemoryId.TalkedToPlayer => Get(language, "和玩家交谈过", "Talked to player"),
                CampusCharacterMemoryId.PassedNoteToday => Get(language, "今天传过纸条", "Passed a note today"),
                CampusCharacterMemoryId.ReceivedNoteFromPlayer => Get(language, "收到玩家传来的纸条", "Received a note from player"),
                CampusCharacterMemoryId.CaughtNotePassing => Get(language, "撞见了传纸条", "Caught note passing"),
                CampusCharacterMemoryId.SawRestlessClassroom => Get(language, "注意到教室骚动", "Saw a restless classroom"),
                CampusCharacterMemoryId.DozedOffInClass => Get(language, "上课时睡着", "Dozed off in class"),
                CampusCharacterMemoryId.NoticedClassroomDozing => Get(language, "注意到学生睡着", "Noticed a student dozing"),
                CampusCharacterMemoryId.SneakedOutDuringClass => Get(language, "上课时溜出教室", "Sneaked out during class"),
                CampusCharacterMemoryId.CaughtSkippingClass => Get(language, "逃课被发现", "Caught skipping class"),
                CampusCharacterMemoryId.StoleCanteenFood => Get(language, "拿走了食堂食物", "Took canteen food"),
                CampusCharacterMemoryId.CanteenTheftSuspected => Get(language, "被食堂店员怀疑", "Suspected by canteen clerk"),
                CampusCharacterMemoryId.OrderedSecretDelivery => Get(language, "偷偷点了外卖", "Ordered delivery secretly"),
                CampusCharacterMemoryId.DeliveryStolen => Get(language, "拿走了外卖", "Took a delivery"),
                CampusCharacterMemoryId.LostDelivery => Get(language, "外卖丢了", "Lost a delivery"),
                CampusCharacterMemoryId.PickedUpDelivery => Get(language, "\u53d6\u5230\u4e86\u5916\u5356", "Picked up a delivery"),
                CampusCharacterMemoryId.ReportedLostDelivery => Get(language, "\u62a5\u544a\u5916\u5356\u4e22\u5931", "Reported a lost delivery"),
                _ => CampusGameplayDebugTextCatalog.Get(language, CampusGameplayDebugTextId.None)
            };
        }

        public static string FormatTalkPrompt(CampusDisplayLanguage language, string displayName)
        {
            return language switch
            {
                CampusDisplayLanguage.English => "Talk " + displayName,
                CampusDisplayLanguage.Bilingual => "交谈 " + displayName + " / Talk " + displayName,
                _ => "交谈 " + displayName
            };
        }

        public static string FormatTalkLog(CampusDisplayLanguage language, string displayName, string line)
        {
            return language switch
            {
                CampusDisplayLanguage.English => "[Talk] " + displayName + ": " + line,
                CampusDisplayLanguage.Bilingual => "[对话 / Talk] " + displayName + ": " + line,
                _ => "[对话] " + displayName + "：" + line
            };
        }

        public static string FormatSceneRosterReady(CampusDisplayLanguage language, int studentCount, int teacherCount)
        {
            return language switch
            {
                CampusDisplayLanguage.English => "[System] Scene roster ready. Students=" + studentCount + ", Teachers=" + teacherCount + ".",
                CampusDisplayLanguage.Bilingual => "[系统 / System] 场景角色列表已就绪。学生=" + studentCount + "，教师=" + teacherCount + " / Scene roster ready. Students=" + studentCount + ", Teachers=" + teacherCount + ".",
                _ => "[系统] 场景角色列表已就绪。学生=" + studentCount + "，教师=" + teacherCount + "。"
            };
        }

        public static string FormatPlayerCharacterBound(CampusDisplayLanguage language, string displayName)
        {
            return language switch
            {
                CampusDisplayLanguage.English => "[System] Player character bound to " + displayName + ".",
                CampusDisplayLanguage.Bilingual => "[系统 / System] 玩家角色已绑定到 " + displayName + " / Player character bound to " + displayName + ".",
                _ => "[系统] 玩家角色已绑定到 " + displayName + "。"
            };
        }

        public static string FormatPlayerControlSwitched(CampusDisplayLanguage language, string displayName)
        {
            return language switch
            {
                CampusDisplayLanguage.English => "[System] Player control switched to " + displayName + ".",
                CampusDisplayLanguage.Bilingual => "[系统 / System] 玩家控制已切换到 " + displayName + " / Player control switched to " + displayName + ".",
                _ => "[系统] 玩家控制已切换到 " + displayName + "。"
            };
        }

        public static string FormatSanctionIssued(CampusDisplayLanguage language, string displayName, CampusSanctionLevel level)
        {
            return level switch
            {
                CampusSanctionLevel.Warning => Get(language, "[处分] " + displayName + " 收到口头警告。", "[Sanction] " + displayName + " received a verbal warning."),
                CampusSanctionLevel.Reprimand => Get(language, "[处分] " + displayName + " 在课堂上被训斥。", "[Sanction] " + displayName + " was reprimanded in class."),
                CampusSanctionLevel.OfficePunishment => Get(language, "[处分] " + displayName + " 被带去了办公室。", "[Sanction] " + displayName + " was sent to the office."),
                _ => string.Empty
            };
        }

        public static string FormatDailyWarnings(CampusDisplayLanguage language, int warningCount)
        {
            return Get(language, "[处分] 当日警告数 = " + warningCount + "。", "[Sanction] Daily warnings = " + warningCount + ".");
        }

        private static string Get(CampusDisplayLanguage language, string chinese, string english)
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
