using System;
using System.Collections.Generic;
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
                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] Missing required preset file: " + PresetFileName);
                return emptyData;
            }

            try
            {
                EcologyPresetFile file = JsonUtility.FromJson<EcologyPresetFile>(json);
                return ParseFile(file);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] Failed to parse " + PresetFileName + ": " + exception.Message);
                return emptyData;
            }
        }

        private static EcologyPresetData ParseFile(EcologyPresetFile file)
        {
            EcologyPresetData data = new EcologyPresetData();
            if (file == null)
            {
                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] Preset file is empty: " + PresetFileName);
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
                ActionRecord action = ParseAction(files[i]);
                if (action != null && !string.IsNullOrEmpty(action.ActionId))
                {
                    data.ActionCatalog[action.ActionId] = action;
                }
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
                ActionTargetRuleRecord targetRule = ParseActionTargetRule(files[i]);
                if (targetRule != null && !string.IsNullOrEmpty(targetRule.Id))
                {
                    data.ActionTargetRules[targetRule.Id] = targetRule;
                }
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
                ActionChainRecord chain = ParseActionChain(files[i]);
                if (chain != null && !string.IsNullOrEmpty(chain.Id))
                {
                    data.ActionChains[chain.Id] = chain;
                }
            }
        }

        private static void ParseNpcDecisionProfiles(EcologyPresetData data, List<NpcDecisionProfileFileRecord> files)
        {
            if (files == null)
            {
                return;
            }

            for (int i = 0; i < files.Count; i++)
            {
                NpcDecisionProfileRecord profile = ParseDecisionProfile(files[i]);
                if (profile != null && profile.Entries.Count > 0)
                {
                    data.NpcDecisionProfiles.Add(profile);
                }
            }
        }

        private static ActionRecord ParseAction(ActionCatalogFileRecord file)
        {
            if (file == null || !TryParseActionMode(file.ActionMode, out CampusNpcEcologyActionMode actionMode))
            {
                return null;
            }

            return new ActionRecord
            {
                ActionId = NormalizeId(file.ActionId),
                ActionMode = actionMode,
                Payload = file.Payload ?? string.Empty,
                RepeatPolicy = ParseRepeatPolicy(file.RepeatPolicy)
            };
        }

        private static ActionTargetRuleRecord ParseActionTargetRule(ActionTargetRuleFileRecord file)
        {
            if (file == null || !TryParseTargetKind(file.TargetKind, out CampusNpcEcologyTargetKind targetKind))
            {
                return null;
            }

            return new ActionTargetRuleRecord
            {
                Id = NormalizeId(file.Id),
                ActionId = NormalizeId(file.ActionId),
                TargetKind = targetKind,
                RoomType = ParseRoomType(file.RoomType),
                FacilityTypes = ParseFacilityTypes(file.FacilityTypes),
                Owner = NormalizeId(file.Owner),
                SourceLocation = NormalizeId(file.SourceLocation),
                SourceContainerPrefix = NormalizeId(file.SourceContainerPrefix),
                DefinitionId = NormalizeId(file.DefinitionId),
                StopDistance = Mathf.Max(0.05f, file.StopDistance),
                RequirementIds = ParseRequirementIds(file.Requirements)
            };
        }

        private static ActionChainRecord ParseActionChain(ActionChainFileRecord file)
        {
            if (file == null)
            {
                return null;
            }

            return new ActionChainRecord
            {
                Id = NormalizeId(file.Id),
                ActionIds = NormalizeIds(file.ActionIds)
            };
        }

        private static NpcDecisionProfileRecord ParseDecisionProfile(NpcDecisionProfileFileRecord file)
        {
            if (file == null || !TryParseRole(file.Role, out CampusCharacterRole role))
            {
                return null;
            }

            NpcDecisionProfileRecord record = new NpcDecisionProfileRecord
            {
                Id = NormalizeId(file.Id),
                Role = role,
                CharacterIds = NormalizeIds(file.CharacterIds),
                TeacherDutyMask = ParseTeacherDutyMask(file.TeacherDuties),
                StaffDutyMask = ParseStaffDutyMask(file.StaffDuties),
                Traits = ParseTraits(file.Traits)
            };

            if (file.Entries != null)
            {
                for (int i = 0; i < file.Entries.Count; i++)
                {
                    ScheduleEntryRecord entry = ParseEntry(file.Entries[i]);
                    if (entry != null)
                    {
                        record.Entries.Add(entry);
                    }
                }
            }

            return record;
        }

        private static ScheduleEntryRecord ParseEntry(ScheduleEntryFileRecord file)
        {
            if (file == null || !TryParseIntentKind(file.IntentKind, out CampusNpcIntentKind intentKind))
            {
                return null;
            }

            string actionChainId = NormalizeId(file.ActionChainId);
            if (string.IsNullOrEmpty(actionChainId))
            {
                return null;
            }

            ScheduleEntryRecord record = new ScheduleEntryRecord
            {
                Id = NormalizeId(file.Id),
                Segments = ParseSegments(file.Segments),
                ScheduleWindows = ParseScheduleWindows(file.ScheduleWindows),
                IntentKind = intentKind,
                IntentLabel = string.IsNullOrWhiteSpace(file.IntentLabel) ? NormalizeId(file.Id) : file.IntentLabel.Trim(),
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

            if (!string.IsNullOrEmpty(targetRule.ActionId) &&
                !data.ActionCatalog.ContainsKey(targetRule.ActionId))
            {
                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] ActionTargetRule '" + targetRule.Id + "' references unknown ActionId '" + targetRule.ActionId + "'.");
                return false;
            }

            if (targetRule.TargetKind == CampusNpcEcologyTargetKind.RoomFacility &&
                targetRule.FacilityTypes.Length == 0)
            {
                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] RoomFacility ActionTargetRule '" + targetRule.Id + "' is missing FacilityTypes.");
                return false;
            }

            for (int requirementIndex = 0; requirementIndex < targetRule.RequirementIds.Length; requirementIndex++)
            {
                string requirementId = targetRule.RequirementIds[requirementIndex];
                if (CampusNpcActionRequirementCatalog.IsKnownRequirement(requirementId))
                {
                    continue;
                }

                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] ActionTargetRule '" + targetRule.Id + "' references unknown Requirement '" + requirementId + "'.");
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

                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] ActionCatalog action '" + pair.Key + "' has no ActionTargetRules row.");
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

                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] ActionChain '" + chain.Id + "' references unknown ActionId '" + actionId + "'.");
                return false;
            }

            return true;
        }

        private static void ValidateNpcDecisionProfiles(EcologyPresetData data)
        {
            if (data.NpcDecisionProfiles.Count == 0)
            {
                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] No NPC decision profiles were loaded from " + PresetFileName + ".");
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

                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] NPC decision profile '" + profile.Id + "' has no valid entries and was ignored.");
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
                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] NPC decision profile '" + profile.Id + "' entry '" + entryId + "' references unknown ActionChainId '" + actionChainId + "'.");
                profile.Entries.RemoveAt(entryIndex);
            }
        }
    }
}
