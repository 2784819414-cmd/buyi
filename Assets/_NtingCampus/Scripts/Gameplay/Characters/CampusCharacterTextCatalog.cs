using NtingCampus.Gameplay.Sanctions;
using NtingCampus.UI.Runtime.Gameplay;

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
                CampusCharacterDialogueId.TeacherTeachClassInteractive => Resolve(language, "回到座位上。课还没结束。", "Back to your seat. Class is not over."),
                CampusCharacterDialogueId.TeacherInvestigateInteractive => Resolve(language, "我在处理这里的秩序。", "I am handling order here."),
                CampusCharacterDialogueId.TeacherPatrolInteractive => Resolve(language, "走廊保持畅通，别停留。", "Keep the corridor clear and keep moving."),
                CampusCharacterDialogueId.TeacherDefaultInteractive => Resolve(language, "把时间用在正事上，我看着呢。", "Use your time properly. I am watching."),
                CampusCharacterDialogueId.StudentAttendClassGoodInteractive => Resolve(language, "我得跟上这节课。", "I need to keep up with this lesson."),
                CampusCharacterDialogueId.StudentAttendClassDefaultInteractive => Resolve(language, "下课再聊，行吗？", "Can we talk after class instead?"),
                CampusCharacterDialogueId.StudentDozeInteractive => Resolve(language, "我只要闭眼一秒就完了。", "If I close my eyes for one second, I am done for."),
                CampusCharacterDialogueId.StudentSocializeTroublemakerInteractive => Resolve(language, "这里总得闹点事出来。", "Something has to happen around here."),
                CampusCharacterDialogueId.StudentSocializeDefaultInteractive => Resolve(language, "课间一下子就过去了。", "Breaks go by too fast."),
                CampusCharacterDialogueId.StudentAvoidDisturbanceInteractive => Resolve(language, "别想，我可不背这个锅。", "No chance. I am not taking the blame for this."),
                CampusCharacterDialogueId.StudentCheckBulletinInteractive => Resolve(language, "公告栏上总有值得看的东西。", "There is always something worth noticing on the board."),
                CampusCharacterDialogueId.StudentRestDormInteractive => Resolve(language, "我现在只想找个安静角落。", "I just need a quiet corner for a while."),
                CampusCharacterDialogueId.StudentDefaultInteractive => Resolve(language, "我先低调一点。", "I am keeping my head down."),
                CampusCharacterDialogueId.StudentAttendClassGoodAmbient => Resolve(language, "这段得记住。", "Need to remember this."),
                CampusCharacterDialogueId.StudentAttendClassDefaultAmbient => Resolve(language, "继续翻……", "Back to the page..."),
                CampusCharacterDialogueId.StudentDozeAmbient => Resolve(language, "先别睡着……", "Just staying awake..."),
                CampusCharacterDialogueId.TeacherTeachClassAmbient => Resolve(language, "注意听课。", "Eyes on the lesson."),
                CampusCharacterDialogueId.TeacherPatrolAmbient => Resolve(language, "走廊要清出来。", "Hallway stays clear."),
                CampusCharacterDialogueId.TeacherOfficeAmbient => Resolve(language, "又一份报告。", "Another report, another delay."),
                CampusCharacterDialogueId.StudentRestDormAmbient => Resolve(language, "总算安静一点了。", "Finally, a quieter room."),
                CampusCharacterDialogueId.StudentWanderAmbient => Resolve(language, "休息时间根本不够。", "Break is not long enough."),
                CampusCharacterDialogueId.StudentCheckBulletinAmbient => Resolve(language, "总有人贴点新东西。", "Someone always posts something interesting."),
                CampusCharacterDialogueId.StudentSocializeTroublemakerAmbient => Resolve(language, "这房间得添点火花。", "This room needs a spark."),
                CampusCharacterDialogueId.StudentSocializeDefaultAmbient => Resolve(language, "课间一下子就过去了。", "Breaks go by too fast."),
                CampusCharacterDialogueId.TeacherInvestigateAmbient => Resolve(language, "别再窃窃私语了。", "No more whispers."),
                CampusCharacterDialogueId.StudentAvoidDisturbanceAmbient => Resolve(language, "别把我扯进去。", "Do not drag me into it."),
                CampusCharacterDialogueId.TeacherDefaultAmbient => Resolve(language, "安静，挺好。", "Quiet room, good."),
                CampusCharacterDialogueId.StudentDefaultAmbient => Resolve(language, "嗯。", "Mm."),
                _ => "..."
            };
        }

        public static string GetMemory(CampusDisplayLanguage language, CampusCharacterMemoryId id)
        {
            return id switch
            {
                CampusCharacterMemoryId.None => CampusGameplayDebugTextCatalog.Get(language, CampusGameplayDebugTextId.None),
                CampusCharacterMemoryId.TalkedToActor => Resolve(language, "和角色交谈过", "Talked to an actor"),
                CampusCharacterMemoryId.PassedNoteToday => Resolve(language, "今天传过纸条", "Passed a note today"),
                CampusCharacterMemoryId.ReceivedNoteFromActor => Resolve(language, "收到其他角色传来的纸条", "Received a note from another actor"),
                CampusCharacterMemoryId.CaughtNotePassing => Resolve(language, "撞见了传纸条", "Caught note passing"),
                CampusCharacterMemoryId.SawRestlessClassroom => Resolve(language, "注意到教室骚动", "Saw a restless classroom"),
                CampusCharacterMemoryId.DozedOffInClass => Resolve(language, "上课时睡着", "Dozed off in class"),
                CampusCharacterMemoryId.NoticedClassroomDozing => Resolve(language, "注意到学生睡着", "Noticed a student dozing"),
                CampusCharacterMemoryId.SneakedOutDuringClass => Resolve(language, "上课时溜出教室", "Sneaked out during class"),
                CampusCharacterMemoryId.CaughtSkippingClass => Resolve(language, "逃课被发现", "Caught skipping class"),
                CampusCharacterMemoryId.TookProtectedFood => Resolve(language, "拿走了受保护食物", "Took protected food"),
                CampusCharacterMemoryId.SuspectedProtectedTheft => Resolve(language, "被怀疑偷拿受保护物品", "Was suspected of taking a protected item"),
                CampusCharacterMemoryId.ArrangedPrivatePickup => Resolve(language, "私下安排了取物", "Arranged a private pickup"),
                CampusCharacterMemoryId.ClaimedItemTaken => Resolve(language, "拿走了已认领物品", "Took a claimed item"),
                CampusCharacterMemoryId.LostClaimedItem => Resolve(language, "丢失了已认领物品", "Lost a claimed item"),
                CampusCharacterMemoryId.RecoveredClaimedItem => Resolve(language, "取回了已认领物品", "Recovered a claimed item"),
                CampusCharacterMemoryId.ReportedMissingItem => Resolve(language, "报告物品遗失", "Reported a missing item"),
                CampusCharacterMemoryId.WitnessedActorMischief => Resolve(language, "目击角色惹事", "Witnessed actor mischief"),
                CampusCharacterMemoryId.DistrustsActor => Resolve(language, "开始不信任某个角色", "Started distrusting an actor"),
                CampusCharacterMemoryId.ImpressedByActor => Resolve(language, "觉得某个角色很敢做事", "Impressed by an actor's nerve"),
                CampusCharacterMemoryId.WarnedAboutActor => Resolve(language, "提醒过别人注意某个角色", "Warned others about an actor"),
                CampusCharacterMemoryId.SelectedProtectedGoods => Resolve(language, "选择了待结账物品", "Selected protected goods"),
                CampusCharacterMemoryId.ClearedProtectedTransfer => Resolve(language, "完成了受保护物品结算", "Cleared a protected transfer"),
                CampusCharacterMemoryId.ReceivedClearedGoods => Resolve(language, "拿到了已结算物品", "Received a cleared item"),
                CampusCharacterMemoryId.TookProtectedItem => Resolve(language, "拿走了受保护物品", "Took a protected item"),
                CampusCharacterMemoryId.WitnessedTheft => Resolve(language, "目击偷窃", "Witnessed theft"),
                CampusCharacterMemoryId.FoundContraband => Resolve(language, "发现违禁品", "Found contraband"),
                _ => CampusGameplayDebugTextCatalog.Get(language, CampusGameplayDebugTextId.None)
            };
        }

        public static string FormatTalkPrompt(CampusDisplayLanguage language, string displayName)
        {
            return Resolve(language, "交谈 " + displayName, "Talk " + displayName);
        }

        public static string FormatTalkLog(CampusDisplayLanguage language, string displayName, string line)
        {
            return Resolve(language, "[对话] " + displayName + "：" + line, "[Talk] " + displayName + ": " + line);
        }

        public static string FormatNpcSpeechLog(CampusDisplayLanguage language, string displayName, string line)
        {
            return Resolve(language, "[NPC] " + displayName + "：" + line, "[NPC] " + displayName + ": " + line);
        }

        public static string FormatSceneRosterReady(CampusDisplayLanguage language, int studentCount, int teacherCount)
        {
            return Resolve(
                language,
                "[系统] 场景角色列表已就绪。学生=" + studentCount + "，教师=" + teacherCount + "。",
                "[System] Scene roster ready. Students=" + studentCount + ", Teachers=" + teacherCount + ".");
        }

        public static string FormatPlayerCharacterBound(CampusDisplayLanguage language, string displayName)
        {
            return Resolve(
                language,
                "[系统] 玩家角色已绑定到 " + displayName + "。",
                "[System] Player character bound to " + displayName + ".");
        }

        public static string FormatPlayerControlSwitched(CampusDisplayLanguage language, string displayName)
        {
            return Resolve(
                language,
                "[系统] 玩家控制已切换到 " + displayName + "。",
                "[System] Player control switched to " + displayName + ".");
        }

        public static string FormatSanctionIssued(CampusDisplayLanguage language, string displayName, CampusSanctionLevel level)
        {
            return level switch
            {
                CampusSanctionLevel.Warning => Resolve(language, "[处分] " + displayName + " 收到口头警告。", "[Sanction] " + displayName + " received a verbal warning."),
                CampusSanctionLevel.Reprimand => Resolve(language, "[处分] " + displayName + " 在课堂上被训斥。", "[Sanction] " + displayName + " was reprimanded in class."),
                CampusSanctionLevel.OfficePunishment => Resolve(language, "[处分] " + displayName + " 被带去了办公室。", "[Sanction] " + displayName + " was sent to the office."),
                _ => string.Empty
            };
        }

        public static string FormatDailyWarnings(CampusDisplayLanguage language, int warningCount)
        {
            return Resolve(language, "[处分] 当日警告数 = " + warningCount + "。", "[Sanction] Daily warnings = " + warningCount + ".");
        }

        public static string FormatSanctionReason(CampusDisplayLanguage language, CampusSanctionReasonId id)
        {
            return id switch
            {
                CampusSanctionReasonId.SkippingClassObserved => Resolve(language, "[处分] 逃课被老师撞见。", "[Sanction] Skipping class was seen by a teacher."),
                CampusSanctionReasonId.ProtectedPropertyObserved => Resolve(language, "[处分] 有人在目击下拿走受保护物品。", "[Sanction] Protected property was taken under observation."),
                CampusSanctionReasonId.ContrabandFound => Resolve(language, "[处分] 检查时发现违禁品。", "[Sanction] Contraband was found during inspection."),
                _ => string.Empty
            };
        }

        public static string FormatProtectedItemObserved(CampusDisplayLanguage language, string itemDisplayName)
        {
            return Resolve(
                language,
                "[事件] 有人目击了受保护物品移动：" + itemDisplayName + "。",
                "[Incident] Protected item movement was witnessed: " + itemDisplayName + ".");
        }

        private static string Resolve(
            CampusDisplayLanguage language,
            string chinese,
            string english,
            string traditionalChinese = null,
            string russian = null,
            string japanese = null)
        {
            return CampusDisplayLanguageCatalog.Resolve(language, chinese, english, traditionalChinese, russian, japanese);
        }
    }
}
