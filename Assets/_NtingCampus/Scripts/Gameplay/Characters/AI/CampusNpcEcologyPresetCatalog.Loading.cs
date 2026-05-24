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
            ParseActionSteps(data, file.ActionSteps);
            ParseActionChains(data, file.ActionChains);
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

        private static void ParseActionSteps(EcologyPresetData data, List<ActionStepFileRecord> files)
        {
            if (files == null)
            {
                return;
            }

            for (int i = 0; i < files.Count; i++)
            {
                ActionDefinitionRecord step = ParseActionStep(files[i]);
                if (step != null && !string.IsNullOrEmpty(step.Id))
                {
                    data.ActionSteps[step.Id] = step;
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

        private static ActionDefinitionRecord ParseActionStep(ActionStepFileRecord file)
        {
            if (file == null ||
                !TryParseTargetKind(file.TargetKind, out CampusNpcEcologyTargetKind targetKind) ||
                !TryParseActionMode(file.ActionMode, out CampusNpcEcologyActionMode actionMode))
            {
                return null;
            }

            string executeActionId = NormalizeId(file.ExecuteActionId);
            return new ActionDefinitionRecord
            {
                Id = NormalizeId(file.Id),
                TargetKind = targetKind,
                RoomType = ParseRoomType(file.RoomType),
                FacilityGroupId = NormalizeId(file.FacilityGroupId),
                ActionMode = actionMode,
                ExecuteActionId = executeActionId,
                Payload = file.Payload ?? string.Empty,
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
                StepIds = NormalizeIds(file.Steps)
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

            ValidateActionSteps(data);
            ValidateActionChains(data);
            ValidateScheduleTemplates(data);
        }

        private static void ValidateActionSteps(EcologyPresetData data)
        {
            List<string> invalidStepIds = new List<string>();
            foreach (KeyValuePair<string, ActionDefinitionRecord> pair in data.ActionSteps)
            {
                if (!IsValidActionStep(pair.Value, data))
                {
                    invalidStepIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < invalidStepIds.Count; i++)
            {
                data.ActionSteps.Remove(invalidStepIds[i]);
            }
        }

        private static bool IsValidActionStep(ActionDefinitionRecord step, EcologyPresetData data)
        {
            if (step == null || string.IsNullOrEmpty(step.Id))
            {
                return false;
            }

            if ((step.ActionMode == CampusNpcEcologyActionMode.DomainAction ||
                 step.ActionMode == CampusNpcEcologyActionMode.PressInteractionAction) &&
                string.IsNullOrEmpty(step.ExecuteActionId))
            {
                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] ActionStep '" + step.Id + "' is missing ExecuteActionId.");
                return false;
            }

            if (step.TargetKind == CampusNpcEcologyTargetKind.RoomFacility)
            {
                if (string.IsNullOrEmpty(step.FacilityGroupId))
                {
                    Debug.LogWarning("[CampusNpcEcologyPresetCatalog] RoomFacility ActionStep '" + step.Id + "' is missing FacilityGroupId.");
                    return false;
                }

                if (!data.FacilityGroups.ContainsKey(step.FacilityGroupId))
                {
                    Debug.LogWarning("[CampusNpcEcologyPresetCatalog] ActionStep '" + step.Id + "' references unknown FacilityGroup '" + step.FacilityGroupId + "'.");
                    return false;
                }
            }

            for (int requirementIndex = 0; requirementIndex < step.RequirementIds.Length; requirementIndex++)
            {
                string requirementId = step.RequirementIds[requirementIndex];
                if (CampusNpcActionRequirementCatalog.IsKnownRequirement(requirementId))
                {
                    continue;
                }

                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] ActionStep '" + step.Id + "' references unknown Requirement '" + requirementId + "'.");
                return false;
            }

            return true;
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
            if (chain == null || string.IsNullOrEmpty(chain.Id) || chain.StepIds.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < chain.StepIds.Length; i++)
            {
                string stepId = chain.StepIds[i];
                if (data.ActionSteps.ContainsKey(stepId))
                {
                    continue;
                }

                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] ActionChain '" + chain.Id + "' references unknown ActionStep '" + stepId + "'.");
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
                ValidateTemplateEntries(template, data);
                if (template == null || template.Entries.Count > 0)
                {
                    continue;
                }

                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] Template '" + template.Id + "' has no valid entries and was ignored.");
                data.ScheduleTemplates.RemoveAt(templateIndex);
            }
        }

        private static void ValidateTemplateEntries(ScheduleTemplateRecord template, EcologyPresetData data)
        {
            if (template == null)
            {
                return;
            }

            for (int entryIndex = template.Entries.Count - 1; entryIndex >= 0; entryIndex--)
            {
                ScheduleEntryRecord entry = template.Entries[entryIndex];
                if (entry != null &&
                    !string.IsNullOrEmpty(entry.ActionChainId) &&
                    data.ActionChains.ContainsKey(entry.ActionChainId))
                {
                    continue;
                }

                string entryId = entry != null ? entry.Id : string.Empty;
                string actionChainId = entry != null ? entry.ActionChainId : string.Empty;
                Debug.LogWarning("[CampusNpcEcologyPresetCatalog] Template '" + template.Id + "' entry '" + entryId + "' references unknown ActionChainId '" + actionChainId + "'.");
                template.Entries.RemoveAt(entryIndex);
            }
        }
    }
}
