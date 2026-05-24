using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal static partial class CampusNpcEcologyPresetCatalog
    {
        public static bool TryResolveScheduledIntent(CampusNpcAiRuntime npc, out CampusNpcIntent intent)
        {
            intent = null;
            if (npc == null || npc.Runtime == null || npc.Data == null)
            {
                return false;
            }

            SelectedEntry best = default;
            bool hasBest = false;
            IReadOnlyList<ScheduleTemplateRecord> templates = Data.ScheduleTemplates;
            for (int templateIndex = 0; templateIndex < templates.Count; templateIndex++)
            {
                ScheduleTemplateRecord template = templates[templateIndex];
                if (!MatchesTemplate(template, npc))
                {
                    continue;
                }

                for (int entryIndex = 0; entryIndex < template.Entries.Count; entryIndex++)
                {
                    ScheduleEntryRecord entry = template.Entries[entryIndex];
                    if (!MatchesEntry(entry, npc) ||
                        !TryResolveActionChain(entry, out ActionChainRecord actionChain) ||
                        !TryResolveCurrentActionStep(npc, entry, actionChain, out int stepIndex, out ActionDefinitionRecord actionStep) ||
                        !PassesRuntimeRequirements(actionStep, npc) ||
                        !TryBuildChainStepOpportunity(npc, entry, actionChain, stepIndex, actionStep, out CampusNpcActionOpportunity opportunity))
                    {
                        continue;
                    }

                    float finalScore = CalculateOpportunityScore(
                        npc,
                        template,
                        entry,
                        actionStep,
                        opportunity);
                    if (!hasBest || finalScore > best.Score)
                    {
                        best = new SelectedEntry(template, entry, actionChain, actionStep, stepIndex, opportunity, finalScore);
                        hasBest = true;
                    }
                }
            }

            if (!hasBest)
            {
                return false;
            }

            npc.BeginActionChainStep(best.Entry.Id, best.ActionChain.Id, best.StepIndex);
            intent = best.Opportunity.ToIntent();
            LogSelection(npc, best);
            return true;
        }

        private static bool MatchesTemplate(ScheduleTemplateRecord template, CampusNpcAiRuntime npc)
        {
            CampusCharacterData data = npc.Data;
            if (template == null || data == null || data.Role != template.Role)
            {
                return false;
            }

            if (template.CharacterIds.Length > 0 && !ContainsId(template.CharacterIds, data.Id))
            {
                return false;
            }

            if (template.TeacherDutyMask != CampusTeacherDuty.None &&
                (data.TeacherDuty & template.TeacherDutyMask) == 0)
            {
                return false;
            }

            if (template.StaffDutyMask != CampusStaffDuty.None &&
                (data.StaffDuty & template.StaffDutyMask) == 0)
            {
                return false;
            }

            for (int i = 0; i < template.Traits.Length; i++)
            {
                if (!data.HasTrait(template.Traits[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesEntry(ScheduleEntryRecord entry, CampusNpcAiRuntime npc)
        {
            if (entry == null || npc == null)
            {
                return false;
            }

            if (entry.Segments.Length > 0 && !ContainsSegment(entry.Segments, npc.Segment))
            {
                return false;
            }

            if (entry.ScheduleWindows.Length > 0 && !MatchesAnyScheduleWindow(entry.ScheduleWindows, npc.Segment))
            {
                return false;
            }

            if (entry.HasClockRange &&
                !IsWithinClockRange(npc.CurrentClockMinute, entry.StartMinute, entry.EndMinute))
            {
                return false;
            }

            return true;
        }

        private static bool MatchesAnyScheduleWindow(
            CampusNpcEcologyScheduleWindow[] windows,
            CampusTimeSegment segment)
        {
            for (int i = 0; i < windows.Length; i++)
            {
                if (MatchesScheduleWindow(windows[i], segment))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesScheduleWindow(CampusNpcEcologyScheduleWindow window, CampusTimeSegment segment)
        {
            switch (window)
            {
                case CampusNpcEcologyScheduleWindow.Always:
                    return true;
                case CampusNpcEcologyScheduleWindow.ClassSession:
                    return CampusNpcScheduleFacts.IsClassSession(segment);
                case CampusNpcEcologyScheduleWindow.Break:
                    return CampusNpcScheduleFacts.IsBreak(segment);
                case CampusNpcEcologyScheduleWindow.MealPeak:
                    return CampusNpcScheduleFacts.IsMealPeak(segment);
                case CampusNpcEcologyScheduleWindow.StudentFreeMovement:
                    return CampusNpcScheduleFacts.IsStudentFreeMovementWindow(segment);
                case CampusNpcEcologyScheduleWindow.DormWindow:
                    return CampusNpcScheduleFacts.IsDormWindow(segment);
                case CampusNpcEcologyScheduleWindow.TeacherOfficeWindow:
                    return CampusNpcScheduleFacts.IsTeacherOfficeWindow(segment);
                case CampusNpcEcologyScheduleWindow.StaffOffDuty:
                    return CampusNpcScheduleFacts.IsStaffOffDuty(segment);
                default:
                    return false;
            }
        }

        private static bool ContainsSegment(CampusTimeSegment[] segments, CampusTimeSegment segment)
        {
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == segment)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWithinClockRange(int currentMinute, int startMinute, int endMinute)
        {
            if (startMinute == endMinute)
            {
                return true;
            }

            return startMinute < endMinute
                ? currentMinute >= startMinute && currentMinute < endMinute
                : currentMinute >= startMinute || currentMinute < endMinute;
        }

        private static bool TryResolveActionChain(
            ScheduleEntryRecord entry,
            out ActionChainRecord actionChain)
        {
            actionChain = null;
            if (entry == null || string.IsNullOrEmpty(entry.ActionChainId))
            {
                return false;
            }

            return Data.ActionChains.TryGetValue(entry.ActionChainId, out actionChain);
        }

        private static bool TryResolveCurrentActionStep(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionChainRecord actionChain,
            out int stepIndex,
            out ActionDefinitionRecord actionStep)
        {
            stepIndex = 0;
            actionStep = null;
            if (entry == null || actionChain == null || actionChain.StepIds.Length == 0)
            {
                return false;
            }

            if (npc != null &&
                npc.TryGetActiveActionChainStep(entry.Id, actionChain.Id, out int activeStepIndex) &&
                activeStepIndex >= 0 &&
                activeStepIndex < actionChain.StepIds.Length)
            {
                stepIndex = activeStepIndex;
            }

            return Data.ActionSteps.TryGetValue(actionChain.StepIds[stepIndex], out actionStep);
        }

        private static bool TryBuildChainStepOpportunity(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionChainRecord actionChain,
            int stepIndex,
            ActionDefinitionRecord actionStep,
            out CampusNpcActionOpportunity opportunity)
        {
            if (TryBuildOpportunity(npc, entry, actionStep, out opportunity))
            {
                bool advancesChain = opportunity.Action != null &&
                                     (opportunity.Action.Kind != CampusCharacterActionKind.NoOp ||
                                      actionStep.ActionMode == CampusNpcEcologyActionMode.NoOp);
                opportunity.AssignActionChain(
                    entry.Id,
                    actionChain.Id,
                    stepIndex,
                    actionChain.StepIds.Length,
                    advancesChain);
                return true;
            }

            if (npc != null && npc.IsActiveActionChain(entry.Id, actionChain.Id))
            {
                npc.ClearActionChainProgress();
            }

            return false;
        }

        private static bool PassesRuntimeRequirements(
            ActionDefinitionRecord actionDefinition,
            CampusNpcAiRuntime npc)
        {
            if (actionDefinition == null || npc == null)
            {
                return false;
            }

            return CampusNpcActionRequirementCatalog.PassesAll(
                npc.Runtime,
                actionDefinition.RequirementIds);
        }

        private static void LogSelection(CampusNpcAiRuntime npc, SelectedEntry selected)
        {
            if (!EnableSelectionDebug ||
                npc == null ||
                selected.Template == null ||
                selected.Entry == null ||
                selected.ActionStep == null)
            {
                return;
            }

            Debug.Log(
                "[NpcSchedule] npc=" + (npc.Data != null ? npc.Data.Id : string.Empty) +
                " template=" + selected.Template.Id +
                " entry=" + selected.Entry.Id +
                " chain=" + selected.ActionChain.Id +
                " step=" + selected.ActionStep.Id +
                " score=" + selected.Score.ToString("0.###"));
        }
    }
}
