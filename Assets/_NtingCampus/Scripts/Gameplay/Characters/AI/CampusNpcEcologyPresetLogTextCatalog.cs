using System.Collections.Generic;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;
using Entry = NtingCampus.UI.Runtime.Gameplay.CampusLocalizedTextEntry;

namespace NtingCampus.Gameplay.Characters
{
    internal enum CampusNpcEcologyPresetLogTextId
    {
        MissingPresetFile = 0,
        FailedToParsePreset = 1,
        PresetFileEmpty = 2,
        DuplicateProfileId = 3,
        RowEmpty = 4,
        RowMissingField = 5,
        ActionUnknownActionMode = 6,
        ActionUnknownRepeatPolicy = 7,
        TargetRuleMissingActionId = 8,
        TargetRuleUnknownTargetKind = 9,
        TargetRuleUnknownRoomType = 10,
        ProfileUnknownRole = 11,
        EntryRowEmpty = 12,
        EntryMissingId = 13,
        EntryUnknownIntentKind = 14,
        EntryMissingActionChainId = 15,
        TargetRuleUnknownActionId = 16,
        TargetRuleUnknownActionChainId = 17,
        TargetRuleMissingFacilityTypes = 18,
        TargetRuleMissingRoomType = 19,
        TargetRuleUnknownRequirement = 20,
        ActionMissingTargetRule = 21,
        ActionChainUnknownActionId = 22,
        NoDecisionProfiles = 23,
        ProfileNoValidEntries = 24,
        ProfileEntryUnknownActionChainId = 25,
        DuplicateTableId = 26,
        OwnerUnknownValue = 27,
        SelectionDebug = 28
    }

    internal static class CampusNpcEcologyPresetLogTextCatalog
    {
        private const string Prefix = "[CampusNpcEcologyPresetCatalog] ";
        private const string SelectionPrefix = "[NpcSchedule] ";

        private static readonly Dictionary<CampusNpcEcologyPresetLogTextId, Entry> Entries = new()
        {
            { CampusNpcEcologyPresetLogTextId.MissingPresetFile, new Entry("缺少必需的 NPC 生态预设文件：{0}", "Missing required NPC ecology preset file: {0}") },
            { CampusNpcEcologyPresetLogTextId.FailedToParsePreset, new Entry("解析 NPC 生态预设 {0} 失败：{1}", "Failed to parse NPC ecology preset {0}: {1}") },
            { CampusNpcEcologyPresetLogTextId.PresetFileEmpty, new Entry("NPC 生态预设文件为空：{0}", "NPC ecology preset file is empty: {0}") },
            { CampusNpcEcologyPresetLogTextId.DuplicateProfileId, new Entry("NpcDecisionProfiles id {0} 在第 {1} 行重复，已忽略。", "Duplicate NpcDecisionProfiles id {0} at row {1} was ignored.") },
            { CampusNpcEcologyPresetLogTextId.RowEmpty, new Entry("{0} 第 {1} 行为空。", "{0} row {1} is empty.") },
            { CampusNpcEcologyPresetLogTextId.RowMissingField, new Entry("{0} 第 {1} 行缺少 {2}。", "{0} row {1} is missing {2}.") },
            { CampusNpcEcologyPresetLogTextId.ActionUnknownActionMode, new Entry("ActionCatalog 动作 {0} 的 ActionMode 未知：{1}", "ActionCatalog action {0} has unknown ActionMode {1}.") },
            { CampusNpcEcologyPresetLogTextId.ActionUnknownRepeatPolicy, new Entry("ActionCatalog 动作 {0} 的 RepeatPolicy 未知：{1}", "ActionCatalog action {0} has unknown RepeatPolicy {1}.") },
            { CampusNpcEcologyPresetLogTextId.TargetRuleMissingActionId, new Entry("ActionTargetRule {0} 缺少 ActionId。", "ActionTargetRule {0} is missing ActionId.") },
            { CampusNpcEcologyPresetLogTextId.TargetRuleUnknownTargetKind, new Entry("ActionTargetRule {0} 的 TargetKind 未知：{1}", "ActionTargetRule {0} has unknown TargetKind {1}.") },
            { CampusNpcEcologyPresetLogTextId.TargetRuleUnknownRoomType, new Entry("ActionTargetRule {0} 的 RoomType 未知：{1}", "ActionTargetRule {0} has unknown RoomType {1}.") },
            { CampusNpcEcologyPresetLogTextId.ProfileUnknownRole, new Entry("NpcDecisionProfile {0} 的 Role 未知：{1}", "NpcDecisionProfile {0} has unknown Role {1}.") },
            { CampusNpcEcologyPresetLogTextId.EntryRowEmpty, new Entry("NpcDecisionProfile {0} 的 entry 第 {1} 行为空。", "NpcDecisionProfile {0} entry row {1} is empty.") },
            { CampusNpcEcologyPresetLogTextId.EntryMissingId, new Entry("NpcDecisionProfile {0} 的 entry 第 {1} 行缺少 Id。", "NpcDecisionProfile {0} entry row {1} is missing Id.") },
            { CampusNpcEcologyPresetLogTextId.EntryUnknownIntentKind, new Entry("NpcDecisionProfile {0} 的 entry {1} 的 IntentKind 未知：{2}", "NpcDecisionProfile {0} entry {1} has unknown IntentKind {2}.") },
            { CampusNpcEcologyPresetLogTextId.EntryMissingActionChainId, new Entry("NpcDecisionProfile {0} 的 entry {1} 缺少 ActionChainId。", "NpcDecisionProfile {0} entry {1} is missing ActionChainId.") },
            { CampusNpcEcologyPresetLogTextId.TargetRuleUnknownActionId, new Entry("ActionTargetRule {0} 引用了未知 ActionId：{1}", "ActionTargetRule {0} references unknown ActionId {1}.") },
            { CampusNpcEcologyPresetLogTextId.TargetRuleUnknownActionChainId, new Entry("ActionTargetRule {0} 引用了未知 ActionChainId：{1}", "ActionTargetRule {0} references unknown ActionChainId {1}.") },
            { CampusNpcEcologyPresetLogTextId.TargetRuleMissingFacilityTypes, new Entry("ActionTargetRule {0} 对 TargetKind {1} 缺少必需 FacilityTypes。", "ActionTargetRule {0} is missing required FacilityTypes for TargetKind {1}.") },
            { CampusNpcEcologyPresetLogTextId.TargetRuleMissingRoomType, new Entry("ActionTargetRule {0} 对 TargetKind {1} 缺少必需 RoomType。", "ActionTargetRule {0} is missing required RoomType for TargetKind {1}.") },
            { CampusNpcEcologyPresetLogTextId.TargetRuleUnknownRequirement, new Entry("ActionTargetRule {0} 引用了未知 Requirement：{1}", "ActionTargetRule {0} references unknown Requirement {1}.") },
            { CampusNpcEcologyPresetLogTextId.ActionMissingTargetRule, new Entry("ActionCatalog 动作 {0} 没有 ActionTargetRules 行。", "ActionCatalog action {0} has no ActionTargetRules row.") },
            { CampusNpcEcologyPresetLogTextId.ActionChainUnknownActionId, new Entry("ActionChain {0} 引用了未知 ActionId：{1}", "ActionChain {0} references unknown ActionId {1}.") },
            { CampusNpcEcologyPresetLogTextId.NoDecisionProfiles, new Entry("没有从 {0} 加载到 NPC decision profiles。", "No NPC decision profiles were loaded from {0}.") },
            { CampusNpcEcologyPresetLogTextId.ProfileNoValidEntries, new Entry("NPC decision profile {0} 没有有效 entries，已忽略。", "NPC decision profile {0} has no valid entries and was ignored.") },
            { CampusNpcEcologyPresetLogTextId.ProfileEntryUnknownActionChainId, new Entry("NPC decision profile {0} 的 entry {1} 引用了未知 ActionChainId：{2}", "NPC decision profile {0} entry {1} references unknown ActionChainId {2}.") },
            { CampusNpcEcologyPresetLogTextId.DuplicateTableId, new Entry("{0} id {1} 在第 {2} 行重复，已忽略。", "Duplicate {0} id {1} at row {2} was ignored.") },
            { CampusNpcEcologyPresetLogTextId.OwnerUnknownValue, new Entry("{0} 的 {1} 未知：{2}", "{0} has unknown {1} {2}.") },
            { CampusNpcEcologyPresetLogTextId.SelectionDebug, new Entry("npc={0} profile={1} entry={2} chain={3} action={4} score={5}", "npc={0} profile={1} entry={2} chain={3} action={4} score={5}") }
        };

        public static string Format(CampusNpcEcologyPresetLogTextId id, params object[] args)
        {
            return string.Format(Get(id), args);
        }

        public static string Get(CampusNpcEcologyPresetLogTextId id)
        {
            Entry entry = Entries.TryGetValue(id, out Entry resolved)
                ? resolved
                : new Entry(id.ToString(), id.ToString());

            return entry.Get(CampusLanguageState.CurrentLanguage);
        }

        public static void Warning(CampusNpcEcologyPresetLogTextId id, params object[] args)
        {
            Debug.LogWarning(Prefix + Format(id, args));
        }

        public static void Selection(CampusNpcEcologyPresetLogTextId id, params object[] args)
        {
            Debug.Log(SelectionPrefix + Format(id, args));
        }
    }
}
