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

    public enum CampusSanctionReasonId
    {
        SkippingClassObserved = 0,
        ProtectedPropertyObserved = 1,
        ContrabandFound = 2
    }

    public static class CampusCharacterTextCatalog
    {
        public static string GetDialogue(CampusDisplayLanguage language, CampusCharacterDialogueId id)
        {
            return id switch
            {
                CampusCharacterDialogueId.TeacherTeachClassInteractive => Get(language, "回到座位上。课还没结束。", "Back to your seat. Class is not over."),
                CampusCharacterDialogueId.TeacherInvestigateInteractive => Get(language, "我会处理这里的秩序。", "I am handling order here."),
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
                CampusCharacterDialogueId.StudentSocializeDefaultAmbient => Get(language, "课间一下子就过去了。", "Breaks go by too fast."),
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
                CampusCharacterMemoryId.TalkedToActor => Get(language, "和角色交谈过", "Talked to an actor"),
                CampusCharacterMemoryId.PassedNoteToday => Get(language, "今天传过纸条", "Passed a note today"),
                CampusCharacterMemoryId.ReceivedNoteFromActor => Get(language, "收到其他角色传来的纸条", "Received a note from another actor"),
                CampusCharacterMemoryId.CaughtNotePassing => Get(language, "撞见了传纸条", "Caught note passing"),
                CampusCharacterMemoryId.SawRestlessClassroom => Get(language, "注意到教室骚动", "Saw a restless classroom"),
                CampusCharacterMemoryId.DozedOffInClass => Get(language, "上课时睡着", "Dozed off in class"),
                CampusCharacterMemoryId.NoticedClassroomDozing => Get(language, "注意到学生睡着", "Noticed a student dozing"),
                CampusCharacterMemoryId.SneakedOutDuringClass => Get(language, "上课时溜出教室", "Sneaked out during class"),
                CampusCharacterMemoryId.CaughtSkippingClass => Get(language, "逃课被发现", "Caught skipping class"),
                CampusCharacterMemoryId.TookProtectedFood => Get(language, "拿走了受保护物品", "Took a protected item"),
                CampusCharacterMemoryId.SuspectedProtectedTheft => Get(language, "被怀疑偷拿受保护物品", "Was suspected of taking a protected item"),
                CampusCharacterMemoryId.ArrangedPrivatePickup => Get(language, "私下安排了取件", "Arranged a private pickup"),
                CampusCharacterMemoryId.ClaimedItemTaken => Get(language, "拿走了已认领物品", "Took a claimed item"),
                CampusCharacterMemoryId.LostClaimedItem => Get(language, "丢失了已认领物品", "Lost a claimed item"),
                CampusCharacterMemoryId.RecoveredClaimedItem => Get(language, "取回了已认领物品", "Recovered a claimed item"),
                CampusCharacterMemoryId.ReportedMissingItem => Get(language, "报告物品遗失", "Reported a missing item"),
                CampusCharacterMemoryId.WitnessedActorMischief => Get(language, "目击角色惹事", "Witnessed actor mischief"),
                CampusCharacterMemoryId.DistrustsActor => Get(language, "开始不信任某个角色", "Started distrusting an actor"),
                CampusCharacterMemoryId.ImpressedByActor => Get(language, "觉得某个角色很会找事", "Impressed by an actor's nerve"),
                CampusCharacterMemoryId.WarnedAboutActor => Get(language, "提醒过别人注意某个角色", "Warned others about an actor"),
                CampusCharacterMemoryId.SelectedProtectedGoods => Get(language, "选择了受保护物品", "Selected a protected item"),
                CampusCharacterMemoryId.ClearedProtectedTransfer => Get(language, "完成了受保护物品结算", "Cleared a protected transfer"),
                CampusCharacterMemoryId.ReceivedClearedGoods => Get(language, "拿到了已结算物品", "Received a cleared item"),
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

        public static string FormatNpcSpeechLog(CampusDisplayLanguage language, string displayName, string line)
        {
            return language switch
            {
                CampusDisplayLanguage.English => "[NPC] " + displayName + ": " + line,
                CampusDisplayLanguage.Bilingual => "[NPC] " + displayName + ": " + line,
                _ => "[NPC] " + displayName + "：" + line
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

        public static string FormatSanctionReason(CampusDisplayLanguage language, CampusSanctionReasonId id)
        {
            return id switch
            {
                CampusSanctionReasonId.SkippingClassObserved => Get(language, "[处分] 逃课被老师撞见。", "[Sanction] Skipping class was seen by a teacher."),
                CampusSanctionReasonId.ProtectedPropertyObserved => Get(language, "[处分] 有人在目击下拿走受保护物品。", "[Sanction] Protected property was taken under observation."),
                CampusSanctionReasonId.ContrabandFound => Get(language, "[处分] 检查时发现违禁物。", "[Sanction] Contraband was found during inspection."),
                _ => string.Empty
            };
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
