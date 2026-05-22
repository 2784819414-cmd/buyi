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
            ParseFacilityGroups(data, file.FacilityGroups);
            ParseActionDefinitions(data, file.ActionDefinitions);
            ParseScheduleTemplates(data, file.ScheduleTemplates);
            ValidateData(data);
            return data;
        }

        private static void ParseFacilityGroups(EcologyPresetData data, List<FacilityGroupFileRecord> groups)
        {
            if (groups == null)
            {
                return;
            }

            for (int i = 0; i < groups.Count; i++)
            {
                FacilityGroupFileRecord group = groups[i];
                string groupId = NormalizeId(group != null ? group.Id : string.Empty);
                if (string.IsNullOrEmpty(groupId))
                {
                    continue;
                }

                CampusFacilityType[] facilityTypes = ParseFacilityTypes(group.FacilityTypes);
                if (facilityTypes.Length > 0)
                {
                    data.FacilityGroups[groupId] = facilityTypes;
                }
            }
        }

        private static void ParseActionDefinitions(EcologyPresetData data, List<ActionDefinitionFileRecord> files)
        {
            if (files == null)
            {
                return;
            }

            for (int i = 0; i < files.Count; i++)
            {
                ActionDefinitionRecord definition = ParseActionDefinition(files[i]);
                AddActionDefinition(data, definition);
            }
        }

        private static void ParseScheduleTemplates(EcologyPresetData data, List<ScheduleTemplateFileRecord> files)
        {
            if (files == null)
            {
                return;
            }

            for (int i = 0; i < files.Count; i++)
            {
                ScheduleTemplateRecord template = ParseTemplate(files[i]);
                if (template != null && template.Entries.Count > 0)
                {
                    data.ScheduleTemplates.Add(template);
                }
            }
        }

        private static void AddActionDefinition(EcologyPresetData data, ActionDefinitionRecord definition)
        {
            if (data == null || definition == null || string.IsNullOrEmpty(definition.Id))
            {
                return;
            }

            data.ActionDefinitions[definition.Id] = definition;
        }

        private static ActionDefinitionRecord ParseActionDefinition(ActionDefinitionFileRecord file)
        {
            if (file == null ||
                !TryParseTargetKind(file.TargetKind, out CampusNpcEcologyTargetKind targetKind) ||
                !TryParseActionMode(file.ActionMode, out CampusNpcEcologyActionMode actionMode))
            {
                return null;
            }

            string executeActionId = NormalizeId(file.ExecuteActionId);
            if (string.IsNullOrEmpty(executeActionId))
            {
                executeActionId = NormalizeId(file.ActionId);
            }

            return new ActionDefinitionRecord
            {
                Id = NormalizeId(file.Id),
                TargetKind = targetKind,
                RoomType = ParseRoomType(file.RoomType),
                FacilityGroupId = NormalizeId(file.FacilityGroupId),
                ActionMode = actionMode,
                ExecuteActionId = executeActionId,
                Payload = file.Payload ?? string.Empty,
                StopDistance = Mathf.Max(0.05f, file.StopDistance),
                RequirementIds = ParseRequirementIds(file.Requirements)
            };
        }

        private static ScheduleTemplateRecord ParseTemplate(ScheduleTemplateFileRecord file)
        {
            if (file == null || !TryParseRole(file.Role, out CampusCharacterRole role))
            {
                return null;
            }

            ScheduleTemplateRecord record = new ScheduleTemplateRecord
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

            string actionId = NormalizeId(file.ActionId);
            if (string.IsNullOrEmpty(actionId))
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
                ActionId = actionId,
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

            ValidateActionDefinitions(data);
            ValidateScheduleTemplates(data);
        }

        private static void ValidateActionDefinitions(EcologyPresetData data)
        {
            List<string> invalidActionIds = new List<string>();
            foreach (KeyValuePair<string, ActionDefinitionRecord> pair in data.ActionDefinitions)
            {
                if (!IsValidActionDefinition(pair.Value, data))
                {
                    invalidActionIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < invalidActionIds.Count; i++)
            {
                data.ActionDefinitions.Remove(invalidActionIds[i]);
            }
        }

        private static bool IsValidActionDefinition(ActionDefinitionRecord definition, EcologyPresetData data)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id))
            {
                return false;
            }

            if ((definition.ActionMode == CampusNpcEcologyActionMode.DomainAction ||
                 definition.ActionMode == CampusNpcEcologyActionMode.PressInteractionAction) &&
                string.IsNullOrEmpty(definition.ExecuteActionId))
            {
                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] Action '" + definition.Id + "' is missing ExecuteActionId.");
                return false;
            }

            if (definition.TargetKind == CampusNpcEcologyTargetKind.RoomFacility)
            {
                if (string.IsNullOrEmpty(definition.FacilityGroupId))
                {
                    Debug.LogWarning("[CampusNpcEcologyPresetCatalog] RoomFacility action '" + definition.Id + "' is missing FacilityGroupId.");
                    return false;
                }

                if (!data.FacilityGroups.ContainsKey(definition.FacilityGroupId))
                {
                    Debug.LogWarning("[CampusNpcEcologyPresetCatalog] Action '" + definition.Id + "' references unknown FacilityGroup '" + definition.FacilityGroupId + "'.");
                    return false;
                }
            }

            for (int requirementIndex = 0; requirementIndex < definition.RequirementIds.Length; requirementIndex++)
            {
                string requirementId = definition.RequirementIds[requirementIndex];
                if (CampusNpcActionRequirementCatalog.IsKnownRequirement(requirementId))
                {
                    continue;
                }

                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] Action '" + definition.Id + "' references unknown Requirement '" + requirementId + "'.");
                return false;
            }

            return true;
        }

        private static void ValidateScheduleTemplates(EcologyPresetData data)
        {
            if (data.ScheduleTemplates.Count == 0)
            {
                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] No schedule templates were loaded from " + PresetFileName + ".");
                return;
            }

            for (int templateIndex = data.ScheduleTemplates.Count - 1; templateIndex >= 0; templateIndex--)
            {
                ScheduleTemplateRecord template = data.ScheduleTemplates[templateIndex];
                ValidateTemplateEntries(template, data.ActionDefinitions);
                if (template == null || template.Entries.Count > 0)
                {
                    continue;
                }

                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] Template '" + template.Id + "' has no valid entries and was ignored.");
                data.ScheduleTemplates.RemoveAt(templateIndex);
            }
        }

        private static void ValidateTemplateEntries(
            ScheduleTemplateRecord template,
            Dictionary<string, ActionDefinitionRecord> actionDefinitions)
        {
            if (template == null)
            {
                return;
            }

            for (int entryIndex = template.Entries.Count - 1; entryIndex >= 0; entryIndex--)
            {
                ScheduleEntryRecord entry = template.Entries[entryIndex];
                if (entry != null &&
                    !string.IsNullOrEmpty(entry.ActionId) &&
                    actionDefinitions.ContainsKey(entry.ActionId))
                {
                    continue;
                }

                string entryId = entry != null ? entry.Id : string.Empty;
                string actionId = entry != null ? entry.ActionId : string.Empty;
                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] Template '" + template.Id + "' entry '" + entryId + "' references unknown ActionId '" + actionId + "'.");
                template.Entries.RemoveAt(entryIndex);
            }
        }
    }
}
