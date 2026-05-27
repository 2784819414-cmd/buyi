using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal static partial class CampusNpcEcologyPresetCatalog
    {
        private static EcologyPresetData LoadData()
        {
            EcologyPresetData emptyData = new EcologyPresetData();
            if (!CampusRuntimeModPresetStore.TryReadJson(PresetFileName, out string json))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.MissingPresetFile,
                    PresetFileName);
                return emptyData;
            }

            try
            {
                EcologyPresetFile file = JsonUtility.FromJson<EcologyPresetFile>(json);
                return ParseFile(file);
            }
            catch (Exception exception)
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.FailedToParsePreset,
                    PresetFileName,
                    exception.Message);
                return emptyData;
            }
        }

        private static EcologyPresetData ParseFile(EcologyPresetFile file)
        {
            EcologyPresetData data = new EcologyPresetData();
            if (file == null)
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.PresetFileEmpty,
                    PresetFileName);
                return data;
            }

            data.EnableSelectionDebug = file.EnableSelectionDebug;
            ParseActionCatalog(data, file.ActionCatalog);
            ParseActionTargetRules(data, file.ActionTargetRules);
            ParseActionChains(data, file.ActionChains);
            ParseNpcDecisionProfiles(data, file.NpcDecisionProfiles);
            ValidateData(data);
            return data;
        }

        private static void ParseActionCatalog(EcologyPresetData data, List<ActionCatalogFileRecord> files)
        {
            if (files == null)
            {
                return;
            }

            for (int i = 0; i < files.Count; i++)
            {
                ActionRecord action = ParseAction(files[i], i);
                TryAddUnique(data.ActionCatalog, "ActionCatalog", i, action != null ? action.ActionId : string.Empty, action);
            }
        }

        private static void ParseActionTargetRules(EcologyPresetData data, List<ActionTargetRuleFileRecord> files)
        {
            if (files == null)
            {
                return;
            }

            for (int i = 0; i < files.Count; i++)
            {
                ActionTargetRuleRecord targetRule = ParseActionTargetRule(files[i], i);
                TryAddUnique(data.ActionTargetRules, "ActionTargetRules", i, targetRule != null ? targetRule.Id : string.Empty, targetRule);
            }
        }

        private static void ParseActionChains(EcologyPresetData data, List<ActionChainFileRecord> files)
        {
            if (files == null)
            {
                return;
            }

            for (int i = 0; i < files.Count; i++)
            {
                ActionChainRecord chain = ParseActionChain(files[i], i);
                TryAddUnique(data.ActionChains, "ActionChains", i, chain != null ? chain.Id : string.Empty, chain);
            }
        }

        private static void ParseNpcDecisionProfiles(EcologyPresetData data, List<NpcDecisionProfileFileRecord> files)
        {
            if (files == null)
            {
                return;
            }

            HashSet<string> profileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Count; i++)
            {
                NpcDecisionProfileRecord profile = ParseDecisionProfile(files[i], i);
                if (profile == null || profile.Entries.Count == 0)
                {
                    continue;
                }

                if (!profileIds.Add(profile.Id))
                {
                    CampusNpcEcologyPresetLogTextCatalog.Warning(
                        CampusNpcEcologyPresetLogTextId.DuplicateProfileId,
                        profile.Id,
                        i);
                    continue;
                }

                data.NpcDecisionProfiles.Add(profile);
            }
        }

        private static ActionRecord ParseAction(ActionCatalogFileRecord file, int rowIndex)
        {
            if (file == null)
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.RowEmpty,
                    "ActionCatalog",
                    rowIndex);
                return null;
            }

            string actionId = NormalizeId(file.ActionId);
            if (string.IsNullOrEmpty(actionId))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.RowMissingField,
                    "ActionCatalog",
                    rowIndex,
                    "ActionId");
                return null;
            }

            if (!TryParseActionMode(file.ActionMode, out CampusNpcEcologyActionMode actionMode))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.ActionUnknownActionMode,
                    actionId,
                    file.ActionMode);
                return null;
            }

            if (!TryParseRepeatPolicy(file.RepeatPolicy, out CampusNpcEcologyActionRepeatPolicy repeatPolicy))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.ActionUnknownRepeatPolicy,
                    actionId,
                    file.RepeatPolicy);
                return null;
            }

            return new ActionRecord
            {
                ActionId = actionId,
                ActionMode = actionMode,
                Payload = file.Payload ?? string.Empty,
                RepeatPolicy = repeatPolicy
            };
        }

        private static ActionTargetRuleRecord ParseActionTargetRule(ActionTargetRuleFileRecord file, int rowIndex)
        {
            if (file == null)
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.RowEmpty,
                    "ActionTargetRules",
                    rowIndex);
                return null;
            }

            string ruleId = NormalizeId(file.Id);
            if (string.IsNullOrEmpty(ruleId))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.RowMissingField,
                    "ActionTargetRules",
                    rowIndex,
                    "Id");
                return null;
            }

            if (string.IsNullOrEmpty(NormalizeId(file.ActionId)))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.TargetRuleMissingActionId,
                    ruleId);
                return null;
            }

            if (!TryParseTargetKind(file.TargetKind, out CampusNpcEcologyTargetKind targetKind))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.TargetRuleUnknownTargetKind,
                    ruleId,
                    file.TargetKind);
                return null;
            }

            if (!TryParseOptionalRoomType(file.RoomType, out CampusRoomType roomType))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.TargetRuleUnknownRoomType,
                    ruleId,
                    file.RoomType);
                return null;
            }

            CampusFacilityType[] facilityTypes = ParseFacilityTypes(file.FacilityTypes, "ActionTargetRule '" + ruleId + "'", out bool hasInvalidFacilityType);
            CampusFacilityType[] navigationFacilityTypes = ParseFacilityTypes(file.NavigationFacilityTypes, "ActionTargetRule '" + ruleId + "' NavigationFacilityTypes", out bool hasInvalidNavigationFacilityType);
            if (hasInvalidFacilityType || hasInvalidNavigationFacilityType)
            {
                return null;
            }

            return new ActionTargetRuleRecord
            {
                Id = ruleId,
                ActionId = NormalizeId(file.ActionId),
                ActionChainIds = NormalizeIds(file.ActionChainIds),
                TargetKind = targetKind,
                StationTypeIds = NormalizeIds(file.StationTypeIds),
                ObjectIds = NormalizeIds(file.ObjectIds),
                RoomType = roomType,
                FacilityTypes = facilityTypes,
                NavigationFacilityTypes = navigationFacilityTypes,
                Owner = NormalizeId(file.Owner),
                SourceLocation = NormalizeId(file.SourceLocation),
                SourceContainerPrefix = NormalizeId(file.SourceContainerPrefix),
                DefinitionId = NormalizeId(file.DefinitionId),
                Priority = file.Priority,
                StopDistance = Mathf.Max(0.05f, file.StopDistance),
                RequirementIds = ParseRequirementIds(file.Requirements)
            };
        }

        private static ActionChainRecord ParseActionChain(ActionChainFileRecord file, int rowIndex)
        {
            if (file == null)
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.RowEmpty,
                    "ActionChains",
                    rowIndex);
                return null;
            }

            string chainId = NormalizeId(file.Id);
            if (string.IsNullOrEmpty(chainId))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.RowMissingField,
                    "ActionChains",
                    rowIndex,
                    "Id");
                return null;
            }

            return new ActionChainRecord
            {
                Id = chainId,
                ActionIds = NormalizeIds(file.ActionIds)
            };
        }

        private static NpcDecisionProfileRecord ParseDecisionProfile(NpcDecisionProfileFileRecord file, int rowIndex)
        {
            if (file == null)
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.RowEmpty,
                    "NpcDecisionProfiles",
                    rowIndex);
                return null;
            }

            string profileId = NormalizeId(file.Id);
            if (string.IsNullOrEmpty(profileId))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.RowMissingField,
                    "NpcDecisionProfiles",
                    rowIndex,
                    "Id");
                return null;
            }

            if (!TryParseRole(file.Role, out CampusCharacterRole role))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.ProfileUnknownRole,
                    profileId,
                    file.Role);
                return null;
            }

            CampusTeacherDuty teacherDutyMask = ParseTeacherDutyMask(file.TeacherDuties, "NpcDecisionProfile '" + profileId + "'", out bool hasInvalidTeacherDuty);
            CampusStaffDuty staffDutyMask = ParseStaffDutyMask(file.StaffDuties, "NpcDecisionProfile '" + profileId + "'", out bool hasInvalidStaffDuty);
            CampusCharacterTrait[] traits = ParseTraits(file.Traits, "NpcDecisionProfile '" + profileId + "'", out bool hasInvalidTrait);
            if (hasInvalidTeacherDuty || hasInvalidStaffDuty || hasInvalidTrait)
            {
                return null;
            }

            NpcDecisionProfileRecord record = new NpcDecisionProfileRecord
            {
                Id = profileId,
                Role = role,
                CharacterIds = NormalizeIds(file.CharacterIds),
                TeacherDutyMask = teacherDutyMask,
                StaffDutyMask = staffDutyMask,
                Traits = traits
            };

            if (file.Entries != null)
            {
                for (int i = 0; i < file.Entries.Count; i++)
                {
                    ScheduleEntryRecord entry = ParseEntry(file.Entries[i], profileId, i);
                    if (entry != null)
                    {
                        record.Entries.Add(entry);
                    }
                }
            }

            return record;
        }

        private static ScheduleEntryRecord ParseEntry(ScheduleEntryFileRecord file, string profileId, int rowIndex)
        {
            if (file == null)
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.EntryRowEmpty,
                    profileId,
                    rowIndex);
                return null;
            }

            string entryId = NormalizeId(file.Id);
            if (string.IsNullOrEmpty(entryId))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.EntryMissingId,
                    profileId,
                    rowIndex);
                return null;
            }

            if (!TryParseIntentKind(file.IntentKind, out CampusNpcIntentKind intentKind))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.EntryUnknownIntentKind,
                    profileId,
                    entryId,
                    file.IntentKind);
                return null;
            }

            string actionChainId = NormalizeId(file.ActionChainId);
            if (string.IsNullOrEmpty(actionChainId))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.EntryMissingActionChainId,
                    profileId,
                    entryId);
                return null;
            }

            CampusTimeSegment[] segments = ParseSegments(file.Segments, "NpcDecisionProfile '" + profileId + "' entry '" + entryId + "'", out bool hasInvalidSegment);
            CampusNpcEcologyScheduleWindow[] scheduleWindows = ParseScheduleWindows(file.ScheduleWindows, "NpcDecisionProfile '" + profileId + "' entry '" + entryId + "'", out bool hasInvalidScheduleWindow);
            if (hasInvalidSegment || hasInvalidScheduleWindow)
            {
                return null;
            }

            ScheduleEntryRecord record = new ScheduleEntryRecord
            {
                Id = entryId,
                Segments = segments,
                ScheduleWindows = scheduleWindows,
                IntentKind = intentKind,
                IntentLabel = string.IsNullOrWhiteSpace(file.IntentLabel) ? entryId : file.IntentLabel.Trim(),
                ActionChainId = actionChainId,
                Score = file.Score
            };

            if (TryParseClockMinute(file.StartClock, out int startMinute) &&
                TryParseClockMinute(file.EndClock, out int endMinute))
            {
                record.HasClockRange = true;
                record.StartMinute = startMinute;
                record.EndMinute = endMinute;
            }

            return record;
        }

        private static void ValidateData(EcologyPresetData data)
        {
            if (data == null)
            {
                return;
            }

            ValidateActionCatalog(data);
            ValidateActionChains(data);
            ValidateActionTargetRules(data);
            RebuildActionTargetRuleIndex(data);
            ValidateActionCatalogTargets(data);
            ValidateActionChains(data);
            ValidateNpcDecisionProfiles(data);
        }

        private static void ValidateActionCatalog(EcologyPresetData data)
        {
            List<string> invalidActionIds = new List<string>();
            foreach (KeyValuePair<string, ActionRecord> pair in data.ActionCatalog)
            {
                if (pair.Value == null || string.IsNullOrEmpty(pair.Value.ActionId))
                {
                    invalidActionIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < invalidActionIds.Count; i++)
            {
                data.ActionCatalog.Remove(invalidActionIds[i]);
            }
        }

        private static void ValidateActionTargetRules(EcologyPresetData data)
        {
            List<string> invalidRuleIds = new List<string>();
            foreach (KeyValuePair<string, ActionTargetRuleRecord> pair in data.ActionTargetRules)
            {
                if (!IsValidActionTargetRule(pair.Value, data))
                {
                    invalidRuleIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < invalidRuleIds.Count; i++)
            {
                data.ActionTargetRules.Remove(invalidRuleIds[i]);
            }
        }

        private static bool IsValidActionTargetRule(ActionTargetRuleRecord targetRule, EcologyPresetData data)
        {
            if (targetRule == null || string.IsNullOrEmpty(targetRule.Id))
            {
                return false;
            }

            if (string.IsNullOrEmpty(targetRule.ActionId))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.TargetRuleMissingActionId,
                    targetRule.Id);
                return false;
            }

            if (!data.ActionCatalog.ContainsKey(targetRule.ActionId))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.TargetRuleUnknownActionId,
                    targetRule.Id,
                    targetRule.ActionId);
                return false;
            }

            for (int chainIndex = 0; chainIndex < targetRule.ActionChainIds.Length; chainIndex++)
            {
                string actionChainId = targetRule.ActionChainIds[chainIndex];
                if (data.ActionChains.ContainsKey(actionChainId))
                {
                    continue;
                }

                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.TargetRuleUnknownActionChainId,
                    targetRule.Id,
                    actionChainId);
                return false;
            }

            if (RequiresFacilityTypes(targetRule.TargetKind) &&
                targetRule.FacilityTypes.Length == 0)
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.TargetRuleMissingFacilityTypes,
                    targetRule.Id,
                    targetRule.TargetKind);
                return false;
            }

            if ((targetRule.TargetKind == CampusNpcEcologyTargetKind.RoomType ||
                 targetRule.TargetKind == CampusNpcEcologyTargetKind.RoomFacility) &&
                targetRule.RoomType == CampusRoomType.Unknown)
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.TargetRuleMissingRoomType,
                    targetRule.Id,
                    targetRule.TargetKind);
                return false;
            }

            for (int requirementIndex = 0; requirementIndex < targetRule.RequirementIds.Length; requirementIndex++)
            {
                string requirementId = targetRule.RequirementIds[requirementIndex];
                if (CampusNpcActionRequirementCatalog.IsKnownRequirement(requirementId))
                {
                    continue;
                }

                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.TargetRuleUnknownRequirement,
                    targetRule.Id,
                    requirementId);
                return false;
            }

            return true;
        }

        private static void RebuildActionTargetRuleIndex(EcologyPresetData data)
        {
            data.ActionTargetRulesByActionId.Clear();
            foreach (ActionTargetRuleRecord targetRule in data.ActionTargetRules.Values)
            {
                if (targetRule == null || string.IsNullOrEmpty(targetRule.ActionId))
                {
                    continue;
                }

                if (!data.ActionTargetRulesByActionId.TryGetValue(targetRule.ActionId, out List<ActionTargetRuleRecord> targetRules))
                {
                    targetRules = new List<ActionTargetRuleRecord>();
                    data.ActionTargetRulesByActionId[targetRule.ActionId] = targetRules;
                }

                targetRules.Add(targetRule);
            }

            foreach (List<ActionTargetRuleRecord> targetRules in data.ActionTargetRulesByActionId.Values)
            {
                targetRules.Sort(CompareTargetRulePriority);
            }
        }

        private static void ValidateActionCatalogTargets(EcologyPresetData data)
        {
            List<string> invalidActionIds = new List<string>();
            foreach (KeyValuePair<string, ActionRecord> pair in data.ActionCatalog)
            {
                if (data.ActionTargetRulesByActionId.ContainsKey(pair.Key))
                {
                    continue;
                }

                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.ActionMissingTargetRule,
                    pair.Key);
                invalidActionIds.Add(pair.Key);
            }

            for (int i = 0; i < invalidActionIds.Count; i++)
            {
                data.ActionCatalog.Remove(invalidActionIds[i]);
            }
        }

        private static void ValidateActionChains(EcologyPresetData data)
        {
            List<string> invalidChainIds = new List<string>();
            foreach (KeyValuePair<string, ActionChainRecord> pair in data.ActionChains)
            {
                if (!IsValidActionChain(pair.Value, data))
                {
                    invalidChainIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < invalidChainIds.Count; i++)
            {
                data.ActionChains.Remove(invalidChainIds[i]);
            }
        }

        private static bool IsValidActionChain(ActionChainRecord chain, EcologyPresetData data)
        {
            if (chain == null || string.IsNullOrEmpty(chain.Id) || chain.ActionIds.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < chain.ActionIds.Length; i++)
            {
                string actionId = chain.ActionIds[i];
                if (data.ActionCatalog.ContainsKey(actionId))
                {
                    continue;
                }

                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.ActionChainUnknownActionId,
                    chain.Id,
                    actionId);
                return false;
            }

            return true;
        }

        private static void ValidateNpcDecisionProfiles(EcologyPresetData data)
        {
            if (data.NpcDecisionProfiles.Count == 0)
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.NoDecisionProfiles,
                    PresetFileName);
                return;
            }

            for (int profileIndex = data.NpcDecisionProfiles.Count - 1; profileIndex >= 0; profileIndex--)
            {
                NpcDecisionProfileRecord profile = data.NpcDecisionProfiles[profileIndex];
                ValidateProfileEntries(profile, data);
                if (profile == null || profile.Entries.Count > 0)
                {
                    continue;
                }

                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.ProfileNoValidEntries,
                    profile.Id);
                data.NpcDecisionProfiles.RemoveAt(profileIndex);
            }
        }

        private static void ValidateProfileEntries(NpcDecisionProfileRecord profile, EcologyPresetData data)
        {
            if (profile == null)
            {
                return;
            }

            for (int entryIndex = profile.Entries.Count - 1; entryIndex >= 0; entryIndex--)
            {
                ScheduleEntryRecord entry = profile.Entries[entryIndex];
                if (entry != null &&
                    !string.IsNullOrEmpty(entry.ActionChainId) &&
                    data.ActionChains.ContainsKey(entry.ActionChainId))
                {
                    continue;
                }

                string entryId = entry != null ? entry.Id : string.Empty;
                string actionChainId = entry != null ? entry.ActionChainId : string.Empty;
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.ProfileEntryUnknownActionChainId,
                    profile.Id,
                    entryId,
                    actionChainId);
                profile.Entries.RemoveAt(entryIndex);
            }
        }

        private static int CompareTargetRulePriority(ActionTargetRuleRecord left, ActionTargetRuleRecord right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int priorityCompare = right.Priority.CompareTo(left.Priority);
            return priorityCompare != 0
                ? priorityCompare
                : string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
        }

        private static bool RequiresFacilityTypes(CampusNpcEcologyTargetKind targetKind)
        {
            switch (targetKind)
            {
                case CampusNpcEcologyTargetKind.StudentDesk:
                case CampusNpcEcologyTargetKind.TeacherPodium:
                case CampusNpcEcologyTargetKind.OfficeDesk:
                case CampusNpcEcologyTargetKind.PrimaryWorkstation:
                case CampusNpcEcologyTargetKind.RoomFacility:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryAddUnique<T>(
            Dictionary<string, T> records,
            string tableName,
            int rowIndex,
            string id,
            T record)
            where T : class
        {
            string normalizedId = NormalizeId(id);
            if (records == null || record == null || string.IsNullOrEmpty(normalizedId))
            {
                return false;
            }

            if (records.ContainsKey(normalizedId))
            {
                CampusNpcEcologyPresetLogTextCatalog.Warning(
                    CampusNpcEcologyPresetLogTextId.DuplicateTableId,
                    tableName,
                    normalizedId,
                    rowIndex);
                return false;
            }

            records.Add(normalizedId, record);
            return true;
        }
    }
}
